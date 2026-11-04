using System.Security.Claims;
using CafePOS.Core.Enums;

namespace CafePOS.Api.Security;

public class CurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public string Name => Principal?.FindFirstValue(ClaimTypes.Name) ?? Principal?.Identity?.Name ?? "system";

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.Cashier;

    public bool IsInRole(params UserRole[] roles) => roles.Contains(Role);
    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
