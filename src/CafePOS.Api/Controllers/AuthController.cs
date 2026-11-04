using CafePOS.Api.Data;
using CafePOS.Api.Security;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly TokenService _tokens;
    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;
    private readonly AuditService _audit;

    public AuthController(UserManager<AppUser> userManager, TokenService tokens, AppDbContext db,
        CurrentUser currentUser, AuditService audit)
    {
        _userManager = userManager;
        _tokens = tokens;
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Unauthorized(new ApiErrorResponse("Invalid email or password."));
        if (!user.IsActive)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("This account has been disabled. Contact an administrator."));
        if (await _userManager.IsLockedOutAsync(user))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new ApiErrorResponse("Too many failed attempts. Try again in a few minutes."));
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new ApiErrorResponse("Invalid email or password."));
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        var response = await _tokens.IssueAsync(user, _currentUser.IpAddress, ct);
        await _audit.LogAsync("Login", nameof(AppUser), user.Id.ToString(), new { user.Email }, ct);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _tokens.RefreshAsync(request.RefreshToken, _currentUser.IpAddress, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiErrorResponse(ex.Message));
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _tokens.RevokeAsync(request.RefreshToken, ct);
        await _audit.LogAsync("Logout", nameof(AppUser), _currentUser.UserId.ToString(), null, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());
        if (user is null) return Unauthorized();
        return Ok(TokenService.ToDto(user));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());
        if (user is null) return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new ApiErrorResponse("Password change failed.", null,
                new Dictionary<string, string[]> { ["password"] = result.Errors.Select(e => e.Description).ToArray() }));

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        await _tokens.RevokeAllAsync(user.Id, ct);
        await _audit.LogAsync("PasswordChanged", nameof(AppUser), user.Id.ToString(), null, ct);

        var response = await _tokens.IssueAsync(user, _currentUser.IpAddress, ct);
        return Ok(response);
    }

    [HttpPost("tokens/revoke-all")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> RevokeAll(Guid userId, CancellationToken ct)
    {
        await _tokens.RevokeAllAsync(userId, ct);
        await _audit.LogAsync("SessionsRevoked", nameof(AppUser), userId.ToString(), null, ct);
        return NoContent();
    }
}
