using System.Windows;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop.Views;

public partial class CheckoutWindow : Window
{
    public CheckoutWindow()
    {
        InitializeComponent();
    }

    private CheckoutViewModel? ViewModel => DataContext as CheckoutViewModel;

    private async void OnComplete(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || ViewModel.IsBusy) return;
        await ViewModel.CompleteAsync();
        if (ViewModel.CompletedOrder is not null) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
