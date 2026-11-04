# CafePOS deployment guide

This guide covers a production installation on Windows. The recommended topology is one
server PC running PostgreSQL and the CafePOS API, plus one or more client PCs running the
WPF desktop app on the same LAN.

```
[CafePOS Desktop] ---- HTTP :5080 ---- [CafePOS API] ---- localhost:5432 ---- [PostgreSQL]
      client PCs                         server PC
```

The desktop never connects to PostgreSQL directly. Only the server PC needs PostgreSQL.

## 1. Server prerequisites

- Windows 10/11 Pro/Enterprise or Windows Server, x64.
- PostgreSQL 16 or newer. PostgreSQL 18 is what the project is tested against.
- A static LAN address for the server PC (recommended), for example `192.168.1.10`.
- Administrator rights for installation.

The published API and desktop are self-contained; the .NET runtime does not need to be
installed on target machines.

## 2. Create the PostgreSQL role and database

Run these commands as a PostgreSQL superuser (`postgres`). Replace the password with a
strong password of your own.

```sql
CREATE ROLE cafepos_app LOGIN PASSWORD 'ChangeThisDatabasePassword!';
CREATE DATABASE cafepos OWNER cafepos_app;
```

Keep PostgreSQL listening only on localhost. The API is the only component that should
have database credentials. In `postgresql.conf` the default `listen_addresses = 'localhost'`
is appropriate for this deployment.

If you use PgAdmin, create the login role and database with the same names and make
`cafepos_app` the owner.

## 3. Run the installer

Copy `CafePOS.Setup.exe` to the server and run it as administrator.

1. Choose **Full installation (server and client)** if the server PC also has a cashier
   terminal. Choose **Server only** for a dedicated server.
2. On the **Server configuration** page enter:
   - Host: `localhost`
   - Port: `5432`
   - Database: `cafepos`
   - User: `cafepos_app`
   - Password: the password created in step 2
3. Leave **Install and start the CafePOS API Windows service** selected.
4. Finish the wizard. The installer:
   - copies the self-contained API and client,
   - writes `C:\Program Files\CafePOS\Server\appsettings.Production.json`,
   - creates a firewall rule for TCP 5080,
   - registers and starts the `CafePOSApi` service.

The API applies EF Core migrations and seeds the first-run data automatically. The first
start can take a few seconds.

If the service fails to start, check:

- `C:\ProgramData\CafePOS\logs\cafepos-api-*.log`
- Windows Event Viewer -> Windows Logs -> Application
- `http://localhost:5080/health` on the server

The most common causes are an incorrect database password, PostgreSQL not running, or the
database/role not existing.

## 4. Verify the server

On the server PC:

```powershell
Invoke-RestMethod http://localhost:5080/health
```

A healthy response contains `"status": "healthy"` and `"database": true`.

Then confirm another PC can reach the server:

```powershell
Invoke-RestMethod http://192.168.1.10:5080/health
```

If the first command works but the second does not, check Windows Firewall and the router
or VLAN configuration. The installer creates an inbound rule named `CafePOS API (TCP 5080)`.

## 5. Install clients

Run the same installer on each client PC and choose **Client only** (or **Custom** with only
the client component). Start CafePOS from the Start menu or desktop shortcut.

On the sign-in screen:

- Server address: `http://192.168.1.10:5080` (the client adds `/api/v1` automatically)
- Email and password: a CafePOS user account

Use the **Test connection** button on the Settings screen to confirm connectivity. Device
settings also let you change the server address, choose a receipt printer, and optionally
choose a separate kitchen printer for KOT tickets. If no kitchen printer is configured,
kitchen tickets use the receipt printer or the Windows print dialog.

The install directory on a client is `C:\Program Files\CafePOS\Client`. Client settings
(server address, last email, receipt and kitchen printer choices) are stored per Windows user
in the application data folder.

## 6. First login and accounts

The initial administrator is:

- `admin@cafepos.local` / `Admin@12345`

The administrator is required to set a new password at first login. Do this immediately.
Create individual accounts for each staff member under **Users** and disable or delete
accounts when staff leave.

