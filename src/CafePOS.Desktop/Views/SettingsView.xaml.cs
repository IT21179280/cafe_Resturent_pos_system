using System.Windows;
using System.Windows.Controls;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private void OnFirebaseTokenChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && sender is PasswordBox box)
            vm.FirebaseAuthToken = box.Password;
    }
}
