using System.Windows;
using System.Windows.Controls;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop.Views;

public partial class UsersView : UserControl
{
    public UsersView() => InitializeComponent();

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm && sender is PasswordBox box) vm.Password = box.Password;
    }

    private void OnResetPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm && sender is PasswordBox box) vm.ResetPassword = box.Password;
    }
}
