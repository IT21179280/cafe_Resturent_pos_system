using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public sealed record CheckoutPaymentEntry(PaymentMethod Method, decimal Amount, string? Reference)
{
    public string MethodLabel => Method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.BankTransfer => "Bank transfer",
        _ => Method.ToString()
    };
}

public partial class CheckoutViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ReceiptService _receipts;

    public CheckoutViewModel(ApiClient api, ReceiptService receipts, OrderDto order, AppSettingsDto? settings)
    {
        _api = api;
        _receipts = receipts;
        Order = order;
        Settings = settings;
        Methods = Enum.GetValues<PaymentMethod>();
        PrintKitchenTicket = receipts.HasKitchenPrinter;
        Payments.CollectionChanged += OnPaymentsChanged;
        AmountText = Math.Max(0, order.Total - order.PaidAmount).ToString("0.00");
        Recalculate();
        _ = LoadShiftAsync();
    }

    public OrderDto Order { get; }
    public AppSettingsDto? Settings { get; }
    public PaymentMethod[] Methods { get; }
    public ObservableCollection<CheckoutPaymentEntry> Payments { get; } = new();

    [ObservableProperty] private PaymentMethod _selectedMethod = PaymentMethod.Cash;
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private string _reference = string.Empty;
    [ObservableProperty] private bool _printReceipt = true;
    [ObservableProperty] private bool _printKitchenTicket;
    [ObservableProperty] private OrderDto? _completedOrder;
    [ObservableProperty] private decimal _remaining;
    [ObservableProperty] private decimal _changeDue;
    [ObservableProperty] private string _shiftText = "Checking shift…";

    public bool IsCashSelected
    {
        get => SelectedMethod == PaymentMethod.Cash;
        set { if (value) SelectedMethod = PaymentMethod.Cash; }
    }

    public bool IsCardSelected
    {
        get => SelectedMethod == PaymentMethod.Card;
        set { if (value) SelectedMethod = PaymentMethod.Card; }
    }

    public bool IsBankTransferSelected
    {
        get => SelectedMethod == PaymentMethod.BankTransfer;
        set { if (value) SelectedMethod = PaymentMethod.BankTransfer; }
    }

    partial void OnSelectedMethodChanged(PaymentMethod value)
    {
        OnPropertyChanged(nameof(IsCashSelected));
        OnPropertyChanged(nameof(IsCardSelected));
        OnPropertyChanged(nameof(IsBankTransferSelected));
    }

    public decimal Total => Order.Total;
    public decimal AlreadyPaid => Order.PaidAmount;
    public decimal NewPaymentsTotal => Payments.Sum(p => p.Method == PaymentMethod.Cash
        ? Math.Min(p.Amount, RemainingBefore(p))
        : p.Amount);

    public decimal RemainingBefore(CheckoutPaymentEntry entry)
    {
        var before = Order.Total - Order.PaidAmount -
                     Payments.TakeWhile(p => !ReferenceEquals(p, entry)).Sum(p => AppliedAmount(p, Order.Total - Order.PaidAmount));
        return Math.Max(0, before);
    }

    private static decimal AppliedAmount(CheckoutPaymentEntry entry, decimal remaining) =>
        entry.Method == PaymentMethod.Cash ? Math.Min(entry.Amount, remaining) : Math.Min(entry.Amount, remaining);

    public string TotalLabel => $"LKR {Total:N2}";
    public string RemainingLabel => $"LKR {Remaining:N2}";
    public bool IsFullyPaid => Remaining <= 0.01m;

    private void OnPaymentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Recalculate();

    private void Recalculate()
    {
        var remaining = Order.Total - Order.PaidAmount;
        var change = 0m;
        foreach (var entry in Payments)
        {
            var applied = entry.Method == PaymentMethod.Cash
                ? Math.Min(entry.Amount, remaining)
                : Math.Min(entry.Amount, remaining);
            if (entry.Method == PaymentMethod.Cash && entry.Amount > remaining) change += entry.Amount - remaining;
            remaining = MoneyRound(Math.Max(0, remaining - applied));
        }
        Remaining = MoneyRound(Math.Max(0, remaining));
        ChangeDue = MoneyRound(change);
        OnPropertyChanged(nameof(IsFullyPaid));
        OnPropertyChanged(nameof(RemainingLabel));
        OnPropertyChanged(nameof(NewPaymentsTotal));
    }

    private static decimal MoneyRound(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private async Task LoadShiftAsync()
    {
        try
        {
            var shift = await _api.GetCurrentShiftAsync();
            ShiftText = shift is { Status: ShiftStatus.Open }
                ? $"Shift open · opened {shift.OpenedAt.ToLocalTime():HH:mm}"
                : "No open shift. Payments will be rejected.";
        }
        catch
        {
            ShiftText = "Shift status unavailable.";
        }
    }

    [RelayCommand]
    private void AddPayment()
    {
        if (!decimal.TryParse(AmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter a valid payment amount.";
            return;
        }
        if (amount > Remaining + 0.01m && SelectedMethod != PaymentMethod.Cash)
        {
            ErrorMessage = "Card or transfer payments cannot exceed the remaining balance.";
            return;
        }
        if (Remaining <= 0)
        {
            ErrorMessage = "The bill is already fully paid.";
            return;
        }
        Payments.Add(new CheckoutPaymentEntry(SelectedMethod, amount,
            string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim()));
        Reference = string.Empty;
        AmountText = Remaining.ToString("0.00");
        ErrorMessage = null;
    }

    [RelayCommand]
    private void RemovePayment(CheckoutPaymentEntry? entry)
    {
        if (entry is not null) Payments.Remove(entry);
    }

    [RelayCommand]
    private void AddExactCash()
    {
        SelectedMethod = PaymentMethod.Cash;
        AmountText = Math.Max(0, Remaining).ToString("0.00");
    }

    [RelayCommand]
    private void AddCardFull()
    {
        SelectedMethod = PaymentMethod.Card;
        AmountText = Math.Max(0, Remaining).ToString("0.00");
    }

    [RelayCommand]
    public async Task CompleteAsync()
    {
        await RunAsync(async () =>
        {
            if (!IsFullyPaid)
                throw new ApiException($"Collect another LKR {Remaining:N2} before completing the sale.");
            if (Payments.Count == 0)
                throw new ApiException("Add at least one payment.");

            CompletedOrder = await _api.CheckoutAsync(Order.Id,
                new CheckoutRequest(Payments.Select(p => new AddPaymentRequest(p.Method, p.Amount, 0, p.Reference)).ToList(),
                    CompleteOrder: true));
            StatusMessage = "Payment completed.";
            PaymentCompleted?.Invoke();
        });
    }

    public event Action? PaymentCompleted;
}

public partial class ModifierOptionViewModel : ObservableObject
{
    public ModifierOptionViewModel(ProductModifierDto modifier, bool selected)
    {
        Id = modifier.Id;
        Name = modifier.Name;
        PriceDelta = modifier.PriceDelta;
        IsSelected = selected;
    }

    public Guid Id { get; }
    public string Name { get; }
    public decimal PriceDelta { get; }
    [ObservableProperty] private bool _isSelected;
    public string PriceLabel => PriceDelta == 0 ? "Included" : $"+ LKR {PriceDelta:N2}";
}

public partial class ModifierPickerViewModel : ObservableObject
{
    public ModifierPickerViewModel(string productName, IEnumerable<ProductModifierDto> modifiers, IEnumerable<Guid> selected)
    {
        ProductName = productName;
        var selectedIds = selected.ToHashSet();
        foreach (var modifier in modifiers.Where(m => m.IsActive).OrderBy(m => m.Name))
        {
            var option = new ModifierOptionViewModel(modifier, selectedIds.Contains(modifier.Id));
            option.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(ModifierOptionViewModel.IsSelected)) return;
                OnPropertyChanged(nameof(ExtraTotal));
                OnPropertyChanged(nameof(ExtraLabel));
            };
            Options.Add(option);
        }
    }

    public string ProductName { get; }
    public ObservableCollection<ModifierOptionViewModel> Options { get; } = new();
    public decimal ExtraTotal => Options.Where(o => o.IsSelected).Sum(o => o.PriceDelta);
    public string ExtraLabel => ExtraTotal == 0 ? "No extra charge" : $"+ LKR {ExtraTotal:N2}";
}
