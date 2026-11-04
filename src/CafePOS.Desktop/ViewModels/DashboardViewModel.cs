using CafePOS.Core.Contracts;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;

    public DashboardViewModel(ApiClient api, AuthSession session, BrandingService branding)
    {
        _api = api;
        _session = session;
        Branding = branding;
    }

    public BrandingService Branding { get; }

    [ObservableProperty] private DashboardDto? _dashboard;
    [ObservableProperty] private decimal _maxHourlySales = 1;
    [ObservableProperty] private string _greeting = "Welcome back";

    public string TodayLabel => DateTime.Now.ToString("dddd, dd MMMM yyyy");
    public string UserName => _session.User?.FullName ?? string.Empty;

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        Greeting = DateTime.Now.Hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };
        Dashboard = await _api.GetDashboardAsync();
        MaxHourlySales = Math.Max(1, Dashboard.HourlySales.Count == 0 ? 1 : Dashboard.HourlySales.Max(h => h.Sales));
    });
}
