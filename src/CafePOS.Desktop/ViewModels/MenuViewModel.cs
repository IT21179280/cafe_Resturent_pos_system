using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;
using CafePOS.Desktop.Services;
using CafePOS.Desktop.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CafePOS.Desktop.ViewModels;

public partial class MenuViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly DialogService _dialogs;

    public MenuViewModel(ApiClient api, DialogService dialogs)
    {
        _api = api;
        _dialogs = dialogs;
    }

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<ProductDto> Products { get; } = new();

    [ObservableProperty] private CategoryDto? _selectedCategory;
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _categoryDescription = string.Empty;
    [ObservableProperty] private int _categoryOrder;
    [ObservableProperty] private bool _categoryActive = true;
    [ObservableProperty] private string _categoryImageUrl = string.Empty;
    [ObservableProperty] private string _editorTitle = "New category";

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        var categoryId = SelectedCategory?.Id;
        var productId = SelectedProduct?.Id;
        var categories = await _api.GetCategoriesAsync(includeInactive: true);
        Categories.Clear();
        foreach (var category in categories.OrderBy(c => c.DisplayOrder)) Categories.Add(category);
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == categoryId) ?? Categories.FirstOrDefault();

        var products = await _api.GetProductsAsync(Search, null, false, 1, 500);
        Products.Clear();
        foreach (var product in products.Items.OrderBy(p => p.Name)) Products.Add(product);
        SelectedProduct = Products.FirstOrDefault(p => p.Id == productId);
    }

    partial void OnSelectedCategoryChanged(CategoryDto? value)
    {
        if (value is null)
        {
            EditorTitle = "New category";
            CategoryImageUrl = string.Empty;
            return;
        }
        EditorTitle = value.Name;
        CategoryName = value.Name;
        CategoryDescription = value.Description ?? string.Empty;
        CategoryOrder = value.DisplayOrder;
        CategoryActive = value.IsActive;
        CategoryImageUrl = value.ImageUrl ?? string.Empty;
    }

    partial void OnSearchChanged(string value) => _ = FilterProductsAsync();

    private async Task FilterProductsAsync() => await RunAsync(async () =>
    {
        var products = await _api.GetProductsAsync(Search, null, false, 1, 500);
        Products.Clear();
        foreach (var product in products.Items.OrderBy(p => p.Name)) Products.Add(product);
    });

    [RelayCommand]
    private void NewCategory()
    {
        SelectedCategory = null;
        CategoryName = string.Empty;
        CategoryDescription = string.Empty;
        CategoryOrder = Categories.Count + 1;
        CategoryActive = true;
        CategoryImageUrl = string.Empty;
        EditorTitle = "New category";
    }

    [RelayCommand]
    private async Task SaveCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(CategoryName))
        {
            ErrorMessage = "Enter a category name.";
            return;
        }
        await RunAsync(async () =>
        {
            var request = new SaveCategoryRequest(CategoryName.Trim(),
                string.IsNullOrWhiteSpace(CategoryDescription) ? null : CategoryDescription.Trim(),
                CategoryOrder, CategoryActive,
                string.IsNullOrWhiteSpace(CategoryImageUrl) ? null : CategoryImageUrl.Trim());
            if (SelectedCategory is null) await _api.CreateCategoryAsync(request);
            else await _api.UpdateCategoryAsync(SelectedCategory.Id, request);
            StatusMessage = "Category saved.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task ChooseCategoryImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose category image",
            Filter = ImageDialog.Filter,
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        await RunAsync(async () =>
        {
            await using var stream = File.OpenRead(dialog.FileName);
            CategoryImageUrl = await _api.UploadImageAsync(stream, Path.GetFileName(dialog.FileName), "category");
            StatusMessage = "Category image uploaded. Save the category to apply it.";
        });
    }

    [RelayCommand]
    private void RemoveCategoryImage()
    {
        CategoryImageUrl = string.Empty;
        StatusMessage = "Category image removed. Save the category to apply it.";
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategory is null) return;
        if (!_dialogs.Confirm($"Delete category '{SelectedCategory.Name}'?")) return;
        await RunAsync(async () =>
        {
            await _api.DeleteCategoryAsync(SelectedCategory.Id);
            StatusMessage = "Category deleted.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task NewProductAsync()
    {
        if (Categories.Count == 0)
        {
            ErrorMessage = "Create a category before adding products.";
            return;
        }
        var editor = new ProductEditViewModel(_api, Categories.ToList(), null);
        var window = new ProductEditorWindow { DataContext = editor, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            await RunAsync(LoadCoreAsync);
            StatusMessage = $"Product '{editor.ProductName}' saved.";
        }
    }

    [RelayCommand]
    private async Task EditProductAsync()
    {
        if (SelectedProduct is null) return;
        var editor = new ProductEditViewModel(_api, Categories.ToList(), SelectedProduct);
        var window = new ProductEditorWindow { DataContext = editor, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            await RunAsync(LoadCoreAsync);
            StatusMessage = $"Product '{editor.ProductName}' saved.";
        }
    }

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (SelectedProduct is null) return;
        if (!_dialogs.Confirm($"Delete product '{SelectedProduct.Name}'?")) return;
        await RunAsync(async () =>
        {
            await _api.DeleteProductAsync(SelectedProduct.Id);
            StatusMessage = "Product deleted.";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export products",
            FileName = $"products-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            var payload = await _api.ExportProductsCsvAsync();
            payload.SaveAs(dialog.FileName);
            StatusMessage = $"Exported to {dialog.FileName}.";
        });
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import products",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            await using var stream = File.OpenRead(dialog.FileName);
            var result = await _api.ImportProductsAsync(stream, System.IO.Path.GetFileName(dialog.FileName));
            var detail = result.Errors.Count > 0 ? $" Skipped rows: {string.Join("; ", result.Errors.Take(5))}" : string.Empty;
            StatusMessage = $"Imported {result.Imported}, skipped {result.Skipped}.{detail}";
            await LoadCoreAsync();
        });
    }

    [RelayCommand]
    private async Task EditRecipeAsync()
    {
        if (SelectedProduct is null) return;
        var editor = new RecipeEditViewModel(_api, SelectedProduct);
        var window = new RecipeEditorWindow { DataContext = editor, Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
            StatusMessage = $"Recipe for '{SelectedProduct.Name}' saved.";
    }
}

