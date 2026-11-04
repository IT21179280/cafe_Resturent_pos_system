using System.Windows;
using System.Windows.Threading;
using CafePOS.Desktop.Services;
using CafePOS.Desktop.ViewModels;
using CafePOS.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CafePOS.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var settings = AppSettingsStore.Load();
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddSingleton<AuthSession>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ApiClient>();
        services.AddSingleton<BrandingService>();
        services.AddSingleton<SignalRService>();
        services.AddSingleton<ReceiptService>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<OrdersViewModel>();
        services.AddTransient<TablesViewModel>();
        services.AddTransient<KitchenViewModel>();
        services.AddTransient<MenuViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<PurchaseOrdersViewModel>();
        services.AddTransient<ShiftsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<SettingsViewModel>();

        Services = services.BuildServiceProvider();
        ShowLogin();
    }

    public static void ShowLogin()
    {
        var vm = Services.GetRequiredService<LoginViewModel>();
        var window = new LoginWindow(vm);
        Current.MainWindow = window;
        window.Show();
    }

    public static void ShowMain()
    {
        var vm = Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow(vm);
        Current.MainWindow = window;

        vm.LogoutRequested += () =>
        {
            window.Close();
            ShowLogin();
        };
        vm.SessionExpired += () =>
        {
            var hub = Services.GetRequiredService<SignalRService>();
            _ = hub.DisconnectAsync();
            window.Close();
            ShowLogin();
        };

        window.Show();
        _ = vm.InitializeAsync();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will keep running. " +
            "If the problem continues, check the log files in the CafePOS data folder.",
            "CafePOS", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Services is IDisposable disposable) disposable.Dispose();
        base.OnExit(e);
    }
}
