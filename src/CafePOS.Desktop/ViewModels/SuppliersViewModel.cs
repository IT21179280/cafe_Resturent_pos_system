using System.Collections.ObjectModel;
using CafePOS.Core.Contracts;
using CafePOS.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafePOS.Desktop.ViewModels;

public partial class SuppliersViewModel : ViewModelBase, IActivatable
{
    private readonly ApiClient _api;
    private readonly DialogService _dialogs;

    public SuppliersViewModel(ApiClient api, DialogService dialogs)
    {
        _api = api;
        _dialogs = dialogs;
    }

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _contactPerson = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _editorTitle = "New supplier";

    public bool IsExisting => SelectedSupplier is not null;
    public decimal TotalPayable => Suppliers.Sum(s => s.Balance);

    public override async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        var selectedId = SelectedSupplier?.Id;
        var suppliers = await _api.GetSuppliersAsync(Search);
        Suppliers.Clear();
        foreach (var supplier in suppliers.OrderBy(s => s.Name)) Suppliers.Add(supplier);
        SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == selectedId);
        OnPropertyChanged(nameof(TotalPayable));
    });

    partial void OnSelectedSupplierChanged(SupplierDto? value)
    {
        OnPropertyChanged(nameof(IsExisting));
        if (value is null)
        {
            EditorTitle = "New supplier";
            return;
        }
        EditorTitle = value.Name;
        Name = value.Name;
        ContactPerson = value.ContactPerson ?? string.Empty;
        Phone = value.Phone ?? string.Empty;
        Email = value.Email ?? string.Empty;
        Address = value.Address ?? string.Empty;
        Notes = value.Notes ?? string.Empty;
    }

    [RelayCommand]
    private void NewSupplier()
    {
        SelectedSupplier = null;
        Name = string.Empty;
        ContactPerson = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Address = string.Empty;
        Notes = string.Empty;
        EditorTitle = "New supplier";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Enter a supplier name.";
            return;
        }
        await RunAsync(async () =>
        {
            var request = new SaveSupplierRequest(Name.Trim(), NullIfEmpty(ContactPerson), NullIfEmpty(Phone),
                NullIfEmpty(Email), NullIfEmpty(Address), NullIfEmpty(Notes));
            if (SelectedSupplier is null) await _api.CreateSupplierAsync(request);
            else await _api.UpdateSupplierAsync(SelectedSupplier.Id, request);
            StatusMessage = "Supplier saved.";
            await LoadCoreAsync();
        });
    }

    private async Task LoadCoreAsync()
    {
        var selectedId = SelectedSupplier?.Id;
        var suppliers = await _api.GetSuppliersAsync(Search);
        Suppliers.Clear();
        foreach (var supplier in suppliers.OrderBy(s => s.Name)) Suppliers.Add(supplier);
        SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == selectedId);
        OnPropertyChanged(nameof(TotalPayable));
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSupplier is null) return;
        if (!_dialogs.Confirm($"Delete supplier '{SelectedSupplier.Name}'?")) return;
        await RunAsync(async () =>
        {
            await _api.DeleteSupplierAsync(SelectedSupplier.Id);
            SelectedSupplier = null;
            await LoadCoreAsync();
            StatusMessage = "Supplier deleted.";
        });
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
