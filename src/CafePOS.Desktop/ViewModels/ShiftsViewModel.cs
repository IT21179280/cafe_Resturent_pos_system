using System.Collections.ObjectModel;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class ShiftsViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private readonly DialogService _dialogs;

    public ShiftsViewModel(ApiClient api, AuthSession session, DialogService dialogs)
    {
        _api = api;
        _session = session;
        _dialogs = dialogs;
    }

    public ObservableCollection<ShiftDto> Shifts { get; } = new();
    public List<string> ExpenseCategories { get; } = new() { "General", "Supplies", "Transport", "Maintenance", "Petty cash" };

    [ObservableProperty] private ShiftDto? _currentShift;
    [ObservableProperty] private ShiftDto? _selectedShift;
    [ObservableProperty] private string _openingCashText = "0.00";
    [ObservableProperty] private string _countedCashText = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _expenseDescription = string.Empty;
    [ObservableProperty] private string _expenseAmountText = string.Empty;
    [ObservableProperty] private string _expenseCategory = "General";
    [ObservableProperty] private string _reopenReason = string.Empty;

    public bool CanManage => _session.IsManager;
    public bool HasOpenShift => CurrentShift is { Status: ShiftStatus.Open };
    public bool HasNoOpenShift => CurrentShift is not { Status: ShiftStatus.Open };
    public string ShiftOwner => CurrentShift?.UserName ?? _session.User?.FullName ?? string.Empty;

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        CurrentShift = await _api.GetCurrentShiftAsync();
        var from = new DateTimeOffset(DateTime.Today.AddDays(-30), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today));
        var to = new DateTimeOffset(DateTime.Today.AddDays(1), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today));
        var shifts = await _api.GetShiftsAsync(from, to);
        Shifts.Clear();
        foreach (var shift in shifts.OrderByDescending(s => s.OpenedAt)) Shifts.Add(shift);
        SelectedShift = Shifts.FirstOrDefault(s => s.Id == CurrentShift?.Id) ?? Shifts.FirstOrDefault();
        OnPropertyChanged(nameof(HasOpenShift));
        OnPropertyChanged(nameof(HasNoOpenShift));
        OnPropertyChanged(nameof(ShiftOwner));
    }

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        if (!decimal.TryParse(OpeningCashText, out var cash) || cash < 0)
        {
            ErrorMessage = "Enter a valid opening cash amount.";
            return;
        }
        await RunAsync(async () =>
        {
            CurrentShift = await _api.OpenShiftAsync(cash);
            StatusMessage = "Shift opened. You can now take payments.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (CurrentShift is null) return;
        if (!decimal.TryParse(CountedCashText, out var counted) || counted < 0)
        {
            ErrorMessage = "Enter the counted cash amount.";
            return;
        }
        if (!_dialogs.Confirm($"Close the shift with LKR {counted:N2} counted?")) return;
        await RunAsync(async () =>
        {
            var closed = await _api.CloseShiftAsync(CurrentShift.Id, counted, Notes);
            CountedCashText = string.Empty;
            Notes = string.Empty;
            StatusMessage = closed.Difference == 0
                ? "Shift closed and balanced."
                : $"Shift closed with a difference of LKR {closed.Difference:N2}.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task ReopenShiftAsync()
    {
        if (SelectedShift is null) return;
        if (string.IsNullOrWhiteSpace(ReopenReason))
        {
            ErrorMessage = "Enter a reason to reopen the shift.";
            return;
        }
        await RunAsync(async () =>
        {
            await _api.ReopenShiftAsync(SelectedShift.Id, ReopenReason.Trim());
            ReopenReason = string.Empty;
            StatusMessage = "Shift reopened.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task AddExpenseAsync()
    {
        if (CurrentShift is null)
        {
            ErrorMessage = "Open a shift before recording an expense.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ExpenseDescription))
        {
            ErrorMessage = "Enter an expense description.";
            return;
        }
        if (!decimal.TryParse(ExpenseAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter a valid expense amount.";
            return;
        }
        await RunAsync(async () =>
        {
            await _api.AddShiftExpenseAsync(CurrentShift.Id, ExpenseDescription.Trim(), amount,
                ExpenseCategory);
            ExpenseDescription = string.Empty;
            ExpenseAmountText = string.Empty;
            StatusMessage = "Expense recorded.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task OpenNewShiftAsync()
    {
        OpeningCashText = "0.00";
        await OpenShiftAsync();
    }
}
