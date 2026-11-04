using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CafePOS.Api.Data;
using CafePOS.Api.Hubs;
using CafePOS.Api.Middleware;
using CafePOS.Api.Security;
using CafePOS.Api.Services;
using CafePOS.Api.Validation;
using CafePOS.Core.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Run as a Windows service when installed by the CafePOS Setup (no-op in the console).
builder.Host.UseWindowsService(options => options.ServiceName = "CafePOS API");

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'Default' is not configured. Set ConnectionStrings__Default.");

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters. Set Jwt__Key in production.");

builder.Services.Configure<JwtOptions>(jwtSection);

// Catalog and branding images are written outside the database. In production this
// lives under ProgramData so repairs and upgrades never delete the restaurant's images.
var configuredUploads = builder.Configuration["Storage:UploadsPath"];
var uploadsPath = string.IsNullOrWhiteSpace(configuredUploads)
    ? (builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "uploads")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CafePOS", "uploads"))
    : Path.GetFullPath(configuredUploads, builder.Environment.ContentRootPath);
Directory.CreateDirectory(uploadsPath);
builder.Services.AddSingleton(new ImageStorage(uploadsPath));

builder.Services.AddDbContext<AppDbContext>(options =>
    // No retry-on-failure: the order/payment services use explicit transactions, and
    // Npgsql's retrying strategy rejects user-initiated transactions by default.
    options.UseNpgsql(connectionString,
        npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/orders"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(id, out var userId) ||
                    !await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive))
                    context.Fail("The account is no longer active.");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<SettingsService>();

// Firebase Realtime Database mirroring is server-side only; clients keep using the
// authenticated API and PostgreSQL remains the source of truth.
builder.Services.AddHttpClient(nameof(FirebaseSyncService), client => client.Timeout = TimeSpan.FromSeconds(30))
    // The Firebase auth token is part of the request URI; never write it to the logs.
    .RemoveAllLoggers();
builder.Services.AddSingleton<FirebaseSyncService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FirebaseSyncService>());

builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.CreateUserRequest>, CreateUserRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.ChangePasswordRequest>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.SaveProductRequest>, SaveProductRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.SaveCategoryRequest>, SaveCategoryRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.SaveTableRequest>, SaveTableRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.CreateOrderRequest>, CreateOrderRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.UpdateOrderRequest>, UpdateOrderRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.CheckoutRequest>, CheckoutRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.RefundRequest>, RefundRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.SaveIngredientRequest>, SaveIngredientRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.StockAdjustmentRequest>, StockAdjustmentRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.OpenShiftRequest>, OpenShiftRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.CloseShiftRequest>, CloseShiftRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.ShiftExpenseRequest>, ShiftExpenseRequestValidator>();
builder.Services.AddScoped<IValidator<CafePOS.Core.Contracts.SaveSupplierRequest>, SaveSupplierRequestValidator>();

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseRateLimiter();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "public,max-age=604800";
    }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrderHub>("/hubs/orders");

app.MapGet("/health", async (AppDbContext db) =>
{
    var databaseOk = await db.Database.CanConnectAsync();
    return Results.Json(new
    {
        status = databaseOk ? "healthy" : "degraded",
        database = databaseOk,
        timestamp = DateTimeOffset.UtcNow
    }, statusCode: databaseOk ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapGet("/api/v1/health", async (AppDbContext db) =>
{
    var databaseOk = await db.Database.CanConnectAsync();
    return Results.Json(new { status = databaseOk ? "healthy" : "degraded", database = databaseOk, timestamp = DateTimeOffset.UtcNow },
        statusCode: databaseOk ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Applying database migrations...");
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(app.Services, logger);
}

app.Run();
