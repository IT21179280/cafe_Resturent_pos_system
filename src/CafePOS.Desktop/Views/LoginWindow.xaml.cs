using System.Windows;
using System.Windows.Input;
using CafePOS.Desktop.Services;
using CafePOS.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CafePOS.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) =>
        {
            PasswordInput.Focus();
            _ = vm.InitializeBrandingAsync();
        };
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _vm.Password = PasswordInput.Password;

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnSignIn(sender, e);
    }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        if (_vm.IsBusy) return;
        var success = await _vm.LoginAsync();
        if (!success) return;

        if (_vm.SignedInUser?.MustChangePassword == true)
        {
            var services = App.Services;
            var changeVm = services.GetRequiredService<ChangePasswordViewModel>();
            changeVm.IsForced = true;
            var dialog = new ChangePasswordWindow { DataContext = changeVm, Owner = this };
            if (dialog.ShowDialog() != true)
            {
                var api = services.GetRequiredService<ApiClient>();
                var session = services.GetRequiredService<AuthSession>();
                await api.LogoutAsync();
                session.Clear();
                PasswordInput.Clear();
                return;
            }
        }

        App.ShowMain();
        Close();
    }
}
