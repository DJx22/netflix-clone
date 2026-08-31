using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for authentication use cases.
/// All business logic lives in <see cref="AuthService"/>; this class only maps
/// HTTP to use-case calls and results back to HTTP (§11).
/// </summary>
[ApiController]
[Route(Routes.AuthBase)]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Register a new account.</summary>
    /// <response code="201">Account created.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost(Routes.Register)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCurrentUser), new { }, result);
    }

    /// <summary>Exchange credentials for an access token and refresh token.</summary>
    /// <response code="200">Authenticated.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost(Routes.Login)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Exchange a valid refresh token for a new access token.</summary>
    /// <response code="200">New token pair issued.</response>
    /// <response code="401">Refresh token invalid, expired, or already revoked.</response>
    [HttpPost(Routes.Refresh)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Revoke a refresh token (logout).</summary>
    /// <response code="204">Revoked.</response>
    /// <response code="401">Refresh token invalid or already revoked.</response>
    [HttpPost(Routes.Revoke)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeTokenAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Return the identity of the currently authenticated caller.</summary>
    /// <response code="200">Current user.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    [HttpGet(Routes.Me)]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        // The JWT middleware has already validated the token by the time we reach here (§12).
        // Extract the subject claim — AuthService.GetCurrentUserAsync handles the DB lookup.
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(sub, out var userId))
        {
            // A valid, structurally correct JWT with a non-Guid sub claim should not happen,
            // but we must not crash or expose internals if it does.
            return Unauthorized();
        }

        var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(result);
    }
}
