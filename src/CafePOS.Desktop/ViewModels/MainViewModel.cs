using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public sealed class NavItem
{
    public NavItem(string title, string glyph, object viewModel)
    {
        Title = title;
        Glyph = glyph;
        ViewModel = viewModel;
    }

    public string Title { get; }
    public string Glyph { get; }
    public object ViewModel { get; }
}

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthSession _session;
    private readonly ApiClient _api;
    private readonly SignalRService _hub;
    private readonly PosViewModel _pos;
    private readonly DispatcherTimer _clock;

    public MainViewModel(AuthSession session, ApiClient api, SignalRService hub, BrandingService branding,
        PosViewModel pos, DashboardViewModel dashboard, OrdersViewModel orders, TablesViewModel tables,
        KitchenViewModel kitchen, MenuViewModel menu, InventoryViewModel inventory, SuppliersViewModel suppliers,
        PurchaseOrdersViewModel purchasing, ShiftsViewModel shifts, ReportsViewModel reports,
        UsersViewModel users, SettingsViewModel settings)
    {
        _session = session;
        _api = api;
        _hub = hub;
        _pos = pos;
        Branding = branding;

        Dashboard = dashboard;
        Orders = orders;
        Tables = tables;
        Kitchen = kitchen;
        Menu = menu;
        Inventory = inventory;
        Suppliers = suppliers;
        Purchasing = purchasing;
        Shifts = shifts;
        Reports = reports;
        Users = users;
        Settings = settings;

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _clock.Tick += (_, _) => ClockText = DateTime.Now.ToString("ddd, dd MMM yyyy  HH:mm");
        ClockText = DateTime.Now.ToString("ddd, dd MMM yyyy  HH:mm");

        _hub.ConnectionChanged += (live, status) => Ui.OnUiThread(() =>
        {
            IsLive = live;
            ConnectionStatus = status;
        });
        _session.SessionExpired += () => Ui.OnUiThread(() => SessionExpired?.Invoke());
        Tables.OpenOrderRequested += table => _ = OpenTableInPosAsync(table);
    }

    public BrandingService Branding { get; }
    public DashboardViewModel Dashboard { get; }
    public OrdersViewModel Orders { get; }
    public TablesViewModel Tables { get; }
    public KitchenViewModel Kitchen { get; }
    public MenuViewModel Menu { get; }
    public InventoryViewModel Inventory { get; }
    public SuppliersViewModel Suppliers { get; }
    public PurchaseOrdersViewModel Purchasing { get; }
    public ShiftsViewModel Shifts { get; }
    public ReportsViewModel Reports { get; }
    public UsersViewModel Users { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavItem> NavItems { get; } = new();

    [ObservableProperty] private object? _currentViewModel;
    [ObservableProperty] private NavItem? _selectedNav;
    [ObservableProperty] private string _currentTitle = "Dashboard";
    [ObservableProperty] private string _connectionStatus = "Connecting…";
    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private string _clockText = string.Empty;
    [ObservableProperty] private bool _isSidebarCollapsed;

    public GridLength SidebarWidth => IsSidebarCollapsed ? new GridLength(88) : new GridLength(238);
    public GridLength SidebarIconColumnWidth => IsSidebarCollapsed ? new GridLength(34) : new GridLength(26);
    public double SidebarIconFontSize => IsSidebarCollapsed ? 12 : 15;
    public double SidebarToggleFontSize => IsSidebarCollapsed ? 14 : 16;
    public string SidebarToggleGlyph => IsSidebarCollapsed ? "\uE76C" : "\uE700";
    public string SidebarToggleTooltip => IsSidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";
    public Visibility SidebarTextVisibility => IsSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarIconColumnWidth));
        OnPropertyChanged(nameof(SidebarIconFontSize));
        OnPropertyChanged(nameof(SidebarToggleFontSize));
        OnPropertyChanged(nameof(SidebarToggleGlyph));
        OnPropertyChanged(nameof(SidebarToggleTooltip));
        OnPropertyChanged(nameof(SidebarTextVisibility));
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    public string UserName => _session.User?.FullName ?? "User";
    public string UserRoleText => _session.User?.RoleName switch
    {
        UserRole.Administrator => "Administrator",
        UserRole.Manager => "Manager",
        UserRole.Cashier => "Cashier",
        UserRole.Kitchen => "Kitchen",
        _ => "Staff"
    };
    public string UserInitials
    {
        get
        {
            var words = UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => "CP",
                1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
                _ => (words[0][0].ToString() + words[^1][0]).ToUpperInvariant()
            };
        }
    }
    public bool IsManager => _session.IsManager;

    public event Action? LogoutRequested;
    public event Action? SessionExpired;

    public override async Task InitializeAsync()
    {
        BuildNavigation();
        _clock.Start();
        await Branding.LoadAsync();
        await _hub.ConnectAsync();
        if (NavItems.Count > 0)
        {
            SelectedNav = NavItems[0];
            await ActivateAsync(SelectedNav.ViewModel);
        }
    }

    private void BuildNavigation()
    {
        NavItems.Clear();
        void Add(string title, string glyph, object vm, params UserRole[] roles)
        {
            var role = _session.User?.RoleName ?? UserRole.Cashier;
            if (roles.Length == 0 || roles.Contains(role)) NavItems.Add(new NavItem(title, glyph, vm));
        }

        Add("Dashboard", "\uE80F", Dashboard, UserRole.Administrator, UserRole.Manager, UserRole.Cashier);
        Add("Point of Sale", "\uE7BF", _pos, UserRole.Administrator, UserRole.Manager, UserRole.Cashier);
        Add("Orders", "\uE8A5", Orders, UserRole.Administrator, UserRole.Manager, UserRole.Cashier);
        Add("Tables", "\uE8B7", Tables, UserRole.Administrator, UserRole.Manager, UserRole.Cashier);
        Add("Kitchen Display", "\uE9D9", Kitchen, UserRole.Administrator, UserRole.Manager,
            UserRole.Cashier, UserRole.Kitchen);
        Add("Menu & Products", "\uE8F1", Menu, UserRole.Administrator, UserRole.Manager);
        Add("Inventory", "\uE7B8", Inventory, UserRole.Administrator, UserRole.Manager);
        Add("Suppliers", "\uE716", Suppliers, UserRole.Administrator, UserRole.Manager);
        Add("Purchasing", "\uE7C1", Purchasing, UserRole.Administrator, UserRole.Manager);
        Add("Shifts", "\uE823", Shifts, UserRole.Administrator, UserRole.Manager, UserRole.Cashier);
        Add("Reports", "\uE9D2", Reports, UserRole.Administrator, UserRole.Manager);
        Add("Users", "\uE77B", Users, UserRole.Administrator);
        Add("Settings", "\uE713", Settings, UserRole.Administrator, UserRole.Manager);
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is null) return;
        CurrentTitle = value.Title;
        CurrentViewModel = value.ViewModel;
        _ = ActivateAsync(value.ViewModel);
    }

    private async Task ActivateAsync(object viewModel)
    {
        if (viewModel is IActivatable activatable)
        {
            try
            {
                await activatable.InitializeAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }

    public async Task OpenTableInPosAsync(TableDto table)
    {
        var item = NavItems.FirstOrDefault(n => n.ViewModel is PosViewModel);
        if (item is null) return;
        SelectedNav = item;
        await _pos.OpenTableAsync(table);
    }

    public async Task NavigateToAsync<T>() where T : class
    {
        var item = NavItems.FirstOrDefault(n => n.ViewModel is T);
        if (item is null) return;
        SelectedNav = item;
        await ActivateAsync(item.ViewModel);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!_session.IsAuthenticated) return;
        await _api.LogoutAsync();
        await _hub.DisconnectAsync();
        _clock.Stop();
        _session.Clear();
        LogoutRequested?.Invoke();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedNav is not null) await ActivateAsync(SelectedNav.ViewModel);
    }

    public void Stop()
    {
        _clock.Stop();
    }
}
