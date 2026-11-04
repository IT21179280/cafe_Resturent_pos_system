using System.Windows;

namespace CafePOS.Desktop.Views;

public partial class ModifierPickerWindow : Window
{
    public ModifierPickerWindow() => InitializeComponent();

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
