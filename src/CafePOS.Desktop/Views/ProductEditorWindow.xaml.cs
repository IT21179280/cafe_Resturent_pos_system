using System.Windows;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop.Views;

public partial class ProductEditorWindow : Window
{
    public ProductEditorWindow() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductEditViewModel vm || vm.IsBusy) return;
        await vm.SaveAsync();
        if (vm.Saved) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
