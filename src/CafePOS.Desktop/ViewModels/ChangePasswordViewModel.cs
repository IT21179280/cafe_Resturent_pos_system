using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafePOS.Desktop.ViewModels;

public partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;

    public ChangePasswordViewModel(ApiClient api, AuthSession session)
    {
        _api = api;
        _session = session;
    }

    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _isForced;

    public string Heading => IsForced ? "Set a new password to continue" : "Change password";
    public string UserEmail => _session.User?.Email ?? string.Empty;

    public async Task<bool> SaveAsync()
    {
        var ok = false;
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(CurrentPassword)) throw new ApiException("Enter your current password.");
            if (NewPassword.Length < 8) throw new ApiException("The new password must be at least 8 characters.");
            if (NewPassword != ConfirmPassword) throw new ApiException("The new passwords do not match.");
            if (NewPassword == CurrentPassword) throw new ApiException("Choose a password different from the current one.");

            await _api.ChangePasswordAsync(CurrentPassword, NewPassword);
            var user = await _api.MeAsync();
            _session.UpdateUser(user);
            StatusMessage = "Password updated successfully.";
            ok = true;
        });
        return ok;
    }
}
