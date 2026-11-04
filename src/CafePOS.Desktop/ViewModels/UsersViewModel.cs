using System.Collections.ObjectModel;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class UsersViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private readonly DialogService _dialogs;

    public UsersViewModel(ApiClient api, AuthSession session, DialogService dialogs)
    {
        _api = api;
        _session = session;
        _dialogs = dialogs;
    }

    public ObservableCollection<UserDto> Users { get; } = new();
    public UserRole[] Roles { get; } = Enum.GetValues<UserRole>();

    [ObservableProperty] private UserDto? _selectedUser;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private UserRole _selectedRole = UserRole.Cashier;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _resetPassword = string.Empty;
    [ObservableProperty] private string _editorTitle = "New user";

    public bool IsExisting => SelectedUser is not null;
    public bool IsSelf => SelectedUser is not null && SelectedUser.Id == _session.User?.Id;

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var selectedId = SelectedUser?.Id;
        var users = await _api.GetUsersAsync();
        Users.Clear();
        foreach (var user in users.OrderBy(u => u.FullName)) Users.Add(user);
        SelectedUser = Users.FirstOrDefault(u => u.Id == selectedId);
    });

    partial void OnSelectedUserChanged(UserDto? value)
    {
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(IsSelf));
        if (value is null)
        {
            EditorTitle = "New user";
            return;
        }
        EditorTitle = value.FullName;
        Email = value.Email;
        FullName = value.FullName;
        SelectedRole = value.RoleName;
        IsActive = value.IsActive;
        Password = string.Empty;
        ResetPassword = string.Empty;
    }

    [RelayCommand]
    private void NewUser()
    {
        SelectedUser = null;
        Email = string.Empty;
        FullName = string.Empty;
        SelectedRole = UserRole.Cashier;
        IsActive = true;
        Password = string.Empty;
        ResetPassword = string.Empty;
        EditorTitle = "New user";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "Enter the user's full name.";
            return;
        }
        if (SelectedUser is null && string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Enter the email address.";
            return;
        }
        if (SelectedUser is null && Password.Length < 8)
        {
            ErrorMessage = "The initial password must be at least 8 characters.";
            return;
        }
        await RunAsync(async () =>
        {
            if (SelectedUser is null)
            {
                await _api.CreateUserAsync(new CreateUserRequest(Email.Trim(), FullName.Trim(), SelectedRole, Password));
                StatusMessage = "User created.";
            }
            else
            {
                await _api.UpdateUserAsync(SelectedUser.Id,
                    new UpdateUserRequest(FullName.Trim(), SelectedRole, IsActive));
                StatusMessage = "User updated.";
            }
            await LoadCoreAsync();
        });
    }

    private async Task LoadCoreAsync()
    {
        var selectedId = SelectedUser?.Id;
        var users = await _api.GetUsersAsync();
        Users.Clear();
        foreach (var user in users.OrderBy(u => u.FullName)) Users.Add(user);
        SelectedUser = Users.FirstOrDefault(u => u.Id == selectedId);
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is null) return;
        if (ResetPassword.Length < 8)
        {
            ErrorMessage = "The new password must be at least 8 characters.";
            return;
        }
        if (!_dialogs.Confirm($"Reset the password for {SelectedUser.Email}?")) return;
        await RunAsync(async () =>
        {
            await _api.ResetPasswordAsync(SelectedUser.Id, ResetPassword);
            ResetPassword = string.Empty;
            StatusMessage = "Password reset. The user must change it at next sign-in.";
        });
    }

    [RelayCommand]
    private async Task RevokeSessionsAsync()
    {
        if (SelectedUser is null) return;
        if (!_dialogs.Confirm($"Sign {SelectedUser.FullName} out of all devices?")) return;
        await RunAsync(async () =>
        {
            await _api.RevokeAllTokensAsync(SelectedUser.Id);
            StatusMessage = "All refresh tokens revoked.";
        });
    }
}
