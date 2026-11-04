using System.Collections.ObjectModel;
using System.Windows.Threading;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class KitchenViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly SignalRService _hub;
    private readonly ReceiptService _receipts;
    private readonly AuthSession _session;
    private readonly DispatcherTimer _autoRefresh;

    public KitchenViewModel(ApiClient api, SignalRService hub, ReceiptService receipts, AuthSession session)
    {
        _api = api;
        _hub = hub;
        _receipts = receipts;
        _session = session;

        _hub.OrderCreated += _ => ReloadFromEvent();
        _hub.OrderUpdated += _ => ReloadFromEvent();
        _hub.OrderStatusChanged += _ => ReloadFromEvent();

        _autoRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        _autoRefresh.Tick += (_, _) => { if (!IsBusy) _ = LoadAsync(); };
    }

    public ObservableCollection<OrderDto> Orders { get; } = new();
    public List<string> Stations { get; } = new()
    {
        "All", "Kitchen", "Bar", "Bakery", "Grill", "Dessert"
    };

    [ObservableProperty] private string _selectedStation = "All";
    [ObservableProperty] private OrderDto? _selectedOrder;

    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _preparingCount;
    [ObservableProperty] private int _readyCount;

    public string UserName => _session.User?.FullName ?? "Kitchen";

    public override async Task InitializeAsync()
    {
        if (_session.User?.RoleName == UserRole.Kitchen)
            await _hub.JoinKitchenAsync();
        if (!_autoRefresh.IsEnabled) _autoRefresh.Start();
        await LoadAsync();
    }

    private void ReloadFromEvent() => Ui.OnUiThread(() =>
    {
        if (!IsBusy) _ = LoadAsync();
    });

    partial void OnSelectedStationChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        PreparationStation? station = null;
        if (SelectedStation != "All" && Enum.TryParse<PreparationStation>(SelectedStation, out var parsed))
            station = parsed;

        var orders = await _api.GetKitchenOrdersAsync(station);
        var selectedId = SelectedOrder?.Id;
        Orders.Clear();
        foreach (var order in orders.OrderBy(o => o.PlacedAt)) Orders.Add(order);
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == selectedId);
        PendingCount = Orders.Count(o => o.Status == OrderStatus.Pending);
        PreparingCount = Orders.Count(o => o.Status == OrderStatus.Preparing);
        ReadyCount = Orders.Count(o => o.Status == OrderStatus.Ready);
    });

    [RelayCommand]
    private async Task StartAsync(OrderDto? order)
    {
        if (order is null) return;
        await ChangeStatusAsync(order, OrderStatus.Preparing);
    }

    [RelayCommand]
    private async Task ReadyAsync(OrderDto? order)
    {
        if (order is null) return;
        await ChangeStatusAsync(order, OrderStatus.Ready);
    }

    [RelayCommand]
    private async Task ServedAsync(OrderDto? order)
    {
        if (order is null) return;
        await ChangeStatusAsync(order, OrderStatus.Served);
    }

    private async Task ChangeStatusAsync(OrderDto order, OrderStatus status)
    {
        await RunAsync(async () =>
        {
            var updated = await _api.UpdateOrderStatusAsync(order.Id, status);
            StatusMessage = $"{updated.OrderNumber} moved to {status}.";
            await LoadCoreAsync();
        });
    }

    private async Task LoadCoreAsync()
    {
        var orders = await _api.GetKitchenOrdersAsync(null);
        Orders.Clear();
        foreach (var order in orders.OrderBy(o => o.PlacedAt)) Orders.Add(order);
        PendingCount = Orders.Count(o => o.Status == OrderStatus.Pending);
        PreparingCount = Orders.Count(o => o.Status == OrderStatus.Preparing);
        ReadyCount = Orders.Count(o => o.Status == OrderStatus.Ready);
    }

    [RelayCommand]
    private void PrintTicket(OrderDto? order)
    {
        if (order is null) return;
        var result = _receipts.PrintKitchenTicket(order, null, useDialog: true);
        if (!result.Printed) ErrorMessage = result.Error;
    }
}