Seeded demo accounts (`manager@`, `cashier@`, `kitchen@`) should also be given new passwords
before production use.

## 7. Service management

The API runs as the Windows service `CafePOSApi` (display name `CafePOS API`).

```powershell
Get-Service CafePOSApi
Stop-Service CafePOSApi
Start-Service CafePOSApi
Restart-Service CafePOSApi
```

The service starts automatically with Windows. It runs as `LocalSystem` by default. If your
security policy requires a dedicated service account, change the account in `services.msc`
and grant it read access to `C:\Program Files\CafePOS\Server` and write access to
`C:\ProgramData\CafePOS`.

## 8. Configuration file

`C:\Program Files\CafePOS\Server\appsettings.Production.json` contains:

- the PostgreSQL connection string,
- the JWT signing key,
- the log location,
- the listening URL (`http://0.0.0.0:5080`),
- the uploaded-image directory (`C:\ProgramData\CafePOS\uploads`),
- automatic migration and seeding settings.

Uploaded branding, product and category images are stored in
`C:\ProgramData\CafePOS\uploads` and served by the API at `/uploads`. Keep this directory in
your backup plan and do not delete it during upgrades or server maintenance.

Firebase mirroring is optional and originates from the API as outbound HTTPS traffic to the
configured Realtime Database URL. It needs no inbound firewall rule. Firebase credentials
entered in Settings are stored in the database, not written to `appsettings.Production.json`.

The installer preserves this file during upgrades and uninstall. If the database password
contains characters that are difficult to pass through the installer, edit the JSON file
manually and restart the service.

Back up this file securely. The JWT key signs login sessions; changing it signs everyone
out. Do not commit it to source control.

## 9. Upgrades

1. Back up the database first (see [BACKUP.md](BACKUP.md)).
2. Run the new `CafePOS.Setup.exe` over the existing installation.
3. Keep the existing production configuration and database details.
4. The installer restarts the service and EF Core applies any new migrations.

The existing `appsettings.Production.json` and the database are not removed.

## 10. Network and remote access

- LAN clients: allow TCP 5080 from the LAN only.
- Remote owner access: use Tailscale (see [TAILSCALE.md](TAILSCALE.md)). Do not port-forward
  port 5080 on the router.
- HTTPS: the bundled API uses HTTP. On a trusted LAN or Tailscale network, traffic is
  protected by the network. If you need HTTPS on the open internet, put a TLS reverse proxy
  in front of the API and update client server addresses to the HTTPS URL.
- PostgreSQL must remain local to the server; never expose 5432 to clients or the internet.

## 11. Troubleshooting

| Symptom | Check |
| --- | --- |
| Client says it cannot reach the server | Is `CafePOSApi` running? Does `http://server:5080/health` work from the client? Is the firewall rule present? |
| Client signs in but screens are empty | Check the API log for a database error; confirm the database was migrated and seeded. |
| Service starts and immediately stops | Read `C:\ProgramData\CafePOS\logs`; usually a bad connection string or PostgreSQL is stopped. |
| Login fails for a known password | Account may be locked after five failed attempts (5 minutes) or disabled in Users. |
| Kitchen display does not update live | Check the connection indicator and that the client can reach `/hubs/orders`; SignalR uses the same port. |
| Printer does not print | In Settings choose the correct Windows printer and enable thermal printing only if the printer supports ESC/POS. For kitchen tickets, set the kitchen printer name and enable the kitchen printer toggle. Otherwise use the Windows print dialog. |
| Port 5080 already in use | Stop the other service or change the Kestrel URL in `appsettings.Production.json` and update client server addresses. |

## 12. Manual uninstall / reset

Uninstall from Windows Settings. The uninstaller stops and removes the `CafePOSApi` service
and removes the firewall rule. It keeps:

- the PostgreSQL database (all sales data),
- `appsettings.Production.json` (credentials),
- `C:\ProgramData\CafePOS\logs`.

Delete those manually only after taking a backup and confirming they are no longer needed.
