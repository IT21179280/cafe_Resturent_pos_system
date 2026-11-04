using System.Windows;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop.Views;

public partial class PurchaseOrderEditWindow : Window
{
    public PurchaseOrderEditWindow() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PurchaseOrderEditViewModel vm || vm.IsBusy) return;
        await vm.SaveAsync();
        if (!string.IsNullOrEmpty(vm.OrderNumber)) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