public partial class ProductEditViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ProductDto? _existing;

    public ProductEditViewModel(ApiClient api, List<CategoryDto> categories, ProductDto? existing)
    {
        _api = api;
        _existing = existing;
        Categories = categories;
        Stations = Enum.GetValues<PreparationStation>();
        SelectedCategory = categories.FirstOrDefault(c => c.Id == existing?.CategoryId) ?? categories.FirstOrDefault();

        if (existing is null) return;
        ProductName = existing.Name;
        Sku = existing.Sku;
        Barcode = existing.Barcode ?? string.Empty;
        Description = existing.Description ?? string.Empty;
        PriceText = existing.Price.ToString("0.00");
        CostPriceText = existing.CostPrice.ToString("0.00");
        TaxRateText = existing.TaxRate.ToString("0.##");
        SelectedStation = existing.PreparationStation;
        IsAvailable = existing.IsAvailable;
        TrackInventory = existing.TrackInventory;
        ImageUrl = existing.ImageUrl ?? string.Empty;

        foreach (var modifier in existing.Modifiers)
            Modifiers.Add(new ModifierEditViewModel(modifier.Id, modifier.Name, modifier.PriceDelta, modifier.IsActive));
    }

    public List<CategoryDto> Categories { get; }
    public PreparationStation[] Stations { get; }
    public ObservableCollection<ModifierEditViewModel> Modifiers { get; } = new();
    public bool IsNew => _existing is null;
    public string Heading => IsNew ? "New product" : $"Edit {_existing!.Name}";

    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _sku = string.Empty;
    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private CategoryDto? _selectedCategory;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _priceText = "0.00";
    [ObservableProperty] private string _costPriceText = "0.00";
    [ObservableProperty] private string _taxRateText = "0";
    [ObservableProperty] private PreparationStation _selectedStation = PreparationStation.Kitchen;
    [ObservableProperty] private bool _isAvailable = true;
    [ObservableProperty] private bool _trackInventory;
    [ObservableProperty] private string _imageUrl = string.Empty;
    [ObservableProperty] private bool _saved;

    [RelayCommand]
    private async Task ChooseImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose product image",
            Filter = ImageDialog.Filter,
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        await RunAsync(async () =>
        {
            await using var stream = File.OpenRead(dialog.FileName);
            ImageUrl = await _api.UploadImageAsync(stream, Path.GetFileName(dialog.FileName), "product");
            StatusMessage = "Product image uploaded.";
        });
    }

    [RelayCommand]
    private void RemoveImage()
    {
        ImageUrl = string.Empty;
        StatusMessage = "Product image removed.";
    }

    [RelayCommand]
    private void AddModifier() => Modifiers.Add(new ModifierEditViewModel());

    [RelayCommand]
    private void RemoveModifier(ModifierEditViewModel? modifier)
    {
        if (modifier is not null) Modifiers.Remove(modifier);
    }

    public Task SaveAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(ProductName)) throw new ApiException("Enter a product name.");
        if (Sku.Length == 0) throw new ApiException("Enter a SKU.");
        if (SelectedCategory is null) throw new ApiException("Select a category.");
        if (!decimal.TryParse(PriceText, out var price) || price < 0) throw new ApiException("Enter a valid price.");
        if (!decimal.TryParse(CostPriceText, out var cost) || cost < 0) throw new ApiException("Enter a valid cost price.");
        if (!decimal.TryParse(TaxRateText, out var tax) || tax < 0) throw new ApiException("Enter a valid tax rate.");

        var modifiers = new List<SaveProductModifierRequest>();
        var modifierNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in Modifiers)
        {
            var name = modifier.Name.Trim();
            if (name.Length == 0)
                throw new ApiException("Enter a name for each modifier, or remove the empty row.");
            if (!modifierNames.Add(name))
                throw new ApiException($"Modifier '{name}' is listed more than once.");
            if (!decimal.TryParse(modifier.PriceText, out var extra) || extra < 0)
                throw new ApiException($"Enter a valid extra price for '{name}'.");

            modifiers.Add(new SaveProductModifierRequest(modifier.Id, name, extra, modifier.IsActive));
        }

        var request = new SaveProductRequest(ProductName.Trim(), Sku.Trim(),
            string.IsNullOrWhiteSpace(Barcode) ? null : Barcode.Trim(), SelectedCategory.Id,
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(), price, cost,
            string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim(), IsAvailable, SelectedStation, tax,
            TrackInventory, modifiers);

        if (_existing is null) await _api.CreateProductAsync(request);
        else await _api.UpdateProductAsync(_existing.Id, request);
        Saved = true;
        StatusMessage = "Product saved.";
    });
}

