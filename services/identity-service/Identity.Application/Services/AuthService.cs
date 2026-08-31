using Identity.Application.DTOs;
using Identity.Application.Exceptions;
using Identity.Application.Interfaces;
using Identity.Domain.Exceptions;
using Identity.Domain.Repositories;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Services;

/// <summary>
/// Orchestrates all authentication use cases for the Identity service.
/// Contains no business rules — those live in the domain. This class only
/// coordinates domain objects, calls repositories, and maps results to DTOs.
/// </summary>
public sealed class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Registers a new account. Validates that the email is not already taken,
    /// then delegates password hashing and user creation to the domain.
    /// </summary>
    /// <exception cref="EmailAlreadyRegisteredException">
    /// Thrown when the email is already in use. Maps to HTTP 409.
    /// </exception>
    public async Task<UserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = Email.From(request.Email);

        var isEmailTaken = await _userRepository
            .ExistsWithEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        if (isEmailTaken)
        {
            throw new EmailAlreadyRegisteredException(email.Value);
        }

        var password = Password.From(request.Password);
        var passwordHash = _passwordHasher.Hash(password);
        var user = Domain.Entities.User.Create(email, passwordHash);

        await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);

        return MapToUserResponse(user);
    }

    /// <summary>
    /// Authenticates a user with email and password.
    /// Returns a token pair on success.
    /// </summary>
    /// <exception cref="InvalidCredentialsException">
    /// Thrown when the email is not found or the password does not match. Maps to HTTP 401.
    /// The error message is intentionally generic to prevent user enumeration.
    /// </exception>
    public async Task<TokenResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = Email.From(request.Email);

        var user = await _userRepository
            .FindByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        // Generic message — do not distinguish "email not found" from "wrong password"
        // to prevent account enumeration (openapi.yaml line 80 returns a single 401 for both).
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        var password = Password.From(request.Password);

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var (refreshTokenValue, refreshExpiresAtUtc) = _tokenService.GenerateRefreshToken();
        user.IssueRefreshToken(refreshTokenValue, refreshExpiresAtUtc);

        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        var (accessToken, accessExpiresAtUtc) = _tokenService.GenerateAccessToken(user);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAtUtc = accessExpiresAtUtc
        };
    }

    /// <summary>
    /// Rotates a refresh token: revokes the submitted one, issues a new pair.
    /// The domain enforces the single-use invariant.
    /// </summary>
    /// <exception cref="InvalidRefreshTokenException">
    /// Thrown when the token is not found, already revoked, or expired. Maps to HTTP 401.
    /// </exception>
    public async Task<TokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var (newRefreshTokenValue, newRefreshExpiresAtUtc) = _tokenService.GenerateRefreshToken();

        // FindByRefreshTokenAsync is a targeted query: look up the user who owns this token.
        // Doing FindAll + LINQ in memory would be unsafe at scale even if currently low-traffic.
        var user = await _userRepository
            .FindByRefreshTokenAsync(request.RefreshToken, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            throw new InvalidRefreshTokenException("Refresh token not found.");
        }

        // Domain enforces: not revoked, not expired.
        user.RotateRefreshToken(request.RefreshToken, newRefreshTokenValue, newRefreshExpiresAtUtc);

        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        var (accessToken, accessExpiresAtUtc) = _tokenService.GenerateAccessToken(user);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshTokenValue,
            ExpiresAtUtc = accessExpiresAtUtc
        };
    }

    /// <summary>
    /// Revokes a refresh token (logout).
    /// Domain deliberately allows revoking an expired token (openapi.yaml lines 120-124).
    /// </summary>
    /// <exception cref="InvalidRefreshTokenException">
    /// Thrown when the token is not found or already revoked. Maps to HTTP 401.
    /// </exception>
    public async Task RevokeTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository
            .FindByRefreshTokenAsync(request.RefreshToken, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            // Do not distinguish "not found" from "revoked" to prevent token oracle attacks.
            throw new InvalidRefreshTokenException("Refresh token invalid or already revoked.");
        }

        // Domain allows expired tokens here (logout with dead session must succeed).
        user.RevokeRefreshToken(request.RefreshToken);

        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the identity of the currently authenticated user.
    /// The caller must have already validated the JWT and extracted the UserId claim.
    /// </summary>
    /// <exception cref="UserNotFoundException">Thrown when the UserId from the JWT has no matching row.</exception>
    public async Task<UserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var typedId = UserId.From(userId);

        var user = await _userRepository
            .FindByIdAsync(typedId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            throw new UserNotFoundException(userId);
        }

        return MapToUserResponse(user);
    }

    private static UserResponse MapToUserResponse(Domain.Entities.User user)
    {
        return new UserResponse
        {
            UserId = user.Id.Value,
            Email = user.Email.Value,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }
}
