using System.Windows;
using CafePOS.Desktop.ViewModels;

namespace CafePOS.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Closed += (_, _) => _vm.Stop();
    }
}
