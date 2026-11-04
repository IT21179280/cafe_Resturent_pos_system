using System.Security.Cryptography;
using CafePOS.Api.Data;
using CafePOS.Api.Security;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Administrator")]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly TokenService _tokens;
    private readonly AuditService _audit;
    private readonly CurrentUser _currentUser;
    private readonly AppDbContext _db;

    public UsersController(UserManager<AppUser> userManager, TokenService tokens, AuditService audit,
        CurrentUser currentUser, AppDbContext db)
    {
        _userManager = userManager;
        _tokens = tokens;
        _audit = audit;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> List([FromQuery] bool includeInactive = true)
    {
        var query = _userManager.Users.AsNoTracking();
        if (!includeInactive) query = query.Where(u => u.IsActive);
        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        return Ok(users.Select(TokenService.ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> Get(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        return user is null ? NotFound() : Ok(TokenService.ToDto(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request)
    {
        if (!Enum.IsDefined(request.Role)) return BadRequest(new ApiErrorResponse("Invalid role."));
        if (await _userManager.FindByEmailAsync(request.Email.Trim()) is not null)
            return Conflict(new ApiErrorResponse("A user with this email already exists."));

        var user = new AppUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            IsActive = true,
            MustChangePassword = true
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new ApiErrorResponse("Could not create the user.", null,
                new Dictionary<string, string[]> { ["user"] = result.Errors.Select(e => e.Description).ToArray() }));

        await _userManager.AddToRoleAsync(user, request.Role.ToString());
        await _audit.LogAsync("UserCreated", nameof(AppUser), user.Id.ToString(), new { user.Email, user.Role });
        return CreatedAtAction(nameof(Get), new { id = user.Id }, TokenService.ToDto(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (!Enum.IsDefined(request.Role)) return BadRequest(new ApiErrorResponse("Invalid role."));
        if (id == _currentUser.UserId && !request.IsActive)
            return BadRequest(new ApiErrorResponse("You cannot disable your own account."));
        if (id == _currentUser.UserId && request.Role != user.Role)
            return BadRequest(new ApiErrorResponse("You cannot change your own role."));
        if (user.Role == UserRole.Administrator && request.Role != UserRole.Administrator && !request.IsActive)
            await EnsureNotLastAdmin(user);

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new ApiErrorResponse("Could not update the user.", null,
                new Dictionary<string, string[]> { ["user"] = result.Errors.Select(e => e.Description).ToArray() }));

        var existingRoles = await _userManager.GetRolesAsync(user);
        if (!existingRoles.Contains(request.Role.ToString()))
        {
            await _userManager.RemoveFromRolesAsync(user, existingRoles);
            await _userManager.AddToRoleAsync(user, request.Role.ToString());
        }

        if (!user.IsActive) await _tokens.RevokeAllAsync(user.Id);
        await _audit.LogAsync("UserUpdated", nameof(AppUser), user.Id.ToString(),
            new { user.Email, user.Role, user.IsActive });
        return Ok(TokenService.ToDto(user));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<object>> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var password = string.IsNullOrWhiteSpace(request.NewPassword) ? GeneratePassword() : request.NewPassword;
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            return BadRequest(new ApiErrorResponse("Could not reset the password.", null,
                new Dictionary<string, string[]> { ["password"] = result.Errors.Select(e => e.Description).ToArray() }));

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);
        await _tokens.RevokeAllAsync(user.Id);
        await _audit.LogAsync("PasswordReset", nameof(AppUser), user.Id.ToString(), new { user.Email });
        return Ok(new { message = "Password reset. The user must change it at next login.", temporaryPassword = password });
    }

    [HttpGet("audit")]
    public async Task<ActionResult> AuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { items, total, page, pageSize });
    }

    private async Task EnsureNotLastAdmin(AppUser user)
    {
        var admins = await _userManager.GetUsersInRoleAsync(UserRole.Administrator.ToString());
        if (admins.Count(a => a.IsActive) <= 1)
            throw new InvalidOperationException("At least one active administrator must remain.");
    }

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%";
        var all = upper + lower + digits + special;
        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            special[RandomNumberGenerator.GetInt32(special.Length)]
        };
        while (chars.Count < 12) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(1000)).ToArray());
    }
}
