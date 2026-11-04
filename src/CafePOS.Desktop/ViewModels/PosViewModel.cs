using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using CafePOS.Core.BusinessRules;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CafePOS.Desktop.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class CartLineViewModel : ObservableObject
{
    private readonly Action _changed;

    public CartLineViewModel(ProductDto product, Action changed) : this(product.Id, product.Name,
        product.Price, product.TaxRate, product.PreparationStation, product.Modifiers, changed)
    {
    }

    public CartLineViewModel(Guid? productId, string name, decimal price, decimal taxRate,
        PreparationStation station, List<ProductModifierDto> availableModifiers, Action changed)
    {
        ProductId = productId;
        ProductName = name;
        UnitPrice = price;
        TaxRate = taxRate;
        Station = station;
        AvailableModifiers = availableModifiers;
        _changed = changed;
    }

    public Guid? ProductId { get; }
    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public decimal TaxRate { get; }
    public PreparationStation Station { get; }
    public List<ProductModifierDto> AvailableModifiers { get; }
    public List<Guid> ModifierIds { get; } = new();
    public List<OrderItemModifierDto> Modifiers { get; } = new();

    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private decimal _lineTotal;
    [ObservableProperty] private decimal _taxAmount;

    public bool HasAvailableModifiers => AvailableModifiers.Count > 0;
    public string ModifierSummary => Modifiers.Count == 0 ? string.Empty : string.Join(", ", Modifiers.Select(m => m.Name));
    public string PriceLabel => $"LKR {UnitPrice:N2}";
    public string QuantityLabel => Quantity.ToString("0.##");
    public decimal Gross => Money.Round(UnitPrice * Quantity + Modifiers.Sum(m => m.Price));
    public decimal Net => Math.Max(0, Money.Round(Gross - Discount));

    partial void OnQuantityChanged(decimal value)
    {
        _changed();
        OnPropertyChanged(nameof(QuantityLabel));
        OnPropertyChanged(nameof(Gross));
        OnPropertyChanged(nameof(Net));
    }

    partial void OnDiscountChanged(decimal value)
    {
        _changed();
        OnPropertyChanged(nameof(Net));
    }

    partial void OnNotesChanged(string? value) => _changed();

    public void SetCalculated(decimal total, decimal tax)
    {
        LineTotal = total;
        TaxAmount = tax;
    }

    public void NotifyModifiersChanged()
    {
        OnPropertyChanged(nameof(ModifierSummary));
        OnPropertyChanged(nameof(Gross));
        OnPropertyChanged(nameof(Net));
        _changed();
    }
}

public sealed record CategoryOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record BrowseOption(bool IsProductView, string Name)
{
    public override string ToString() => Name;
}

public sealed record CategoryCardViewModel(Guid? Id, string Name, string? ImageUrl, int ProductCount)
{
    public string ProductCountLabel => ProductCount == 1 ? "1 product" : $"{ProductCount} products";
}

