using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CafePOS.Api.Data;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CafePOS.Api.Security;

public class TokenService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtOptions _options;

    public TokenService(AppDbContext db, UserManager<AppUser> userManager, IOptions<JwtOptions> options)
    {
        _db = db;
        _userManager = userManager;
        _options = options.Value;
    }

    public async Task<LoginResponse> IssueAsync(AppUser user, string? ip, CancellationToken ct = default)
    {
        var accessToken = CreateAccessToken(user, out var expiresAt);
        var refresh = CreateRefreshTokenValue();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refresh),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);
        return new LoginResponse(accessToken, refresh, expiresAt, ToDto(user));
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive || stored.User is null || !stored.User.IsActive)
            throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");

        var user = stored.User;
        stored.RevokedAt = DateTimeOffset.UtcNow;
        var newValue = CreateRefreshTokenValue();
        stored.ReplacedByTokenHash = Hash(newValue);
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(newValue),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);

        var accessToken = CreateAccessToken(user, out var expiresAt);
        return new LoginResponse(accessToken, newValue, expiresAt, ToDto(user));
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (stored is null) return;
        stored.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
    }

    public static UserDto ToDto(AppUser user) => new(user.Id, user.Email ?? string.Empty, user.UserName ?? string.Empty,
        user.FullName, user.Role, user.IsActive, user.MustChangePassword, user.CreatedAt, user.LastLoginAt);

    private string CreateAccessToken(AppUser user, out DateTimeOffset expiresAt)
    {
        expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? "user"),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("full_name", user.FullName),
            new("must_change_password", user.MustChangePassword.ToString().ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
