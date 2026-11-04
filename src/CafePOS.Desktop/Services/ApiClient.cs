using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CafePOS.Core.Contracts;
using CafePOS.Core.Enums;

namespace CafePOS.Desktop.Services;

/// <summary>
/// Single gateway to the CafePOS API. The desktop never talks to PostgreSQL directly and
/// every request carries the JWT access token. Expired tokens are refreshed transparently.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly AuthSession _session;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly JsonSerializerOptions _json;
    private string _baseUrl = "http://localhost:5080/api/v1/";

    public ApiClient(AppSettingsStore settings, AuthSession session)
    {
        _session = session;
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        SetServerUrl(settings.ServerUrl);
    }

    public string ServerUrl => _baseUrl.TrimEnd('/');

    public void SetServerUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var normalized = url.Trim().TrimEnd('/');
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            normalized = "http://" + normalized;
        if (!normalized.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("/api/v1/", StringComparison.OrdinalIgnoreCase))
            normalized += "/api/v1";
        _baseUrl = normalized + "/";
        MediaUrl.SetServerUrl(_baseUrl);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content = null) =>
        new(method, new Uri(_baseUrl + path.TrimStart('/'))) { Content = content };

    // ---------------------------------------------------------------- auth

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var response = await SendAsync<LoginResponse>(HttpMethod.Post, "auth/login",
            new LoginRequest(email.Trim(), password), auth: false, ct: ct);
        _session.Set(response);
        return response;
    }

    public async Task<UserDto> MeAsync(CancellationToken ct = default) =>
        await SendAsync<UserDto>(HttpMethod.Get, "auth/me", ct: ct);

    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) =>
        await SendAsync<object?>(HttpMethod.Post, "auth/change-password",
            new ChangePasswordRequest(currentPassword, newPassword), ct: ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_session.RefreshToken)) return;
        try
        {
            await SendAsync<object?>(HttpMethod.Post, "auth/logout",
                new RefreshTokenRequest(_session.RefreshToken), auth: false, ct: ct);
        }
        catch
        {
            // Logging out locally must always succeed, even if the server is unreachable.
        }
    }

    public async Task RevokeAllTokensAsync(Guid? userId, CancellationToken ct = default) =>
        await SendAsync<object?>(HttpMethod.Post, $"auth/tokens/revoke-all{Query(("userId", userId?.ToString()))}",
            ct: ct);

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_session.RefreshToken)) return false;
        await _refreshLock.WaitAsync(ct);
        try
        {
            var token = _session.RefreshToken;
            if (string.IsNullOrWhiteSpace(token)) return false;
            using var request = CreateRequest(HttpMethod.Post, "auth/refresh",
                JsonContent.Create(new RefreshTokenRequest(token), options: _json));
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;
            var refreshed = await response.Content.ReadFromJsonAsync<LoginResponse>(_json, ct);
            if (refreshed is null) return false;
            _session.Set(refreshed);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // ---------------------------------------------------------------- users

    public Task<List<UserDto>> GetUsersAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<UserDto>>(HttpMethod.Get, "users" + Query(("includeInactive", includeInactive)), ct: ct);

    public Task<UserDto> GetUserAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Get, $"users/{id}", ct: ct);

    public Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Post, "users", request, ct: ct);

    public Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Put, $"users/{id}", request, ct: ct);

    public Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default) =>
        SendAsync<object?>(HttpMethod.Post, $"users/{id}/reset-password", new ResetPasswordRequest(newPassword), ct: ct);

    public async Task<JsonElement> GetAuditLogsAsync(int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        await SendRawAsync(HttpMethod.Get, "users/audit" + Query(("page", page), ("pageSize", pageSize)), null, ct);

    // ---------------------------------------------------------------- categories

    public Task<List<CategoryDto>> GetCategoriesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<CategoryDto>>(HttpMethod.Get, "categories" + Query(("includeInactive", includeInactive)), ct: ct);

    public Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct = default) =>
        SendAsync<CategoryDto>(HttpMethod.Post, "categories", request, ct: ct);

    public Task<CategoryDto> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken ct = default) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"categories/{id}", request, ct: ct);

    public Task DeleteCategoryAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"categories/{id}", ct: ct);

    // ---------------------------------------------------------------- products

    public Task<PagedResult<ProductDto>> GetProductsAsync(string? search = null, Guid? categoryId = null,
        bool availableOnly = false, int page = 1, int pageSize = 200, CancellationToken ct = default) =>
        SendAsync<PagedResult<ProductDto>>(HttpMethod.Get, "products" + Query(
            ("search", search), ("categoryId", categoryId?.ToString()), ("availableOnly", availableOnly),
            ("page", page), ("pageSize", pageSize)), ct: ct);

    public Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<ProductDto>(HttpMethod.Get, $"products/{id}", ct: ct);

    public Task<ProductDto> LookupProductAsync(string code, CancellationToken ct = default) =>
        SendAsync<ProductDto>(HttpMethod.Get, "products/lookup" + Query(("code", code)), ct: ct);

    public Task<ProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken ct = default) =>
        SendAsync<ProductDto>(HttpMethod.Post, "products", request, ct: ct);

    public Task<ProductDto> UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken ct = default) =>
        SendAsync<ProductDto>(HttpMethod.Put, $"products/{id}", request, ct: ct);

    public Task DeleteProductAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"products/{id}", ct: ct);

    public Task<FilePayload> ExportProductsCsvAsync(CancellationToken ct = default) =>
        DownloadAsync("products/export", ct);

    public async Task<ProductImportResult> ImportProductsAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return await SendContentAsync<ProductImportResult>(HttpMethod.Post, "products/import", content, ct);
    }

    public Task<List<RecipeItemDto>> GetRecipeAsync(Guid productId, CancellationToken ct = default) =>
        SendAsync<List<RecipeItemDto>>(HttpMethod.Get, $"products/{productId}/recipe", ct: ct);

    public Task<List<RecipeItemDto>> SaveRecipeAsync(Guid productId, SaveRecipeRequest request, CancellationToken ct = default) =>
        SendAsync<List<RecipeItemDto>>(HttpMethod.Put, $"products/{productId}/recipe", request, ct: ct);

    // ---------------------------------------------------------------- tables

    public Task<List<TableDto>> GetTablesAsync(CancellationToken ct = default) =>
        SendAsync<List<TableDto>>(HttpMethod.Get, "tables", ct: ct);

    public Task<TableDto> CreateTableAsync(SaveTableRequest request, CancellationToken ct = default) =>
        SendAsync<TableDto>(HttpMethod.Post, "tables", request, ct: ct);

    public Task<TableDto> UpdateTableAsync(Guid id, SaveTableRequest request, CancellationToken ct = default) =>
        SendAsync<TableDto>(HttpMethod.Put, $"tables/{id}", request, ct: ct);

    public Task<TableDto> SetTableStatusAsync(Guid id, TableStatus status, CancellationToken ct = default) =>
        SendAsync<TableDto>(HttpMethod.Patch, $"tables/{id}/status", new SetTableStatusRequest(status), ct: ct);

    public Task DeleteTableAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"tables/{id}", ct: ct);

    public Task<OrderDto?> GetActiveOrderForTableAsync(Guid tableId, CancellationToken ct = default) =>
        SendAsync<OrderDto?>(HttpMethod.Get, $"tables/{tableId}/active-order", ct: ct);

    // ---------------------------------------------------------------- orders

    public Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(OrderStatus? status = null, OrderType? type = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null, string? search = null, int page = 1, int pageSize = 50,
        CancellationToken ct = default) =>
        SendAsync<PagedResult<OrderSummaryDto>>(HttpMethod.Get, "orders" + Query(
            ("status", status?.ToString()), ("type", type?.ToString()), ("from", from?.ToString("o")),
            ("to", to?.ToString("o")), ("search", search), ("page", page), ("pageSize", pageSize)), ct: ct);

    public Task<OrderDto> GetOrderAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Get, $"orders/{id}", ct: ct);

    public Task<List<OrderDto>> GetKitchenOrdersAsync(PreparationStation? station = null, CancellationToken ct = default) =>
        SendAsync<List<OrderDto>>(HttpMethod.Get, "orders/kitchen" + Query(("station", station?.ToString())), ct: ct);

    public Task<OrderDto?> GetOrderByTableAsync(Guid tableId, CancellationToken ct = default) =>
        SendAsync<OrderDto?>(HttpMethod.Get, $"orders/by-table/{tableId}", ct: ct);

    public Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, "orders", request, ct: ct);

    public Task<OrderDto> UpdateOrderAsync(Guid id, UpdateOrderRequest request, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Put, $"orders/{id}", request, ct: ct);

    public Task<OrderDto> UpdateOrderStatusAsync(Guid id, OrderStatus status, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"orders/{id}/status", new { status }, ct: ct);

    public Task<OrderDto> CancelOrderAsync(Guid id, string reason, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"orders/{id}/cancel", new CancelOrderRequest(reason), ct: ct);

    public Task<OrderDto> MoveOrderAsync(Guid id, Guid? tableId, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"orders/{id}/move", new MoveOrderRequest(tableId), ct: ct);

    public Task<OrderDto> MergeOrdersAsync(Guid sourceId, Guid targetId, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"orders/{sourceId}/merge/{targetId}", ct: ct);

    public Task<OrderDto> CheckoutAsync(Guid id, CheckoutRequest request, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"orders/{id}/checkout", request, ct: ct);

    public Task<OrderDto> RefundAsync(Guid id, RefundRequest request, CancellationToken ct = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"orders/{id}/refund", request, ct: ct);

    // ---------------------------------------------------------------- dashboard / shifts

    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default) =>
        SendAsync<DashboardDto>(HttpMethod.Get, "dashboard", ct: ct);

    public Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default) =>
        SendAsync<ShiftDto?>(HttpMethod.Get, "shifts/current", ct: ct);

    public Task<ShiftDto> OpenShiftAsync(decimal openingCash, CancellationToken ct = default) =>
        SendAsync<ShiftDto>(HttpMethod.Post, "shifts/open", new OpenShiftRequest(openingCash), ct: ct);

    public Task<ShiftDto> CloseShiftAsync(Guid id, decimal countedCash, string? notes, CancellationToken ct = default) =>
        SendAsync<ShiftDto>(HttpMethod.Post, $"shifts/{id}/close", new CloseShiftRequest(countedCash, notes), ct: ct);

    public Task<ShiftDto> ReopenShiftAsync(Guid id, string reason, CancellationToken ct = default) =>
        SendAsync<ShiftDto>(HttpMethod.Post, $"shifts/{id}/reopen", new CancelOrderRequest(reason), ct: ct);

    public Task<ShiftDto> AddShiftExpenseAsync(Guid id, string description, decimal amount, string? category,
        CancellationToken ct = default) =>
        SendAsync<ShiftDto>(HttpMethod.Post, $"shifts/{id}/expenses",
            new ShiftExpenseRequest(description, amount, category), ct: ct);

    public Task<List<ShiftDto>> GetShiftsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null,
        CancellationToken ct = default) =>
        SendAsync<List<ShiftDto>>(HttpMethod.Get, "shifts" + Query(
            ("from", from?.ToString("o")), ("to", to?.ToString("o"))), ct: ct);

    public Task<ShiftDto> GetShiftAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<ShiftDto>(HttpMethod.Get, $"shifts/{id}", ct: ct);

    // ---------------------------------------------------------------- inventory

    public Task<List<IngredientDto>> GetIngredientsAsync(string? search = null, bool lowStock = false,
        CancellationToken ct = default) =>
        SendAsync<List<IngredientDto>>(HttpMethod.Get, "ingredients" + Query(("search", search), ("lowStock", lowStock)), ct: ct);

    public Task<IngredientDto> GetIngredientAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<IngredientDto>(HttpMethod.Get, $"ingredients/{id}", ct: ct);

    public Task<IngredientDto> CreateIngredientAsync(SaveIngredientRequest request, CancellationToken ct = default) =>
        SendAsync<IngredientDto>(HttpMethod.Post, "ingredients", request, ct: ct);

    public Task<IngredientDto> UpdateIngredientAsync(Guid id, SaveIngredientRequest request, CancellationToken ct = default) =>
        SendAsync<IngredientDto>(HttpMethod.Put, $"ingredients/{id}", request, ct: ct);

    public Task<IngredientDto> AdjustStockAsync(Guid id, decimal quantity, string reason, CancellationToken ct = default) =>
        SendAsync<IngredientDto>(HttpMethod.Post, $"ingredients/{id}/adjust", new StockAdjustmentRequest(quantity, reason), ct: ct);

    public Task<List<InventoryMovementReportDto>> GetIngredientMovementsAsync(Guid id, DateTimeOffset? from = null,
        DateTimeOffset? to = null, CancellationToken ct = default) =>
        SendAsync<List<InventoryMovementReportDto>>(HttpMethod.Get, $"ingredients/{id}/movements" + Query(
            ("from", from?.ToString("o")), ("to", to?.ToString("o"))), ct: ct);

    public Task DeleteIngredientAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"ingredients/{id}", ct: ct);

    public Task<List<LowStockDto>> GetLowStockAsync(CancellationToken ct = default) =>
        SendAsync<List<LowStockDto>>(HttpMethod.Get, "inventory/low-stock", ct: ct);

    public Task<List<InventoryMovementReportDto>> GetInventoryMovementsAsync(DateTimeOffset? from = null,
        DateTimeOffset? to = null, Guid? ingredientId = null, CancellationToken ct = default) =>
        SendAsync<List<InventoryMovementReportDto>>(HttpMethod.Get, "inventory/movements" + Query(
            ("from", from?.ToString("o")), ("to", to?.ToString("o")), ("ingredientId", ingredientId?.ToString())), ct: ct);

    // ---------------------------------------------------------------- suppliers & purchasing

    public Task<List<SupplierDto>> GetSuppliersAsync(string? search = null, CancellationToken ct = default) =>
        SendAsync<List<SupplierDto>>(HttpMethod.Get, "suppliers" + Query(("search", search)), ct: ct);

    public Task<SupplierDto> CreateSupplierAsync(SaveSupplierRequest request, CancellationToken ct = default) =>
        SendAsync<SupplierDto>(HttpMethod.Post, "suppliers", request, ct: ct);

    public Task<SupplierDto> UpdateSupplierAsync(Guid id, SaveSupplierRequest request, CancellationToken ct = default) =>
        SendAsync<SupplierDto>(HttpMethod.Put, $"suppliers/{id}", request, ct: ct);

    public Task DeleteSupplierAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object?>(HttpMethod.Delete, $"suppliers/{id}", ct: ct);

    public Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(PurchaseOrderStatus? status = null,
        Guid? supplierId = null, CancellationToken ct = default) =>
        SendAsync<List<PurchaseOrderDto>>(HttpMethod.Get, "purchase-orders" + Query(
            ("status", status?.ToString()), ("supplierId", supplierId?.ToString())), ct: ct);

    public Task<PurchaseOrderDto> GetPurchaseOrderAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<PurchaseOrderDto>(HttpMethod.Get, $"purchase-orders/{id}", ct: ct);

    public Task<PurchaseOrderDto> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default) =>
        SendAsync<PurchaseOrderDto>(HttpMethod.Post, "purchase-orders", request, ct: ct);

    public Task<PurchaseOrderDto> ReceivePurchaseOrderAsync(Guid id, ReceivePurchaseOrderRequest request,
        CancellationToken ct = default) =>
        SendAsync<PurchaseOrderDto>(HttpMethod.Post, $"purchase-orders/{id}/receive", request, ct: ct);

    public Task<PurchaseOrderDto> CancelPurchaseOrderAsync(Guid id, string reason, CancellationToken ct = default) =>
        SendAsync<PurchaseOrderDto>(HttpMethod.Post, $"purchase-orders/{id}/cancel", new CancelOrderRequest(reason), ct: ct);

    // ---------------------------------------------------------------- reports & settings

    public Task<JsonElement> GetReportAsync(string type, DateTimeOffset? from, DateTimeOffset? to, int take = 20,
        CancellationToken ct = default) =>
        SendRawAsync(HttpMethod.Get, $"reports/{type}" + Query(
            ("from", from?.ToString("o")), ("to", to?.ToString("o")), ("take", take)), null, ct);

    public Task<FilePayload> ExportReportCsvAsync(string type, DateTimeOffset? from, DateTimeOffset? to, int take = 20,
        CancellationToken ct = default) =>
        DownloadAsync($"reports/{type}" + Query(
            ("from", from?.ToString("o")), ("to", to?.ToString("o")), ("take", take), ("format", "csv")), ct);

    public Task<JsonElement> GetAuditLogAsync(DateTimeOffset? from, DateTimeOffset? to, int page = 1, int pageSize = 100,
        CancellationToken ct = default) =>
        SendRawAsync(HttpMethod.Get, "reports/audit-log" + Query(
            ("from", from?.ToString("o")), ("to", to?.ToString("o")), ("page", page), ("pageSize", pageSize)), null, ct);

    public Task<AppSettingsDto> GetSettingsAsync(CancellationToken ct = default) =>
        SendAsync<AppSettingsDto>(HttpMethod.Get, "settings", ct: ct);

    public Task<BrandingDto> GetPublicBrandingAsync(CancellationToken ct = default) =>
        SendAsync<BrandingDto>(HttpMethod.Get, "settings/public", auth: false, ct: ct);

    public Task<AppSettingsDto> UpdateSettingsAsync(UpdateAppSettingsRequest request, CancellationToken ct = default) =>
        SendAsync<AppSettingsDto>(HttpMethod.Put, "settings", request, ct: ct);

    public Task<FirebaseSyncResult> SyncFirebaseNowAsync(CancellationToken ct = default) =>
        SendAsync<FirebaseSyncResult>(HttpMethod.Post, "settings/firebase/sync", ct: ct);

    public async Task<string> UploadImageAsync(Stream stream, string fileName, string kind,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeFor(fileName));
        content.Add(file, "file", fileName);
        var result = await SendContentAsync<ImageUploadResult>(HttpMethod.Post,
            "uploads/image" + Query(("kind", kind)), content, ct);
        return result.Url;
    }

    private static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var root = ServerUrl;
            if (root.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
                root = root[..^"/api/v1".Length];
            using var response = await _http.GetAsync(root.TrimEnd('/') + "/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- plumbing

    private static string Query(params (string Key, object? Value)[] pairs)
    {
        var parts = new List<string>();
        foreach (var (key, value) in pairs)
        {
            if (value is null) continue;
            if (value is bool b) parts.Add($"{key}={(b ? "true" : "false")}");
            else if (value is string s && string.IsNullOrWhiteSpace(s)) continue;
            else parts.Add($"{key}={Uri.EscapeDataString(value.ToString() ?? string.Empty)}");
        }
        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, bool auth = true,
        CancellationToken ct = default)
    {
        var json = await SendForJsonAsync(method, path, body, auth, ct);
        if (json.ValueKind == JsonValueKind.Undefined || json.ValueKind == JsonValueKind.Null)
            return default!;
        return json.Deserialize<T>(_json)!;
    }

    private async Task<T> SendContentAsync<T>(HttpMethod method, string path, HttpContent content,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(method, path, content);
        AttachToken(request);
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, _json, ct);
        return value!;
    }

    private async Task<JsonElement> SendRawAsync(HttpMethod method, string path, object? body,
        CancellationToken ct = default)
    {
        var json = await SendForJsonAsync(method, path, body, true, ct);
        return json;
    }

    private async Task<JsonElement> SendForJsonAsync(HttpMethod method, string path, object? body, bool auth,
        CancellationToken ct)
    {
        using var request = CreateRequest(method, path,
            body is null ? null : JsonContent.Create(body, options: _json));
        if (auth) AttachToken(request);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && auth && await TryRefreshAsync(ct))
        {
            using var retry = CreateRequest(method, path,
                body is null ? null : JsonContent.Create(body, options: _json));
            AttachToken(retry);
            using var retryResponse = await _http.SendAsync(retry, ct);
            if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                _session.NotifyExpired();
                throw new ApiException("Your session has expired. Please sign in again.", 401);
            }
            await EnsureSuccessAsync(retryResponse, ct);
            return await ReadJsonAsync(retryResponse, ct);
        }

        await EnsureSuccessAsync(response, ct);
        return await ReadJsonAsync(response, ct);
    }

    private async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.NoContent) return default;
        var text = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(text)) return default;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var status = (int)response.StatusCode;
        string message = response.ReasonPhrase ?? "Request failed";
        string? detail = null;
        Dictionary<string, string[]>? errors = null;
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    message = m.GetString() ?? message;
                if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                    detail = d.GetString();
                if (root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object)
                {
                    errors = new Dictionary<string, string[]>();
                    foreach (var property in e.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.Array) continue;
                        errors[property.Name] = property.Value.EnumerateArray()
                            .Select(x => x.GetString() ?? string.Empty).ToArray();
                    }
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    message = root.GetString() ?? message;
                }
                else if (detail is null && root.TryGetProperty("title", out var title) &&
                         title.ValueKind == JsonValueKind.String)
                {
                    message = title.GetString() ?? message;
                }
            }
        }
        catch
        {
            // Keep the generic message if the error body is not JSON.
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            message = string.IsNullOrWhiteSpace(message) ? "Your session has expired." : message;
        if (!string.IsNullOrWhiteSpace(detail) && !message.Contains(detail, StringComparison.OrdinalIgnoreCase))
            message = $"{message} ({detail})";
        throw new ApiException(message, status, errors);
    }

    private void AttachToken(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_session.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
    }

    private async Task<FilePayload> DownloadAsync(string path, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        AttachToken(request);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(ct))
        {
            using var retry = CreateRequest(HttpMethod.Get, path);
            AttachToken(retry);
            using var retried = await _http.SendAsync(retry, HttpCompletionOption.ResponseHeadersRead, ct);
            await EnsureSuccessAsync(retried, ct);
            return await ToPayloadAsync(retried, ct);
        }
        await EnsureSuccessAsync(response, ct);
        return await ToPayloadAsync(response, ct);
    }

    private static async Task<FilePayload> ToPayloadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar
                   ?? response.Content.Headers.ContentDisposition?.FileName;
        name = name?.Trim('"');
        if (string.IsNullOrWhiteSpace(name)) name = $"export-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        return new FilePayload(bytes, name);
    }

    public void Dispose()
    {
        _http.Dispose();
        _refreshLock.Dispose();
    }
}

public sealed record FilePayload(byte[] Content, string FileName)
{
    public void SaveAs(string path) => File.WriteAllBytes(path, Content);
}
