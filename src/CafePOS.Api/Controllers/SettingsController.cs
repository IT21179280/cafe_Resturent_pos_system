using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly ImageStorage _images;

    private readonly FirebaseSyncService _firebase;
    public SettingsController(SettingsService settings, AuditService audit, ImageStorage images, FirebaseSyncService firebase)
    {
        _settings = settings;
        _audit = audit;
        _images = images;
        _firebase = firebase;
    }

    [HttpGet]
    public async Task<ActionResult<AppSettingsDto>> Get(CancellationToken ct)
    {
        var all = await _settings.GetAllAsync(ct);
        decimal Parse(string key, decimal fallback) =>
            decimal.TryParse(all.GetValueOrDefault(key), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
        string Get(string key, string fallback) => all.GetValueOrDefault(key, fallback);
        bool ParseBool(string key, bool fallback) =>
            bool.TryParse(all.GetValueOrDefault(key), out var value) ? value : fallback;
        DateTimeOffset? ParseDate(string key)
        {
            if (DateTimeOffset.TryParse(all.GetValueOrDefault(key), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt))
                return dt;
            return null;
        }

        var hasToken = !string.IsNullOrWhiteSpace(all.GetValueOrDefault(SettingKeys.FirebaseAuthToken));
        return Ok(new AppSettingsDto(
            Get(SettingKeys.RestaurantName, "CafePOS Restaurant"),
            Get(SettingKeys.RestaurantAddress, string.Empty),
            Get(SettingKeys.RestaurantPhone, string.Empty),
            Get(SettingKeys.RestaurantEmail, string.Empty),
            Get(SettingKeys.Currency, "LKR"),
            Parse(SettingKeys.ServiceChargeRate, 0),
            Parse(SettingKeys.DefaultTaxRate, 0),
            Get(SettingKeys.ReceiptFooter, "Thank you!"),
            Get(SettingKeys.InventoryDeductionEvent, "Completed"),
            Get(SettingKeys.SystemName, "CafePOS"),
            NullIfEmpty(Get(SettingKeys.LogoUrl, string.Empty)),
            SafeFirebaseUrl(Get(SettingKeys.FirebaseDatabaseUrl, string.Empty)),
            ParseBool(SettingKeys.FirebaseSyncEnabled, false),
            hasToken,
            ParseDate(SettingKeys.FirebaseLastSyncedAt),
            NullIfEmpty(Get(SettingKeys.FirebaseLastError, string.Empty))));
    }

    /// <summary>Branding needed by the sign-in window before a token exists.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<BrandingDto>> PublicBranding(CancellationToken ct)
    {
        var all = await _settings.GetAllAsync(ct);
        return Ok(new BrandingDto(
            all.GetValueOrDefault(SettingKeys.SystemName, "CafePOS"),
            NullIfEmpty(all.GetValueOrDefault(SettingKeys.LogoUrl, string.Empty)),
            all.GetValueOrDefault(SettingKeys.RestaurantName, "CafePOS Restaurant")));
    }

    [HttpPut]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<AppSettingsDto>> Update([FromBody] UpdateAppSettingsRequest request, CancellationToken ct)
    {
        if (request.ServiceChargeRate is < 0 or > 100)
            return BadRequest(new ApiErrorResponse("Service charge must be between 0 and 100."));
        if (request.DefaultTaxRate is < 0 or > 100)
            return BadRequest(new ApiErrorResponse("Tax rate must be between 0 and 100."));
        if (!new[] { "Completed", "Served", "Paid" }.Contains(request.InventoryDeductionEvent, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new ApiErrorResponse("Inventory deduction event must be Completed, Served or Paid."));
        if (string.IsNullOrWhiteSpace(request.SystemName))
            return BadRequest(new ApiErrorResponse("System name is required."));
        if (request.SystemName.Trim().Length > 100)
            return BadRequest(new ApiErrorResponse("System name must be 100 characters or fewer."));
        if ((request.LogoUrl?.Trim().Length ?? 0) > 500)
            return BadRequest(new ApiErrorResponse("Logo URL must be 500 characters or fewer."));
        var firebaseInput = request.FirebaseDatabaseUrl?.Trim();
        if ((firebaseInput?.Length ?? 0) > 500)
            return BadRequest(new ApiErrorResponse("Firebase database URL must be 500 characters or fewer."));
        Uri? firebaseUri = null;
        if (!string.IsNullOrWhiteSpace(firebaseInput))
        {
            if (!Uri.TryCreate(firebaseInput, UriKind.Absolute, out firebaseUri) ||
                (firebaseUri.Scheme != Uri.UriSchemeHttps &&
                 !(firebaseUri.Scheme == Uri.UriSchemeHttp &&
                   string.Equals(firebaseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))))
                return BadRequest(new ApiErrorResponse("Firebase database URL must be an absolute http(s) URL."));
            if (!string.IsNullOrEmpty(firebaseUri.Query) || !string.IsNullOrEmpty(firebaseUri.Fragment) ||
                !string.IsNullOrEmpty(firebaseUri.UserInfo))
                return BadRequest(new ApiErrorResponse(
                    "Do not put credentials or query parameters in the Firebase URL. Use the auth token field instead."));
        }
        if (request.FirebaseSyncEnabled && firebaseUri is null)
            return BadRequest(new ApiErrorResponse("Firebase database URL is required when sync is enabled."));
        if ((request.FirebaseAuthToken?.Trim().Length ?? 0) > 2048)
            return BadRequest(new ApiErrorResponse("Firebase auth token is too long."));

        var allCurrent = await _settings.GetAllAsync(ct);
        var oldLogo = allCurrent.GetValueOrDefault(SettingKeys.LogoUrl);
        var oldFirebaseUrl = allCurrent.GetValueOrDefault(SettingKeys.FirebaseDatabaseUrl, string.Empty);
        var newLogo = string.IsNullOrWhiteSpace(request.LogoUrl) ? string.Empty : request.LogoUrl.Trim();

        await _settings.SetAsync(SettingKeys.RestaurantName, request.RestaurantName.Trim(), "Restaurant name", ct);
        await _settings.SetAsync(SettingKeys.RestaurantAddress, request.RestaurantAddress?.Trim() ?? string.Empty, "Restaurant address", ct);
        await _settings.SetAsync(SettingKeys.RestaurantPhone, request.RestaurantPhone?.Trim() ?? string.Empty, "Restaurant phone", ct);
        await _settings.SetAsync(SettingKeys.RestaurantEmail, request.RestaurantEmail?.Trim() ?? string.Empty, "Restaurant email", ct);
        await _settings.SetAsync(SettingKeys.ServiceChargeRate, request.ServiceChargeRate.ToString(System.Globalization.CultureInfo.InvariantCulture), "Service charge percentage", ct);
        await _settings.SetAsync(SettingKeys.DefaultTaxRate, request.DefaultTaxRate.ToString(System.Globalization.CultureInfo.InvariantCulture), "Default tax percentage", ct);
        await _settings.SetAsync(SettingKeys.ReceiptFooter, request.ReceiptFooter?.Trim() ?? string.Empty, "Receipt footer", ct);
        await _settings.SetAsync(SettingKeys.InventoryDeductionEvent, request.InventoryDeductionEvent, "Completed, Served or Paid", ct);
        await _settings.SetAsync(SettingKeys.SystemName, request.SystemName.Trim(), "POS and app display name", ct);
        await _settings.SetAsync(SettingKeys.LogoUrl, newLogo, "POS logo image URL", ct);

        var firebaseUrl = firebaseUri is null
            ? string.Empty
            : firebaseUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        await _settings.SetAsync(SettingKeys.FirebaseDatabaseUrl, firebaseUrl, "Firebase Realtime Database URL", ct);
        await _settings.SetAsync(SettingKeys.FirebaseSyncEnabled, request.FirebaseSyncEnabled ? "true" : "false", "Enable Firebase mirroring", ct);
        if (request.ClearFirebaseAuthToken)
            await _settings.SetAsync(SettingKeys.FirebaseAuthToken, string.Empty, "Firebase auth token", ct);
        else if (!string.IsNullOrWhiteSpace(request.FirebaseAuthToken))
            await _settings.SetAsync(SettingKeys.FirebaseAuthToken, request.FirebaseAuthToken.Trim(), "Firebase auth token", ct);

        // Do not leave an old failure message or timestamp pointing at a previous endpoint.
        if (!string.Equals(oldFirebaseUrl.TrimEnd('/'), firebaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            await _settings.SetAsync(SettingKeys.FirebaseLastSyncedAt, string.Empty, "Last successful Firebase sync", ct);
            await _settings.SetAsync(SettingKeys.FirebaseLastError, string.Empty, "Last Firebase sync error", ct);
        }
        else if (!request.FirebaseSyncEnabled)
        {
            await _settings.SetAsync(SettingKeys.FirebaseLastError, string.Empty, "Last Firebase sync error", ct);
        }

        if (!string.Equals(oldLogo, newLogo, StringComparison.OrdinalIgnoreCase))
            _images.TryDelete(oldLogo);

        await _audit.LogAsync("SettingsUpdated", nameof(AppSetting), null,
            new { request.RestaurantName, request.SystemName, request.ServiceChargeRate, FirebaseSyncEnabled = request.FirebaseSyncEnabled });

        if (request.FirebaseSyncEnabled)
            _ = Task.Run(() => _firebase.SyncNowAsync(CancellationToken.None), CancellationToken.None);

        return Ok(await Get(ct));
    }

    [HttpPost("firebase/sync")]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<FirebaseSyncResult>> SyncFirebase(CancellationToken ct)
    {
        var result = await _firebase.SyncNowAsync(ct);
        return Ok(result);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Never expose a legacy token or query string embedded in a saved database URL.</summary>
    private static string? SafeFirebaseUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? NullIfEmpty(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'))
            : NullIfEmpty(value);
    }
}
