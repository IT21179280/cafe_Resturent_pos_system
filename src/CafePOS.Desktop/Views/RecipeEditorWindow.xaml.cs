using System.Windows;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop.Views;

public partial class RecipeEditorWindow : Window
{
    public RecipeEditorWindow() => InitializeComponent();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RecipeEditViewModel vm || vm.IsBusy) return;
        await vm.SaveAsync();
        if (!vm.HasError) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
