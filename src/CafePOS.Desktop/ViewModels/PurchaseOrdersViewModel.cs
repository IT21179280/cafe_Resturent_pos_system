using System.Collections.ObjectModel;
using System.Windows;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CafePOS.Desktop.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class PurchaseOrdersViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly DialogService _dialogs;

    public PurchaseOrdersViewModel(ApiClient api, DialogService dialogs)
    {
        _api = api;
        _dialogs = dialogs;
    }

    public ObservableCollection<PurchaseOrderDto> Orders { get; } = new();
    public List<string> StatusFilters { get; } = new()
    {
        "All", "Draft", "Ordered", "PartiallyReceived", "Received", "Cancelled"
    };

    [ObservableProperty] private PurchaseOrderDto? _selectedOrder;
    [ObservableProperty] private string _statusFilter = "All";
    [ObservableProperty] private string _cancelReason = string.Empty;

    public bool HasOrder => SelectedOrder is not null;
    public bool CanReceive => SelectedOrder is { Status: PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived };
    public bool CanCancel => SelectedOrder is { Status: PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived };

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        PurchaseOrderStatus? status = Enum.TryParse<PurchaseOrderStatus>(StatusFilter, out var parsed) ? parsed : null;
        var selectedId = SelectedOrder?.Id;
        var orders = await _api.GetPurchaseOrdersAsync(status);
        Orders.Clear();
        foreach (var order in orders.OrderByDescending(o => o.OrderedAt)) Orders.Add(order);
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == selectedId);
    });

    partial void OnSelectedOrderChanged(PurchaseOrderDto? value)
    {
        OnPropertyChanged(nameof(HasOrder));
        OnPropertyChanged(nameof(CanReceive));
        OnPropertyChanged(nameof(CanCancel));
    }

    partial void OnStatusFilterChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task NewOrderAsync()
    {
        var editor = new PurchaseOrderEditViewModel(_api);
        var window = new PurchaseOrderEditWindow { DataContext = editor, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            await RunAsync(LoadCoreAsync);
            StatusMessage = $"Purchase order {editor.OrderNumber} created.";
        }
    }

    [RelayCommand]
    private async Task ReceiveAsync()
    {
        if (SelectedOrder is null) return;
        var editor = new ReceivePurchaseOrderViewModel(_api, SelectedOrder);
        var window = new ReceivePurchaseOrderWindow { DataContext = editor, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            await RunAsync(LoadCoreAsync);
            StatusMessage = "Stock received and inventory updated.";
        }
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
        if (!_dialogs.Confirm($"Cancel purchase order {SelectedOrder.Number}?")) return;
        await RunAsync(async () =>
        {
            await _api.CancelPurchaseOrderAsync(SelectedOrder.Id, CancelReason.Trim());
            CancelReason = string.Empty;
            await LoadCoreAsync();
            StatusMessage = "Purchase order cancelled.";
        });
    }

    private async Task LoadCoreAsync()
    {
        PurchaseOrderStatus? status = Enum.TryParse<PurchaseOrderStatus>(StatusFilter, out var parsed) ? parsed : null;
        var selectedId = SelectedOrder?.Id;
        var orders = await _api.GetPurchaseOrdersAsync(status);
        Orders.Clear();
        foreach (var order in orders.OrderByDescending(o => o.OrderedAt)) Orders.Add(order);
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == selectedId);
    }
}

public partial class PurchaseOrderLineViewModel : ObservableObject
{
    private readonly Action _changed;
    public PurchaseOrderLineViewModel(List<IngredientDto> ingredients, Action changed)
    {
        Ingredients = ingredients;
        _changed = changed;
        SelectedIngredient = ingredients.FirstOrDefault();
        if (SelectedIngredient is not null) UnitCostText = SelectedIngredient.CostPerUnit.ToString("0.00");
    }

    public List<IngredientDto> Ingredients { get; }
    [ObservableProperty] private IngredientDto? _selectedIngredient;
    [ObservableProperty] private string _quantityText = "1";
    [ObservableProperty] private string _unitCostText = "0.00";

    public string Unit => SelectedIngredient?.Unit ?? string.Empty;
    public decimal Quantity => decimal.TryParse(QuantityText, out var q) ? q : 0;
    public decimal UnitCost => decimal.TryParse(UnitCostText, out var c) ? c : 0;
    public decimal LineTotal => Math.Round(Quantity * UnitCost, 2);

