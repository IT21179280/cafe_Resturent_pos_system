using System.Collections.ObjectModel;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class TablesViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly AuthSession _session;
    private readonly DialogService _dialogs;

    public TablesViewModel(ApiClient api, AuthSession session, DialogService dialogs)
    {
        _api = api;
        _session = session;
        _dialogs = dialogs;
    }

    public ObservableCollection<TableDto> Tables { get; } = new();

    [ObservableProperty] private TableDto? _selectedTable;
    [ObservableProperty] private OrderDto? _selectedOrder;
    [ObservableProperty] private string _tableNumber = string.Empty;
    [ObservableProperty] private int _tableCapacity = 4;
    [ObservableProperty] private string _tableArea = string.Empty;
    [ObservableProperty] private int _tableDisplayOrder;
    [ObservableProperty] private string _editorTitle = "New table";

    public bool CanManage => _session.IsManager;
    public bool HasSelection => SelectedTable is not null;
    public bool HasOrder => SelectedOrder is not null;
    public int AvailableCount => Tables.Count(t => t.Status == TableStatus.Available);
    public int OccupiedCount => Tables.Count(t => t.Status == TableStatus.Occupied);
    public int ReservedCount => Tables.Count(t => t.Status == TableStatus.Reserved);

    public event Action<TableDto>? OpenOrderRequested;

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var tables = await _api.GetTablesAsync();
        var selectedId = SelectedTable?.Id;
        Tables.Clear();
        foreach (var table in tables.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Number)) Tables.Add(table);
        SelectedTable = Tables.FirstOrDefault(t => t.Id == selectedId);
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(OccupiedCount));
        OnPropertyChanged(nameof(ReservedCount));
    }

    partial void OnSelectedTableChanged(TableDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasOrder));
        if (value is null)
        {
            EditorTitle = "New table";
            return;
        }
        EditorTitle = $"Table {value.Number}";
        TableNumber = value.Number;
        TableCapacity = value.Capacity;
        TableArea = value.Area ?? string.Empty;
        TableDisplayOrder = value.DisplayOrder;
        SelectedOrder = null;
    }

    partial void OnSelectedOrderChanged(OrderDto? value) => OnPropertyChanged(nameof(HasOrder));

    [RelayCommand]
    private void NewTable()
    {
        SelectedTable = null;
        TableNumber = string.Empty;
        TableCapacity = 4;
        TableArea = string.Empty;
        TableDisplayOrder = Tables.Count + 1;
        EditorTitle = "New table";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(TableNumber))
        {
            ErrorMessage = "Enter a table number.";
            return;
        }
        await RunAsync(async () =>
        {
            var request = new SaveTableRequest(TableNumber.Trim(), Math.Max(1, TableCapacity),
                string.IsNullOrWhiteSpace(TableArea) ? null : TableArea.Trim(), TableDisplayOrder);
            var saved = SelectedTable is null
                ? await _api.CreateTableAsync(request)
                : await _api.UpdateTableAsync(SelectedTable.Id, request);
            StatusMessage = SelectedTable is null ? $"Table {saved.Number} created." : $"Table {saved.Number} saved.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedTable is null) return;
        if (!_dialogs.Confirm($"Delete table {SelectedTable.Number}?")) return;
        await RunAsync(async () =>
        {
            await _api.DeleteTableAsync(SelectedTable.Id);
            SelectedTable = null;
            await LoadCoreAsync();
            StatusMessage = "Table deleted.";
        });
    }

    [RelayCommand]
    private async Task ViewOrderAsync()
    {
        if (SelectedTable is null) return;
        await RunAsync(async () =>
        {
            var order = await _api.GetActiveOrderForTableAsync(SelectedTable.Id);
            if (order is null)
            {
                SelectedOrder = null;
                _dialogs.Info($"Table {SelectedTable.Number} has no open order.");
                return;
            }
            SelectedOrder = order;
        });
    }

    [RelayCommand]
    private void OpenPos()
    {
        if (SelectedTable is null)
        {
            ErrorMessage = "Select a table first.";
            return;
        }
        OpenOrderRequested?.Invoke(SelectedTable);
    }

    [RelayCommand]
    private async Task SetStatusAsync(string status)
    {
        if (SelectedTable is null) return;
        if (!Enum.TryParse<TableStatus>(status, out var target)) return;
        await RunAsync(async () =>
        {
            var updated = await _api.SetTableStatusAsync(SelectedTable.Id, target);
            StatusMessage = $"Table {updated.Number} is now {target}.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task SetAvailableAsync() => await SetStatusAsync(nameof(TableStatus.Available));

    [RelayCommand]
    private async Task SetReservedAsync() => await SetStatusAsync(nameof(TableStatus.Reserved));
}