public partial class PosViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private readonly DialogService _dialogs;
    private readonly ReceiptService _receipts;
    private readonly AppSettingsStore _settings;
    private readonly List<ProductDto> _allProducts = new();
    private AppSettingsDto? _appSettings;
    private string? _clientOrderId;
    private bool _loaded;

    public PosViewModel(ApiClient api, AuthSession session, DialogService dialogs, ReceiptService receipts,
        AppSettingsStore settings)
    {
        _api = api;
        _session = session;
        _dialogs = dialogs;
        _receipts = receipts;
        _settings = settings;

        Cart.CollectionChanged += OnCartChanged;
        SelectedBrowseOption = BrowseOptions[0];
    }

    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<CartLineViewModel> Cart { get; } = new();
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = new();
    public ObservableCollection<CategoryCardViewModel> CategoryCards { get; } = new();
    public ObservableCollection<TableDto> Tables { get; } = new();
    public OrderType[] OrderTypes { get; } = Enum.GetValues<OrderType>();
    public DiscountType[] DiscountTypes { get; } = Enum.GetValues<DiscountType>();
    public List<BrowseOption> BrowseOptions { get; } = new()
    {
        new(false, "Categories"),
        new(true, "Products")
    };

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private BrowseOption? _selectedBrowseOption;
    [ObservableProperty] private CategoryOption? _selectedCategory;
    [ObservableProperty] private OrderType _selectedOrderType = OrderType.DineIn;
    [ObservableProperty] private TableDto? _selectedTable;
    [ObservableProperty] private DiscountType _selectedDiscountType = DiscountType.None;
    [ObservableProperty] private string _discountValueText = "0";
    [ObservableProperty] private string _serviceChargeRateText = "0";
    [ObservableProperty] private bool _sendToKitchen = true;
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _customerPhone = string.Empty;
    [ObservableProperty] private string _deliveryAddress = string.Empty;
    [ObservableProperty] private string _orderNotes = string.Empty;
    [ObservableProperty] private OrderDto? _currentOrder;
    [ObservableProperty] private decimal _subtotal;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _serviceCharge;
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private string _shiftWarning = string.Empty;

    public bool IsDineIn => SelectedOrderType == OrderType.DineIn;
    public bool IsDelivery => SelectedOrderType == OrderType.Delivery;
    public bool IsProductView => SelectedBrowseOption?.IsProductView == true;
    public bool IsCategoryView => !IsProductView;
    public bool ShowCustomer => SelectedOrderType is OrderType.Takeaway or OrderType.Delivery;
    public bool HasCart => Cart.Count > 0;
    public bool HasCurrentOrder => CurrentOrder is not null;
    public string OrderLabel => CurrentOrder?.OrderNumber ?? "New order";
    public string OrderStatusLabel => CurrentOrder is null ? "Not saved" : CurrentOrder.Status.ToString();
    public int ItemCount => Cart.Sum(c => (int)Math.Ceiling(c.Quantity));
    public string TotalLabel => $"LKR {Total:N2}";
    public bool HasShift => string.IsNullOrEmpty(ShiftWarning);

    public override async Task InitializeAsync() => await LoadAsync();

    public async Task OpenTableAsync(TableDto table)
    {
        await LoadAsync();
        SelectedOrderType = OrderType.DineIn;
        SelectedTable = Tables.FirstOrDefault(t => t.Id == table.Id) ?? table;
        await RunAsync(async () =>
        {
            var order = await _api.GetActiveOrderForTableAsync(table.Id);
            if (order is null)
            {
                ResetCart(keepTable: true);
            }
            else
            {
                LoadOrder(order);
                StatusMessage = $"Editing {order.OrderNumber}.";
            }
            await ReloadTablesCoreAsync();
        });
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        _appSettings = await _api.GetSettingsAsync();
        if (!_loaded)
        {
            ServiceChargeRateText = _appSettings.ServiceChargeRate.ToString("0.##");
            _clientOrderId = Guid.NewGuid().ToString();
        }

        var categories = await _api.GetCategoriesAsync(includeInactive: false);
        var selectedCategoryId = SelectedCategory?.Id;
        CategoryOptions.Clear();
        CategoryOptions.Add(new CategoryOption(null, "All items"));
        foreach (var category in categories.OrderBy(c => c.DisplayOrder)) CategoryOptions.Add(new CategoryOption(category.Id, category.Name));
        SelectedCategory = CategoryOptions.FirstOrDefault(c => c.Id == selectedCategoryId) ?? CategoryOptions[0];

        var products = await _api.GetProductsAsync(availableOnly: false, pageSize: 500);
        _allProducts.Clear();
        _allProducts.AddRange(products.Items);
        BuildCategoryCards(categories);
        ApplyFilter();

        await ReloadTablesCoreAsync();

        var shift = await _api.GetCurrentShiftAsync();
        ShiftWarning = shift is { Status: ShiftStatus.Open }
            ? string.Empty
            : "No open shift. Open a shift from the Shifts screen before taking payments.";
        OnPropertyChanged(nameof(HasShift));
        _loaded = true;
    });

    private async Task ReloadTablesCoreAsync()
    {
        var tables = await _api.GetTablesAsync();
        Tables.Clear();
        foreach (var table in tables.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Number)) Tables.Add(table);
        if (SelectedTable is not null)
            SelectedTable = Tables.FirstOrDefault(t => t.Id == SelectedTable.Id) ?? SelectedTable;
    }

    partial void OnSearchTextChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsProductView)
            SelectedBrowseOption = BrowseOptions.First(o => o.IsProductView);
        ApplyFilter();
    }

    partial void OnSelectedBrowseOptionChanged(BrowseOption? value)
    {
        OnPropertyChanged(nameof(IsProductView));
        OnPropertyChanged(nameof(IsCategoryView));
    }

    partial void OnSelectedCategoryChanged(CategoryOption? value) => ApplyFilter();

    private void BuildCategoryCards(IEnumerable<CategoryDto> categories)
    {
        CategoryCards.Clear();
        CategoryCards.Add(new CategoryCardViewModel(null, "All products", null, _allProducts.Count));
        foreach (var category in categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name))
            CategoryCards.Add(new CategoryCardViewModel(category.Id, category.Name, category.ImageUrl,
                category.ProductCount));
    }

    [RelayCommand]
    private void OpenCategory(CategoryCardViewModel? card)
    {
        if (card is null) return;
        SelectedCategory = CategoryOptions.FirstOrDefault(c => c.Id == card.Id) ?? CategoryOptions.FirstOrDefault();
        SelectedBrowseOption = BrowseOptions.First(o => o.IsProductView);
    }

    [RelayCommand]
    private void ShowCategories() => SelectedBrowseOption = BrowseOptions[0];

    private void ApplyFilter()
    {
        var search = SearchText?.Trim() ?? string.Empty;
        var categoryId = SelectedCategory?.Id;
        var filtered = _allProducts.Where(p =>
            (categoryId is null || p.CategoryId == categoryId) &&
            (search.Length == 0 || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             p.Sku.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             (p.Barcode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)));
        Products.Clear();
        foreach (var product in filtered.OrderBy(p => p.Name)) Products.Add(product);
    }

    partial void OnSelectedOrderTypeChanged(OrderType value)
    {
        OnPropertyChanged(nameof(IsDineIn));
        OnPropertyChanged(nameof(IsDelivery));
        OnPropertyChanged(nameof(ShowCustomer));
        if (value != OrderType.DineIn) SelectedTable = null;
    }

    partial void OnSelectedDiscountTypeChanged(DiscountType value) => Recalculate();
    partial void OnDiscountValueTextChanged(string value) => Recalculate();
    partial void OnServiceChargeRateTextChanged(string value) => Recalculate();

    partial void OnCurrentOrderChanged(OrderDto? value)
    {
        OnPropertyChanged(nameof(HasCurrentOrder));
        OnPropertyChanged(nameof(OrderLabel));
        OnPropertyChanged(nameof(OrderStatusLabel));
    }

    partial void OnTotalChanged(decimal value) => OnPropertyChanged(nameof(TotalLabel));

    private void OnCartChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasCart));
        OnPropertyChanged(nameof(ItemCount));
        Recalculate();
    }

    private void Recalculate()
    {
        var lines = Cart.Select(c => new PricingLine(c.UnitPrice, c.Quantity, c.Discount, c.TaxRate,
            c.Modifiers.Sum(m => m.Price))).ToList();
        var discountValue = decimal.TryParse(DiscountValueText, out var d) ? d : 0;
        var serviceRate = decimal.TryParse(ServiceChargeRateText, out var s) ? s : 0;
        var totals = OrderPricing.Calculate(lines, SelectedDiscountType, discountValue, serviceRate);
        Subtotal = totals.Subtotal;
        DiscountAmount = totals.DiscountAmount;
        TaxAmount = totals.TaxAmount;
        ServiceCharge = totals.ServiceCharge;
        Total = totals.Total;
        for (var i = 0; i < Cart.Count && i < totals.Lines.Count; i++)
            Cart[i].SetCalculated(totals.Lines[i].Total, totals.Lines[i].Tax);
        OnPropertyChanged(nameof(ItemCount));
    }

    [RelayCommand]
    private void AddProduct(ProductDto? product)
    {
        if (product is null || !product.IsAvailable) return;
        var line = new CartLineViewModel(product, Recalculate);
        if (product.Modifiers.Count > 0)
        {
            var picker = new ModifierPickerViewModel(product.Name, product.Modifiers, Array.Empty<Guid>());
            var window = new ModifierPickerWindow { DataContext = picker, Owner = Application.Current.MainWindow };
            if (window.ShowDialog() != true) return;
            foreach (var option in picker.Options.Where(o => o.IsSelected))
            {
                line.ModifierIds.Add(option.Id);
                line.Modifiers.Add(new OrderItemModifierDto(option.Id, option.Name, option.PriceDelta));
            }
        }
        Cart.Add(line);
        StatusMessage = $"{product.Name} added.";
        Recalculate();
    }

    [RelayCommand]
    private void IncreaseLine(CartLineViewModel? line)
    {
        if (line is null) return;
        line.Quantity += 1;
    }

    [RelayCommand]
    private void DecreaseLine(CartLineViewModel? line)
    {
        if (line is null) return;
        if (line.Quantity <= 1) Cart.Remove(line);
        else line.Quantity -= 1;
    }

    [RelayCommand]
    private void RemoveLine(CartLineViewModel? line)
    {
        if (line is not null) Cart.Remove(line);
    }

    [RelayCommand]
    private void EditModifiers(CartLineViewModel? line)
    {
        if (line is null || line.AvailableModifiers.Count == 0) return;
        var picker = new ModifierPickerViewModel(line.ProductName, line.AvailableModifiers, line.ModifierIds);
        var window = new ModifierPickerWindow { DataContext = picker, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() != true) return;

        line.ModifierIds.Clear();
        line.Modifiers.Clear();
        foreach (var option in picker.Options.Where(o => o.IsSelected))
        {
            line.ModifierIds.Add(option.Id);
            line.Modifiers.Add(new OrderItemModifierDto(option.Id, option.Name, option.PriceDelta));
        }
        line.NotifyModifiersChanged();
        Recalculate();
    }

    [RelayCommand]
    private void ClearCart()
    {
        if (Cart.Count > 0 && !_dialogs.Confirm("Clear all items from this order?")) return;
        ResetCart(keepTable: false);
        StatusMessage = "Cart cleared.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            var order = await PersistOrderAsync(sendToKitchen: false);
            StatusMessage = $"Order {order.OrderNumber} saved.";
            await ReloadTablesCoreAsync();
        });
    }

    [RelayCommand]
    private async Task SendOrderAsync()
    {
        await RunAsync(async () =>
        {
            var order = await PersistOrderAsync(sendToKitchen: true);
            StatusMessage = $"Order {order.OrderNumber} sent to the kitchen.";
            ResetCart(keepTable: false);
            await ReloadTablesCoreAsync();
        });
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        await RunAsync(async () =>
        {
            var shift = await _api.GetCurrentShiftAsync();
            if (shift is not { Status: ShiftStatus.Open })
                throw new ApiException("Open a shift from the Shifts screen before taking payments.");

            var order = await PersistOrderAsync(sendToKitchen: true);
            var checkout = new CheckoutViewModel(_api, _receipts, order, _appSettings);
            var window = new CheckoutWindow { DataContext = checkout, Owner = Application.Current.MainWindow };
            if (window.ShowDialog() != true || checkout.CompletedOrder is null)
            {
                StatusMessage = "Payment cancelled. The order remains open.";
                await ReloadTablesCoreAsync();
                return;
            }

            var completed = checkout.CompletedOrder;
            StatusMessage = $"Payment complete. Change due LKR {completed.ChangeDue:N2}.";
            if (checkout.PrintReceipt)
            {
                var result = _receipts.Print(completed, _appSettings, useDialog: !_settings.UseThermalPrinter);
                if (!result.Printed) ErrorMessage = result.Error;
            }
            if (checkout.PrintKitchenTicket)
            {
                var ticket = _receipts.PrintKitchenTicket(completed, _appSettings,
                    useDialog: !_settings.UseThermalPrinter && !_settings.UseKitchenPrinter);
                if (!ticket.Printed) ErrorMessage = ticket.Error;
            }
            ResetCart(keepTable: false);
            await ReloadTablesCoreAsync();
        });
    }

    private async Task<OrderDto> PersistOrderAsync(bool sendToKitchen)
    {
        if (Cart.Count == 0) throw new ApiException("Add at least one item to the order.");
        if (SelectedOrderType == OrderType.DineIn && SelectedTable is null)
            throw new ApiException("Select a table for a dine-in order.");
        if (SelectedOrderType == OrderType.Delivery && string.IsNullOrWhiteSpace(CustomerName))
            throw new ApiException("Enter the customer name for a delivery order.");
        if (SelectedOrderType == OrderType.Delivery && string.IsNullOrWhiteSpace(DeliveryAddress))
            throw new ApiException("Enter the delivery address for a delivery order.");

        var items = Cart.Select(line => new OrderItemRequest(
            line.ProductId, line.ProductName, line.UnitPrice, line.Quantity, line.Discount, line.TaxRate,
            line.Notes, line.ModifierIds.ToList(),
            line.Modifiers.Select(m => new OrderItemModifierRequest(m.Name, m.Price)).ToList(),
            line.Station)).ToList();

        var discountValue = decimal.TryParse(DiscountValueText, out var d) ? d : 0;
        var serviceRate = decimal.TryParse(ServiceChargeRateText, out var s) ? (decimal?)s : null;
        var tableId = SelectedOrderType == OrderType.DineIn ? SelectedTable?.Id : null;

        if (CurrentOrder is not null && CurrentOrder.Status is OrderStatus.Draft or OrderStatus.Pending)
        {
            var updated = await _api.UpdateOrderAsync(CurrentOrder.Id, new UpdateOrderRequest(
                SelectedOrderType, tableId, Trim(CustomerName), Trim(CustomerPhone), Trim(DeliveryAddress),
                Trim(OrderNotes), items, SelectedDiscountType, discountValue, null, serviceRate, sendToKitchen));
            CurrentOrder = updated;
            return updated;
        }

        _clientOrderId ??= Guid.NewGuid().ToString();
        var created = await _api.CreateOrderAsync(new CreateOrderRequest(
            SelectedOrderType, tableId, Trim(CustomerName), Trim(CustomerPhone), Trim(DeliveryAddress),
            Trim(OrderNotes), items, SelectedDiscountType, discountValue, null, serviceRate, _clientOrderId,
            sendToKitchen));
        CurrentOrder = created;
        _clientOrderId = null;
        return created;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void LoadOrder(OrderDto order)
    {
        CurrentOrder = order;
        Cart.Clear();
        SelectedOrderType = order.Type;
        SelectedTable = Tables.FirstOrDefault(t => t.Id == order.TableId);
        CustomerName = order.CustomerName ?? string.Empty;
        CustomerPhone = order.CustomerPhone ?? string.Empty;
        DeliveryAddress = order.DeliveryAddress ?? string.Empty;
        OrderNotes = order.Notes ?? string.Empty;
        SelectedDiscountType = order.DiscountType;
        DiscountValueText = order.DiscountValue.ToString("0.##");
        var taxable = order.Subtotal - order.DiscountAmount;
        ServiceChargeRateText = taxable > 0 ? (order.ServiceCharge / taxable * 100).ToString("0.##") : "0";

        foreach (var item in order.Items)
        {
            var product = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
            var line = new CartLineViewModel(item.ProductId, item.ProductName, item.UnitPrice, item.TaxRate,
                item.PreparationStation, product?.Modifiers ?? new List<ProductModifierDto>(), Recalculate)
            {
                Quantity = item.Quantity,
                Discount = item.DiscountAmount,
                Notes = item.Notes
            };
            foreach (var modifier in item.Modifiers)
            {
                var available = product?.Modifiers.FirstOrDefault(m =>
                    string.Equals(m.Name, modifier.Name, StringComparison.OrdinalIgnoreCase));
                if (available is not null) line.ModifierIds.Add(available.Id);
                line.Modifiers.Add(modifier);
            }
            Cart.Add(line);
        }
        Recalculate();
        OnPropertyChanged(nameof(HasCurrentOrder));
        OnPropertyChanged(nameof(OrderLabel));
        OnPropertyChanged(nameof(OrderStatusLabel));
    }

    private void ResetCart(bool keepTable)
    {
        Cart.Clear();
        CurrentOrder = null;
        _clientOrderId = Guid.NewGuid().ToString();
        CustomerName = string.Empty;
        CustomerPhone = string.Empty;
        DeliveryAddress = string.Empty;
        OrderNotes = string.Empty;
        SelectedDiscountType = DiscountType.None;
        DiscountValueText = "0";
        if (!keepTable) SelectedTable = null;
        Recalculate();
        OnPropertyChanged(nameof(HasCurrentOrder));
        OnPropertyChanged(nameof(OrderLabel));
        OnPropertyChanged(nameof(OrderStatusLabel));
    }
}
