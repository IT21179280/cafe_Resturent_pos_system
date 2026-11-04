using System.Windows;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop.Views;

public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow()
    {
        InitializeComponent();
    }

    private ChangePasswordViewModel? ViewModel => DataContext as ChangePasswordViewModel;

    private void OnCurrentChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.CurrentPassword = CurrentInput.Password;
    }

    private void OnNewChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.NewPassword = NewInput.Password;
    }

    private void OnConfirmChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null) ViewModel.ConfirmPassword = ConfirmInput.Password;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || ViewModel.IsBusy) return;
        if (await ViewModel.SaveAsync()) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