    partial void OnSelectedIngredientChanged(IngredientDto? value)
    {
        if (value is not null) UnitCostText = value.CostPerUnit.ToString("0.00");
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(LineTotal));
        _changed();
    }

    partial void OnQuantityTextChanged(string value)
    {
        OnPropertyChanged(nameof(LineTotal));
        _changed();
    }

    partial void OnUnitCostTextChanged(string value)
    {
        OnPropertyChanged(nameof(LineTotal));
        _changed();
    }
}

public partial class PurchaseOrderEditViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    public PurchaseOrderEditViewModel(ApiClient api)
    {
        _api = api;
        Lines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Total));
        _ = LoadAsync();
    }

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<IngredientDto> Ingredients { get; } = new();
    public ObservableCollection<PurchaseOrderLineViewModel> Lines { get; } = new();

    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _orderNumber = string.Empty;

    public decimal Total => Lines.Sum(l => l.LineTotal);

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var suppliers = await _api.GetSuppliersAsync();
        Suppliers.Clear();
        foreach (var supplier in suppliers.OrderBy(s => s.Name)) Suppliers.Add(supplier);
        SelectedSupplier = Suppliers.FirstOrDefault();
        var ingredients = await _api.GetIngredientsAsync();
        Ingredients.Clear();
        foreach (var ingredient in ingredients.OrderBy(i => i.Name)) Ingredients.Add(ingredient);
        AddLine();
    });

    [RelayCommand]
    private void AddLine()
    {
        if (Ingredients.Count == 0) return;
        Lines.Add(new PurchaseOrderLineViewModel(Ingredients.ToList(), () => OnPropertyChanged(nameof(Total))));
    }

    [RelayCommand]
    private void RemoveLine(PurchaseOrderLineViewModel? line)
    {
        if (line is not null) Lines.Remove(line);
    }

    public Task SaveAsync() => RunAsync(async () =>
    {
        if (SelectedSupplier is null) throw new ApiException("Select a supplier.");
        var items = new List<CreatePurchaseOrderItemRequest>();
        foreach (var line in Lines)
        {
            if (line.SelectedIngredient is null) continue;
            if (line.Quantity <= 0) throw new ApiException("Every line needs a quantity greater than zero.");
            if (line.UnitCost < 0) throw new ApiException("Unit cost cannot be negative.");
            items.Add(new CreatePurchaseOrderItemRequest(line.SelectedIngredient.Id, line.Quantity, line.UnitCost));
        }
        if (items.Count == 0) throw new ApiException("Add at least one ingredient.");
        var created = await _api.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest(SelectedSupplier.Id,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(), items));
        OrderNumber = created.Number;
        StatusMessage = $"Purchase order {created.Number} created.";
    });
}

public partial class ReceiveLineViewModel : ObservableObject
{
    public ReceiveLineViewModel(PurchaseOrderItemDto item)
    {
        Item = item;
        QuantityText = (item.Quantity - item.ReceivedQuantity).ToString("0.###");
    }

    public PurchaseOrderItemDto Item { get; }
    public string IngredientName => Item.IngredientName;
    public string Unit => Item.Unit;
    public decimal Outstanding => Math.Max(0, Item.Quantity - Item.ReceivedQuantity);
    [ObservableProperty] private string _quantityText = "0";
}

public partial class ReceivePurchaseOrderViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    public ReceivePurchaseOrderViewModel(ApiClient api, PurchaseOrderDto order)
    {
        _api = api;
        Order = order;
        foreach (var item in order.Items.Where(i => i.Quantity > i.ReceivedQuantity))
            Lines.Add(new ReceiveLineViewModel(item));
    }

    public PurchaseOrderDto Order { get; }
    public ObservableCollection<ReceiveLineViewModel> Lines { get; } = new();
    public string Heading => $"Receive stock · {Order.Number}";

    public Task SaveAsync() => RunAsync(async () =>
    {
        var items = new List<ReceivePurchaseOrderItemRequest>();
        foreach (var line in Lines)
        {
            if (!decimal.TryParse(line.QuantityText, out var quantity)) continue;
            if (quantity <= 0) continue;
            if (quantity > line.Outstanding)
                throw new ApiException($"Cannot receive more than the outstanding quantity for {line.IngredientName}.");
            items.Add(new ReceivePurchaseOrderItemRequest(line.Item.Id, quantity));
        }
        if (items.Count == 0) throw new ApiException("Enter at least one received quantity.");
        await _api.ReceivePurchaseOrderAsync(Order.Id, new ReceivePurchaseOrderRequest(items));
        StatusMessage = "Stock received.";
    });
}
