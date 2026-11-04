using CafePOS.Core.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafePOS.Desktop.Services;

/// <summary>
/// Shared branding state so the sign-in window, sidebar, home page and settings
/// all update together as soon as a new logo or system name is saved.
/// </summary>
public partial class BrandingService : ObservableObject
{
    private readonly ApiClient _api;

    public BrandingService(ApiClient api) => _api = api;

    [ObservableProperty] private string _systemName = "CafePOS";
    [ObservableProperty] private string? _logoUrl;
    [ObservableProperty] private string _restaurantName = "CafePOS Restaurant";

    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            Apply(await _api.GetPublicBrandingAsync(ct));
        }
        catch
        {
            // Branding is cosmetic; a temporarily unreachable server must not block sign-in.
        }
    }

    public void Apply(BrandingDto branding)
    {
        SystemName = string.IsNullOrWhiteSpace(branding.SystemName) ? "CafePOS" : branding.SystemName.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(branding.LogoUrl) ? null : branding.LogoUrl.Trim();
        RestaurantName = string.IsNullOrWhiteSpace(branding.RestaurantName)
            ? "CafePOS Restaurant"
            : branding.RestaurantName.Trim();
    }

    public void Apply(AppSettingsDto settings)
    {
        SystemName = string.IsNullOrWhiteSpace(settings.SystemName) ? "CafePOS" : settings.SystemName.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(settings.LogoUrl) ? null : settings.LogoUrl.Trim();
        RestaurantName = string.IsNullOrWhiteSpace(settings.RestaurantName)
            ? "CafePOS Restaurant"
            : settings.RestaurantName.Trim();
    }
}
