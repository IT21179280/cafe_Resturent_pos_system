using System.IO;
using System.Windows;
using CafePOS.Core.Contracts;
using CafePOS.Desktop.Services;
using CafePOS.Desktop.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CafePOS.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly AppSettingsStore _local;
    private readonly AuthSession _session;
    private readonly BrandingService _branding;

    public SettingsViewModel(ApiClient api, AppSettingsStore local, AuthSession session, BrandingService branding)
    {
        _api = api;
        _local = local;
        _session = session;
        _branding = branding;
        ServerUrl = local.ServerUrl;
        ThermalPrinterName = local.ThermalPrinterName;
        UseThermalPrinter = local.UseThermalPrinter;
        KitchenPrinterName = local.KitchenPrinterName;
        UseKitchenPrinter = local.UseKitchenPrinter;
    }

    public List<string> InventoryEvents { get; } = new() { "Completed", "Served", "Paid" };

    [ObservableProperty] private string _systemName = "CafePOS";
    [ObservableProperty] private string _logoUrl = string.Empty;
    [ObservableProperty] private string _restaurantName = string.Empty;
    [ObservableProperty] private string _restaurantAddress = string.Empty;
    [ObservableProperty] private string _restaurantPhone = string.Empty;
    [ObservableProperty] private string _restaurantEmail = string.Empty;
    [ObservableProperty] private string _serviceChargeRateText = "0";
    [ObservableProperty] private string _defaultTaxRateText = "0";
    [ObservableProperty] private string _receiptFooter = string.Empty;
    [ObservableProperty] private string _inventoryDeductionEvent = "Completed";

    [ObservableProperty] private string _firebaseDatabaseUrl = string.Empty;
    [ObservableProperty] private bool _firebaseSyncEnabled;
    [ObservableProperty] private string _firebaseAuthToken = string.Empty;
    [ObservableProperty] private bool _firebaseHasAuthToken;
    [ObservableProperty] private bool _clearFirebaseAuthToken;
    [ObservableProperty] private string _firebaseStatus = string.Empty;

    [ObservableProperty] private string _serverUrl = string.Empty;
    [ObservableProperty] private string _thermalPrinterName = string.Empty;
    [ObservableProperty] private bool _useThermalPrinter;
    [ObservableProperty] private string _kitchenPrinterName = string.Empty;
    [ObservableProperty] private bool _useKitchenPrinter;

    public string UserEmail => _session.User?.Email ?? string.Empty;
    public string UserRole => _session.User?.RoleName.ToString() ?? string.Empty;

    public override async Task InitializeAsync() => await RunAsync(async () =>
    {
        var settings = await _api.GetSettingsAsync();
        ApplySettings(settings);
    });

    private void ApplySettings(AppSettingsDto settings)
    {
        _branding.Apply(settings);
        SystemName = settings.SystemName;
        LogoUrl = settings.LogoUrl ?? string.Empty;
        RestaurantName = settings.RestaurantName;
        RestaurantAddress = settings.RestaurantAddress;
        RestaurantPhone = settings.RestaurantPhone;
        RestaurantEmail = settings.RestaurantEmail;
        ServiceChargeRateText = settings.ServiceChargeRate.ToString("0.##");
        DefaultTaxRateText = settings.DefaultTaxRate.ToString("0.##");
        ReceiptFooter = settings.ReceiptFooter;
        InventoryDeductionEvent = settings.InventoryDeductionEvent;
        FirebaseDatabaseUrl = settings.FirebaseDatabaseUrl ?? string.Empty;
        FirebaseSyncEnabled = settings.FirebaseSyncEnabled;
        FirebaseHasAuthToken = settings.FirebaseHasAuthToken;
        ClearFirebaseAuthToken = false;
        FirebaseAuthToken = string.Empty;
        var last = settings.FirebaseLastSyncedAt?.ToLocalTime().ToString("g");
        FirebaseStatus = settings.FirebaseSyncEnabled
            ? (settings.FirebaseLastSyncedAt.HasValue
                ? (string.IsNullOrWhiteSpace(settings.FirebaseLastError)
                    ? $"Enabled — last synced {last}"
                    : $"Enabled — last error: {settings.FirebaseLastError}")
                : "Enabled — not synced yet")
            : "Disabled";
    }

    [RelayCommand]
    private async Task SaveRestaurantAsync()
    {
        if (string.IsNullOrWhiteSpace(SystemName))
        {
            ErrorMessage = "Enter the POS system name.";
            return;
        }
        if (!decimal.TryParse(ServiceChargeRateText, out var service) || service < 0 || service > 100)
        {
            ErrorMessage = "Enter a service charge between 0 and 100.";
            return;
        }
        if (!decimal.TryParse(DefaultTaxRateText, out var tax) || tax < 0 || tax > 100)
        {
            ErrorMessage = "Enter a default tax rate between 0 and 100.";
            return;
        }
        if (FirebaseSyncEnabled && string.IsNullOrWhiteSpace(FirebaseDatabaseUrl))
        {
            ErrorMessage = "Enter the Firebase database URL to enable sync.";
            return;
        }
        await RunAsync(async () =>
        {
            var updated = await _api.UpdateSettingsAsync(new UpdateAppSettingsRequest(RestaurantName.Trim(),
                RestaurantAddress.Trim(), RestaurantPhone.Trim(), RestaurantEmail.Trim(), service, tax,
                ReceiptFooter.Trim(), InventoryDeductionEvent, SystemName.Trim(),
                string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
                string.IsNullOrWhiteSpace(FirebaseDatabaseUrl) ? null : FirebaseDatabaseUrl.Trim(),
                FirebaseSyncEnabled,
                string.IsNullOrWhiteSpace(FirebaseAuthToken) ? null : FirebaseAuthToken.Trim(),
                ClearFirebaseAuthToken));
            ApplySettings(updated);
            StatusMessage = "Restaurant, branding and Firebase settings saved.";
        });
    }

    [RelayCommand]
    private async Task SyncFirebaseNowAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _api.SyncFirebaseNowAsync();
            var settings = await _api.GetSettingsAsync();
            ApplySettings(settings);
            if (result.Success)
                StatusMessage = result.Message ?? "Firebase sync completed.";
            else
                ErrorMessage = result.Message ?? "Firebase sync failed.";
        });
    }

    [RelayCommand]
    private async Task ChooseLogoAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose POS logo",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        await RunAsync(async () =>
        {
            await using var stream = File.OpenRead(dialog.FileName);
            LogoUrl = await _api.UploadImageAsync(stream, Path.GetFileName(dialog.FileName), "branding");
            StatusMessage = "Logo uploaded. Save the restaurant settings to publish it.";
        });
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoUrl = string.Empty;
        StatusMessage = "Logo removed. Save the restaurant settings to publish the change.";
    }

    [RelayCommand]
    private void SaveDevice()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            ErrorMessage = "Enter the server address.";
            return;
        }
        if (UseKitchenPrinter && string.IsNullOrWhiteSpace(KitchenPrinterName))
        {
            ErrorMessage = "Enter the Windows printer name for kitchen tickets.";
            return;
        }
        var oldUrl = _local.ServerUrl;
        _local.ServerUrl = ServerUrl.Trim().TrimEnd('/');
        _local.ThermalPrinterName = ThermalPrinterName.Trim();
        _local.UseThermalPrinter = UseThermalPrinter;
        _local.KitchenPrinterName = KitchenPrinterName.Trim();
        _local.UseKitchenPrinter = UseKitchenPrinter;
        _local.Save();
        _api.SetServerUrl(_local.ServerUrl);
        ServerUrl = _local.ServerUrl;
        StatusMessage = string.Equals(oldUrl, _local.ServerUrl, StringComparison.OrdinalIgnoreCase)
            ? "Device settings saved."
            : "Server address changed. Sign out and back in to reconnect.";
    }

    [RelayCommand]
    private async Task TestConnectionAsync() => await RunAsync(async () =>
    {
        var ok = await _api.CheckHealthAsync();
        if (ok) StatusMessage = $"Connected to {_api.ServerUrl}.";
        else ErrorMessage = $"Could not reach {_api.ServerUrl}. Check the address and that the server is running.";
    });

    [RelayCommand]
    private void ChangePassword()
    {
        var vm = new ChangePasswordViewModel(_api, _session);
        var window = new ChangePasswordWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true) StatusMessage = "Password changed.";
    }
}