public partial class ModifierEditViewModel : ObservableObject
{
    public ModifierEditViewModel()
    {
    }

    public ModifierEditViewModel(Guid id, string name, decimal priceDelta, bool isActive)
    {
        Id = id;
        Name = name;
        PriceText = priceDelta.ToString("0.00");
        IsActive = isActive;
    }

    public Guid? Id { get; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _priceText = "0.00";
    [ObservableProperty] private bool _isActive = true;
}

public partial class RecipeLineViewModel : ObservableObject
{
    public RecipeLineViewModel(List<IngredientDto> ingredients, IngredientDto? ingredient = null, decimal quantity = 0)
    {
        Ingredients = ingredients;
        SelectedIngredient = ingredient ?? ingredients.FirstOrDefault();
        QuantityText = quantity == 0 ? "0" : quantity.ToString("0.###");
    }

    public List<IngredientDto> Ingredients { get; }
    [ObservableProperty] private IngredientDto? _selectedIngredient;
    [ObservableProperty] private string _quantityText = "0";
    public string Unit => SelectedIngredient?.Unit ?? string.Empty;
    public decimal Cost => SelectedIngredient is null || !decimal.TryParse(QuantityText, out var q)
        ? 0
        : q * SelectedIngredient.CostPerUnit;

    partial void OnSelectedIngredientChanged(IngredientDto? value)
    {
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(Cost));
    }

    partial void OnQuantityTextChanged(string value) => OnPropertyChanged(nameof(Cost));
}

public partial class RecipeEditViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ProductDto _product;

    public RecipeEditViewModel(ApiClient api, ProductDto product)
    {
        _api = api;
        _product = product;
        _ = LoadAsync();
    }

    public ObservableCollection<IngredientDto> Ingredients { get; } = new();
    public ObservableCollection<RecipeLineViewModel> Lines { get; } = new();
    public string Heading => $"Recipe · {_product.Name}";

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var ingredients = await _api.GetIngredientsAsync();
        Ingredients.Clear();
        foreach (var ingredient in ingredients.OrderBy(i => i.Name)) Ingredients.Add(ingredient);
        var recipe = await _api.GetRecipeAsync(_product.Id);
        Lines.Clear();
        foreach (var item in recipe)
        {
            var ingredient = Ingredients.FirstOrDefault(i => i.Id == item.IngredientId) ??
                             new IngredientDto(item.IngredientId, item.IngredientName, null, item.Unit, 0, 0,
                                 item.CostPerUnit, null, null, false);
            Lines.Add(new RecipeLineViewModel(Ingredients.ToList(), ingredient, item.Quantity));
        }
        if (Lines.Count == 0 && Ingredients.Count > 0) Lines.Add(new RecipeLineViewModel(Ingredients.ToList()));
    });

    [RelayCommand]
    private void AddLine()
    {
        var ingredient = Ingredients.FirstOrDefault();
        if (ingredient is null) return;
        Lines.Add(new RecipeLineViewModel(Ingredients.ToList(), ingredient));
    }

    [RelayCommand]
    private void RemoveLine(RecipeLineViewModel? line)
    {
        if (line is not null) Lines.Remove(line);
    }

    public Task SaveAsync() => RunAsync(async () =>
    {
        var items = new List<SaveRecipeItemRequest>();
        foreach (var line in Lines)
        {
            if (line.SelectedIngredient is null) continue;
            if (!decimal.TryParse(line.QuantityText, out var quantity) || quantity <= 0) continue;
            items.Add(new SaveRecipeItemRequest(line.SelectedIngredient.Id, quantity));
        }
        await _api.SaveRecipeAsync(_product.Id, new SaveRecipeRequest(items));
        StatusMessage = "Recipe saved.";
    });
}
