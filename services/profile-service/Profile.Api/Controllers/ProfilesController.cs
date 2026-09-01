using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Profile.Application.DTOs;
using Profile.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Profile.Api.Controllers;

/// <summary>
/// Thin HTTP adapter for profile use cases.
/// All business logic lives in <see cref="ProfileService"/>; this class only maps
/// HTTP to use-case calls and results back to HTTP (§11).
/// </summary>
[ApiController]
[Route(Routes.ProfilesBase)]
[Authorize]
public sealed class ProfilesController : ControllerBase
{
    private readonly ProfileService _profileService;

    public ProfilesController(ProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>List all profiles under the caller's account.</summary>
    /// <response code="200">Profiles for this account.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListProfiles(CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.ListByAccountAsync(accountId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get one profile.</summary>
    /// <response code="200">Profile.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="403">Profile belongs to a different account.</response>
    /// <response code="404">No profile with that ID.</response>
    [HttpGet(Routes.ProfileById)]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid profileId, CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.GetByIdAsync(profileId, accountId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>Create a new profile under the caller's account.</summary>
    /// <response code="201">Profile created.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="409">Account already has the maximum number of profiles.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProfile(
        [FromBody] CreateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.CreateAsync(accountId.Value, request, cancellationToken);
        return CreatedAtAction(nameof(GetProfile), new { profileId = result.ProfileId }, result);
    }

    /// <summary>Update a profile.</summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="403">Profile belongs to a different account.</response>
    /// <response code="404">No profile with that ID.</response>
    [HttpPut(Routes.ProfileById)]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        Guid profileId,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateAsync(profileId, accountId.Value, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Delete a profile.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Access token missing, expired, or invalid.</response>
    /// <response code="403">Profile belongs to a different account.</response>
    /// <response code="404">No profile with that ID.</response>
    [HttpDelete(Routes.ProfileById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfile(Guid profileId, CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Unauthorized();
        }

        await _profileService.DeleteAsync(profileId, accountId.Value, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Extracts the account ID from the JWT sub claim.
    /// Returns null if the claim is missing or not a valid Guid.
    /// </summary>
    private Guid? GetAccountId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid");

        if (Guid.TryParse(sub, out var accountId))
        {
            return accountId;
        }

        return null;
    }
}
