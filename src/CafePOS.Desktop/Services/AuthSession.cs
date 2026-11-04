using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;

namespace CafePOS.Desktop.Services;

public class AuthSession
{
    public UserDto? User { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsAuthenticated => User is not null && !string.IsNullOrWhiteSpace(AccessToken);
    public bool IsAdministrator => User?.RoleName == UserRole.Administrator;
    public bool IsManager => User?.RoleName is UserRole.Administrator or UserRole.Manager;
    public bool IsCashier => User?.RoleName == UserRole.Cashier;
    public bool IsKitchen => User?.RoleName == UserRole.Kitchen;
    public bool CanSell => User?.RoleName is UserRole.Administrator or UserRole.Manager or UserRole.Cashier;
    public bool MustChangePassword => User?.MustChangePassword == true;

    public event Action? SessionExpired;

    public void Set(LoginResponse response)
    {
        User = response.User;
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        ExpiresAt = response.ExpiresAt;
    }

    public void UpdateUser(UserDto user) => User = user;

    public void Clear()
    {
        User = null;
        AccessToken = null;
        RefreshToken = null;
        ExpiresAt = default;
    }

    public void NotifyExpired()
    {
        Clear();
        SessionExpired?.Invoke();
    }
}
