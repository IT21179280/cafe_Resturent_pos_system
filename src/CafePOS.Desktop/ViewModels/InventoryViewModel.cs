using System.Collections.ObjectModel;
using CafePOS.Core.Contracts;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class InventoryViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly DialogService _dialogs;

    public InventoryViewModel(ApiClient api, DialogService dialogs)
    {
        _api = api;
        _dialogs = dialogs;
    }

    public ObservableCollection<IngredientDto> Ingredients { get; } = new();
    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<InventoryMovementReportDto> Movements { get; } = new();

    [ObservableProperty] private IngredientDto? _selectedIngredient;
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private bool _lowStockOnly;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _sku = string.Empty;
    [ObservableProperty] private string _unit = "pcs";
    [ObservableProperty] private string _openingStockText = "0";
    [ObservableProperty] private string _lowStockThresholdText = "0";
    [ObservableProperty] private string _costPerUnitText = "0";
    [ObservableProperty] private SupplierDto? _defaultSupplier;
    [ObservableProperty] private string _adjustQuantityText = string.Empty;
    [ObservableProperty] private string _adjustReason = string.Empty;
    [ObservableProperty] private string _editorTitle = "New ingredient";

    public bool IsExisting => SelectedIngredient is not null;
    public string CurrentStockLabel => SelectedIngredient is null
        ? "—"
        : $"{SelectedIngredient.CurrentStock:N3} {SelectedIngredient.Unit}";
    public bool IsLowStock => SelectedIngredient?.IsLowStock == true;

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        Suppliers.Clear();
        foreach (var supplier in await _api.GetSuppliersAsync()) Suppliers.Add(supplier);

        var selectedId = SelectedIngredient?.Id;
        var ingredients = await _api.GetIngredientsAsync(Search, LowStockOnly);
        Ingredients.Clear();
        foreach (var ingredient in ingredients.OrderBy(i => i.Name)) Ingredients.Add(ingredient);
        SelectedIngredient = Ingredients.FirstOrDefault(i => i.Id == selectedId);
    });

    partial void OnSelectedIngredientChanged(IngredientDto? value)
    {
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(CurrentStockLabel));
        OnPropertyChanged(nameof(IsLowStock));
        if (value is null)
        {
            EditorTitle = "New ingredient";
            return;
        }
        EditorTitle = value.Name;
        Name = value.Name;
        Sku = value.Sku ?? string.Empty;
        Unit = value.Unit;
        OpeningStockText = value.CurrentStock.ToString("0.###");
        LowStockThresholdText = value.LowStockThreshold.ToString("0.###");
        CostPerUnitText = value.CostPerUnit.ToString("0.00");
        DefaultSupplier = Suppliers.FirstOrDefault(s => s.Id == value.DefaultSupplierId);
        AdjustQuantityText = string.Empty;
        AdjustReason = string.Empty;
        _ = LoadMovementsAsync(value.Id);
    }

    private async Task LoadMovementsAsync(Guid ingredientId) => await RunAsync(async () =>
    {
        await LoadMovementsCoreAsync(ingredientId);
    });

    private async Task LoadMovementsCoreAsync(Guid ingredientId)
    {
        var movements = await _api.GetIngredientMovementsAsync(ingredientId);
        Movements.Clear();
        foreach (var movement in movements.OrderByDescending(m => m.Date).Take(200)) Movements.Add(movement);
    }

    [RelayCommand]
    private void NewIngredient()
    {
        SelectedIngredient = null;
        Name = string.Empty;
        Sku = string.Empty;
        Unit = "pcs";
        OpeningStockText = "0";
        LowStockThresholdText = "0";
        CostPerUnitText = "0";
        DefaultSupplier = null;
        Movements.Clear();
        EditorTitle = "New ingredient";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Enter an ingredient name.";
            return;
        }
        if (!decimal.TryParse(LowStockThresholdText, out var threshold) || threshold < 0)
        {
            ErrorMessage = "Enter a valid low stock threshold.";
            return;
        }
        if (!decimal.TryParse(CostPerUnitText, out var cost) || cost < 0)
        {
            ErrorMessage = "Enter a valid cost per unit.";
            return;
        }
        decimal currentStock;
        if (SelectedIngredient is null)
        {
            if (!decimal.TryParse(OpeningStockText, out currentStock) || currentStock < 0)
            {
                ErrorMessage = "Enter a valid opening stock.";
                return;
            }
        }
        else
        {
            currentStock = SelectedIngredient.CurrentStock;
        }

        await RunAsync(async () =>
        {
            var request = new SaveIngredientRequest(Name.Trim(),
                string.IsNullOrWhiteSpace(Sku) ? null : Sku.Trim(), Unit.Trim(), currentStock, threshold, cost,
                DefaultSupplier?.Id);
            if (SelectedIngredient is null) await _api.CreateIngredientAsync(request);
            else await _api.UpdateIngredientAsync(SelectedIngredient.Id, request);
            StatusMessage = "Ingredient saved.";
            await LoadCoreAsync();
        });
    }

    private async Task LoadCoreAsync()
    {
        var selectedId = SelectedIngredient?.Id;
        var ingredients = await _api.GetIngredientsAsync(Search, LowStockOnly);
        Ingredients.Clear();
        foreach (var ingredient in ingredients.OrderBy(i => i.Name)) Ingredients.Add(ingredient);
        SelectedIngredient = Ingredients.FirstOrDefault(i => i.Id == selectedId);
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(IsLowStock));
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedIngredient is null) return;
        if (!_dialogs.Confirm($"Delete ingredient '{SelectedIngredient.Name}'?")) return;
        await RunAsync(async () =>
        {
            await _api.DeleteIngredientAsync(SelectedIngredient.Id);
            SelectedIngredient = null;
            await LoadCoreAsync();
            StatusMessage = "Ingredient deleted.";
        });
    }

    [RelayCommand]
    private async Task AdjustStockAsync()
    {
        if (SelectedIngredient is null)
        {
            ErrorMessage = "Select an ingredient first.";
            return;
        }
        if (!decimal.TryParse(AdjustQuantityText, out var quantity) || quantity == 0)
        {
            ErrorMessage = "Enter a non-zero quantity. Use a negative value to remove stock.";
            return;
        }
        if (string.IsNullOrWhiteSpace(AdjustReason))
        {
            ErrorMessage = "Enter a reason for the stock adjustment.";
            return;
        }
        await RunAsync(async () =>
        {
            var updated = await _api.AdjustStockAsync(SelectedIngredient.Id, quantity, AdjustReason.Trim());
            StatusMessage = $"{updated.Name} stock is now {updated.CurrentStock:N3} {updated.Unit}.";
            AdjustQuantityText = string.Empty;
            AdjustReason = string.Empty;
            await LoadCoreAsync();
            await LoadMovementsCoreAsync(updated.Id);
        });
    }
}
