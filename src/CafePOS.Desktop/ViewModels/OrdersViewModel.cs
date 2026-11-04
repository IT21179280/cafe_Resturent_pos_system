using System.Collections.ObjectModel;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class OrdersViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private readonly DialogService _dialogs;
    private readonly ReceiptService _receipts;
    private AppSettingsDto? _appSettings;

    public OrdersViewModel(ApiClient api, AuthSession session, DialogService dialogs, ReceiptService receipts)
    {
        _api = api;
        _session = session;
        _dialogs = dialogs;
        _receipts = receipts;
    }

    public ObservableCollection<OrderSummaryDto> Orders { get; } = new();
    public List<string> StatusFilters { get; } = new()
    {
        "All", "Draft", "Pending", "Preparing", "Ready", "Served", "Completed", "Cancelled"
    };
    public List<string> TypeFilters { get; } = new() { "All", "DineIn", "Takeaway", "Delivery" };
    public List<string> StatusOptions { get; } = new()
    {
        nameof(OrderStatus.Pending), nameof(OrderStatus.Preparing), nameof(OrderStatus.Ready),
        nameof(OrderStatus.Served), nameof(OrderStatus.Completed)
    };

    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string _typeFilter = "All";
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private DateTime? _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime? _toDate = DateTime.Today;
    [ObservableProperty] private OrderSummaryDto? _selectedSummary;
    [ObservableProperty] private OrderDto? _selectedOrder;
    [ObservableProperty] private string _newStatus = nameof(OrderStatus.Preparing);
    [ObservableProperty] private string _cancelReason = string.Empty;
    [ObservableProperty] private string _refundReason = string.Empty;
    [ObservableProperty] private string _refundAmountText = string.Empty;
    [ObservableProperty] private PaymentDto? _selectedPayment;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _pageSize = 50;

    public bool CanRefund => _session.IsManager;
    public bool HasOrder => SelectedOrder is not null;
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public string PageLabel => $"Page {Page} of {Math.Max(1, TotalPages)} · {Total} orders";
    public string SelectedTotalLabel => SelectedOrder is null ? string.Empty : $"LKR {SelectedOrder.Total:N2}";

    public override async Task InitializeAsync()
    {
        try { _appSettings = await _api.GetSettingsAsync(); } catch { /* optional */ }
        await LoadAsync();
    }

    partial void OnSelectedSummaryChanged(OrderSummaryDto? value)
    {
        if (value is not null) _ = LoadSelectedAsync(value.Id);
    }

    partial void OnSelectedOrderChanged(OrderDto? value)
    {
        OnPropertyChanged(nameof(HasOrder));
        OnPropertyChanged(nameof(SelectedTotalLabel));
        SelectedPayment = value?.Payments.FirstOrDefault(p => p.Status != PaymentStatus.Refunded);
        NewStatus = value is null ? nameof(OrderStatus.Preparing) : NextStatus(value.Status);
    }

    partial void OnPageChanged(int value) => OnPropertyChanged(nameof(PageLabel));
    partial void OnTotalChanged(int value) => OnPropertyChanged(nameof(PageLabel));
    partial void OnPageSizeChanged(int value) => OnPropertyChanged(nameof(PageLabel));

    private static string NextStatus(OrderStatus status) => status switch
    {
        OrderStatus.Draft => nameof(OrderStatus.Pending),
        OrderStatus.Pending => nameof(OrderStatus.Preparing),
        OrderStatus.Preparing => nameof(OrderStatus.Ready),
        OrderStatus.Ready => nameof(OrderStatus.Served),
        OrderStatus.Served => nameof(OrderStatus.Completed),
        _ => nameof(OrderStatus.Completed)
    };

    [RelayCommand]
    private async Task RefreshAsync() { Page = 1; await LoadAsync(); }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (Page >= TotalPages) return;
        Page++;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (Page <= 1) return;
        Page--;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadOrderAsync()
    {
        if (SelectedSummary is null) return;
        await LoadSelectedAsync(SelectedSummary.Id);
    }

    private async Task LoadSelectedAsync(Guid id) => await RunAsync(async () =>
    {
        SelectedOrder = await _api.GetOrderAsync(id);
    });

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        OrderStatus? status = Enum.TryParse<OrderStatus>(StatusFilter, out var s) ? s : null;
        OrderType? type = Enum.TryParse<OrderType>(TypeFilter, out var t) ? t : null;
        DateTimeOffset? from = FromDate.HasValue
            ? new DateTimeOffset(FromDate.Value.Date, TimeZoneInfo.Local.GetUtcOffset(FromDate.Value.Date))
            : null;
        DateTimeOffset? to = ToDate.HasValue
            ? new DateTimeOffset(ToDate.Value.Date.AddDays(1), TimeZoneInfo.Local.GetUtcOffset(ToDate.Value.Date))
            : null;

        var result = await _api.GetOrdersAsync(status, type, from, to, Search, Page, PageSize);
        Total = result.Total;
        Orders.Clear();
        foreach (var order in result.Items) Orders.Add(order);
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageLabel));
        if (SelectedSummary is not null && Orders.All(o => o.Id != SelectedSummary.Id))
            SelectedOrder = null;
    });

    [RelayCommand]
    private async Task ApplyStatusAsync()
    {
        if (SelectedOrder is null) return;
        if (!Enum.TryParse<OrderStatus>(NewStatus, out var status)) return;
        await RunAsync(async () =>
        {
            SelectedOrder = await _api.UpdateOrderStatusAsync(SelectedOrder.Id, status);
            StatusMessage = $"{SelectedOrder.OrderNumber} is now {status}.";
            await ReloadListCoreAsync();
        });
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (SelectedOrder is null) return;
        await RunAsync(async () =>
        {
            SelectedOrder = await _api.UpdateOrderStatusAsync(SelectedOrder.Id, OrderStatus.Completed);
            StatusMessage = $"{SelectedOrder.OrderNumber} completed.";
            await ReloadListCoreAsync();
        });
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (SelectedOrder is null) return;
        if (string.IsNullOrWhiteSpace(CancelReason))
        {
            ErrorMessage = "Enter a cancellation reason.";
            return;
        }
        if (!_dialogs.Confirm($"Cancel order {SelectedOrder.OrderNumber}? This cannot be undone.")) return;
        await RunAsync(async () =>
        {
            SelectedOrder = await _api.CancelOrderAsync(SelectedOrder.Id, CancelReason.Trim());
            CancelReason = string.Empty;
            StatusMessage = "Order cancelled.";
            await ReloadListCoreAsync();
        });
    }

    [RelayCommand]
    private async Task RefundAsync()
    {
        if (SelectedOrder is null || SelectedPayment is null)
        {
            ErrorMessage = "Select a payment to refund.";
            return;
        }
        if (!decimal.TryParse(RefundAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter a valid refund amount.";
            return;
        }
        if (string.IsNullOrWhiteSpace(RefundReason))
        {
            ErrorMessage = "Enter a refund reason.";
            return;
        }
        if (!_dialogs.Confirm($"Refund LKR {amount:N2} for {SelectedOrder.OrderNumber}?")) return;

        await RunAsync(async () =>
        {
            SelectedOrder = await _api.RefundAsync(SelectedOrder.Id,
                new RefundRequest(SelectedPayment.Id, amount, RefundReason.Trim()));
            RefundAmountText = string.Empty;
            RefundReason = string.Empty;
            StatusMessage = "Refund recorded.";
            await ReloadListCoreAsync();
        });
    }

    [RelayCommand]
    private void Print()
    {
        if (SelectedOrder is null) return;
        var result = _receipts.Print(SelectedOrder, _appSettings, useDialog: true);
        if (!result.Printed) ErrorMessage = result.Error;
    }

    [RelayCommand]
    private void PrintKitchenTicket()
    {
        if (SelectedOrder is null) return;
        var result = _receipts.PrintKitchenTicket(SelectedOrder, _appSettings, useDialog: true);
        if (!result.Printed) ErrorMessage = result.Error;
    }

    private async Task ReloadListCoreAsync()
    {
        OrderStatus? status = Enum.TryParse<OrderStatus>(StatusFilter, out var s) ? s : null;
        OrderType? type = Enum.TryParse<OrderType>(TypeFilter, out var t) ? t : null;
        DateTimeOffset? from = FromDate.HasValue
            ? new DateTimeOffset(FromDate.Value.Date, TimeZoneInfo.Local.GetUtcOffset(FromDate.Value.Date))
            : null;
        DateTimeOffset? to = ToDate.HasValue
            ? new DateTimeOffset(ToDate.Value.Date.AddDays(1), TimeZoneInfo.Local.GetUtcOffset(ToDate.Value.Date))
            : null;
        var result = await _api.GetOrdersAsync(status, type, from, to, Search, Page, PageSize);
        Total = result.Total;
        Orders.Clear();
        foreach (var order in result.Items) Orders.Add(order);
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageLabel));
    }
}
