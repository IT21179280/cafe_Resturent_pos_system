# CafePOS

CafePOS is a complete restaurant and cafe point-of-sale system for Windows. It combines a
WPF desktop client, an ASP.NET Core API, and PostgreSQL. Clients never connect to the
database directly: all reads and writes go through the authenticated API.

The solution is designed for a small cafe or restaurant running one server PC and one or
more cashier, kitchen, or manager terminals on the same LAN. Tailscale can extend the same
setup to a remote owner laptop without exposing PostgreSQL or the API to the public internet.

## What is included

| Area | Features |
| --- | --- |
| Point of sale | Product search and category browsing, dine-in/takeaway/delivery, modifiers, item and order discounts, service charge, tax, split payments, change calculation, receipt printing |
| Tables | Table map, capacity/area, reserve, open in POS, free table |
| Kitchen | Realtime kitchen display (SignalR), station filter, pending/preparing/ready status flow |
| Orders | Search and filters, order details, status changes, cancellation with reason, partial refunds with authorization, reprint receipt |
| Menu | Categories, products, prices, tax, station, availability, barcode/SKU, CSV import/export, recipes, uploaded product and category images |
| Branding | Configurable system name and uploaded logo shown on the sign-in window, dashboard and sidebar |
| Firebase | Optional server-side mirror of orders, sales, products, tables and stock to Firebase Realtime Database for administrator viewing; Settings shows sync status and a manual **Sync now** action |
| Inventory | Ingredients and stock levels, low-stock alerts, adjustments/wastage, recipe-based automatic deduction, movement history |
| Purchasing | Suppliers, purchase orders, partial/full receiving, stock and cost updates |
| Shifts | Open/close shift, opening float, cash expenses, expected cash, variance, reopen with audit reason |
| Reports | Sales, cashier, payment method, category, best sellers, low stock, inventory movements, wastage, refunds, cancellations, shifts, gross profit, CSV export |
| Administration | Users and roles, password reset, audit log, restaurant settings, inventory deduction event, thermal printer settings |
| Security | JWT access tokens, rotating refresh tokens, server-enforced role permissions, rate-limited login, account lockout, audit trail, password-change enforcement |

Money is handled with `decimal` only and displayed in Sri Lankan rupees (LKR).

## Technology

- .NET 10 (LTS), C# 14
- WPF desktop client with CommunityToolkit.Mvvm
- ASP.NET Core Web API with JWT bearer authentication, SignalR, FluentValidation, Serilog
- PostgreSQL with Entity Framework Core and Npgsql
- Firebase Realtime Database REST mirroring (no Firebase client SDK required)
- xUnit tests
- Inno Setup 6 installer with Server/Client component selection

## Repository layout

```
src/CafePOS.Core        Domain entities, enums, contracts, pricing/state/inventory rules
src/CafePOS.Api         ASP.NET Core API, EF Core migrations, services, SignalR hub
src/CafePOS.Desktop     WPF client (views, view models, API client, receipt printing)
tests/CafePOS.Tests     Business-rule unit tests
installer               Inno Setup script and service/configuration helpers
docs                    Deployment, backup, Tailscale, and user guides
```

## Default development accounts

The database seeder creates these accounts on first start:

| Role | Email | Password |
| --- | --- | --- |
| Administrator | `IF you need DM me` | `IF you need DM me` |
| Manager | `IF you need DM me` | `IF you need DM me` |
| Cashier | `IF you need DM me` | `IF you need DM me` |
| Kitchen | `IF you need DM me` | `IF you need DM me` |

The administrator is forced to choose a new password at first login. Change all seeded
passwords before using the system in production.

## Development quick start

Prerequisites:

- Windows 10/11 or Windows Server x64
- .NET 10 SDK
- PostgreSQL 16 or newer (PostgreSQL 18 is used in development)

Create the database and application role (run as a PostgreSQL superuser):

```sql
CREATE ROLE cafepos_app LOGIN PASSWORD 'CafePos_App_2026!Dev';
CREATE DATABASE cafepos OWNER cafepos_app;
```

Create `src/CafePOS.Api/appsettings.Development.json` (this file is gitignored):

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=cafepos;Username=cafepos_app;Password=CafePos_App_2026!Dev"
  },
  "Jwt": {
    "Key": "replace-with-at-least-32-random-characters",
    "Issuer": "CafePOS.Api",
    "Audience": "CafePOS.Desktop",
    "AccessTokenMinutes": 60,
    "RefreshTokenDays": 7
  },
  "Database": { "MigrateOnStartup": true }
}
```

Then:

```powershell
dotnet tool restore
dotnet build CafePOS.slnx
dotnet test tests/CafePOS.Tests/CafePOS.Tests.csproj
dotnet run --project src/CafePOS.Api
```

The API listens on `http://0.0.0.0:5080` in Development. Check it at
`http://localhost:5080/health`. In another terminal start the client from Visual Studio or:

```powershell
dotnet run --project src/CafePOS.Desktop
```

Sign in with the server address `http://localhost:5080` (the client adds `/api/v1`).
The API seeds sample products, tables, recipes, suppliers, and settings automatically on
first start when `Database:MigrateOnStartup` is `true`.

## Build, test, and publish

```powershell
dotnet build CafePOS.slnx -c Release
dotnet test tests/CafePOS.Tests/CafePOS.Tests.csproj -c Release

dotnet publish src/CafePOS.Desktop/CafePOS.Desktop.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -o artifacts/publish/client

dotnet publish src/CafePOS.Api/CafePOS.Api.csproj -c Release -r win-x64 `
  --self-contained true -o artifacts/publish/server
```

Build the single installer (Inno Setup 6 must be installed):

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\CafePOS.iss
# Per-user installs: & "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\CafePOS.iss
```

The installer is written to `artifacts\installer\CafePOS.Setup.exe`. It offers Server,
Client, and Full installation components. The Server component writes
`appsettings.Production.json` from the database details entered in the wizard, registers
`CafePOSApi` as an automatic Windows service, applies migrations on start, and opens TCP
5080 in Windows Firewall. On upgrades and uninstall the existing production configuration
is preserved.

## Documentation

- [Deployment guide](docs/DEPLOYMENT.md)
- [Backup and restore](docs/BACKUP.md)
- [Remote access with Tailscale](docs/TAILSCALE.md)
- [User guide](docs/USER_GUIDE.md)

## Security notes

- Do not expose port 5080 directly to the internet. Use the LAN or Tailscale.
- Change the seeded administrator password and all demonstration accounts.
- Use a unique PostgreSQL password and a unique JWT signing key for each installation.
- Keep `appsettings.Production.json` and database backups private: they contain credentials.
- The desktop client never talks to PostgreSQL; it only uses the HTTPS/HTTP API.
