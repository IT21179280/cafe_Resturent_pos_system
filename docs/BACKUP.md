# Backup and restore

CafePOS stores all business data in PostgreSQL. The API logs, configuration, and local
client settings are secondary but should still be included in a recovery plan.

## What to back up

| Item | Location | Notes |
| --- | --- | --- |
| PostgreSQL database | `cafepos` on the server | Sales, users, products, inventory, shifts, audit log |
| Uploaded images | `C:\ProgramData\CafePOS\uploads` | Branding logo, product photos and category images |
| Server configuration | `C:\Program Files\CafePOS\Server\appsettings.Production.json` | Database password and JWT signing key |
| API logs (optional) | `C:\ProgramData\CafePOS\logs` | Useful for troubleshooting, not required for restore |
| Client device settings (optional) | Per-user application data | Server address and printer choice; easy to recreate |
| Installer | `CafePOS.Setup.exe` | Keep the version used in production |

Firebase Realtime Database is a mirror for administrator viewing, not the authoritative copy.
A complete CafePOS restore uses PostgreSQL and the uploads directory. Firebase alone cannot
restore sales, users or inventory.

## Daily backup with pg_dump

Use the PostgreSQL bin directory (typically `C:\Program Files\PostgreSQL\18\bin`). Run this
from Task Scheduler on the server as an administrator.

```powershell
$env:PGPASSWORD = 'ChangeThisDatabasePassword!'
$backupDir = 'D:\CafePOS-Backups'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

& 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe' `
  --host localhost --port 5432 --username cafepos_app --dbname cafepos `
  --format custom --file "$backupDir\cafepos-$stamp.dump"
```

Copy backups to a second disk or a network location. A backup stored only on the same PC is
not a disaster recovery plan.

A custom-format dump is compressed and supports selective restore. To make a plain SQL
copy instead:

```powershell
& pg_dump.exe --host localhost --username cafepos_app --dbname cafepos `
  --format plain --file "D:\CafePOS-Backups\cafepos-$stamp.sql"
```

## Retention

Keep at least:

- 14 daily backups,
- 4 weekly backups,
- 12 monthly backups.

Delete old files only after confirming the newest backup can be restored. The API log
retention is controlled in `appsettings.Production.json` (30 days by default).

## Restore procedure

1. Stop the CafePOS API service:
   ```powershell
   Stop-Service CafePOSApi
   ```
2. Back up the current broken database first, if possible.
3. Recreate the database if needed:
   ```sql
   DROP DATABASE cafepos;
   CREATE DATABASE cafepos OWNER cafepos_app;
   ```
4. Restore:
   ```powershell
   $env:PGPASSWORD = 'ChangeThisDatabasePassword!'
   & 'C:\Program Files\PostgreSQL\18\bin\pg_restore.exe' `
     --host localhost --port 5432 --username cafepos_app --dbname cafepos `
     --clean --if-exists --no-owner --no-privileges `
     'D:\CafePOS-Backups\cafepos-20261101-020000.dump'
   ```
5. Restore `appsettings.Production.json` if the server was rebuilt.
6. Start the service:
   ```powershell
   Start-Service CafePOSApi
   ```
7. Verify `http://localhost:5080/health` and sign in to confirm products, sales, and shifts.

If you restore an old database after a JWT key change, the old login sessions are invalid.
Users must sign in again.

## Restore test

Test a restore at least once per quarter:

1. Create a temporary database:
   ```sql
   CREATE DATABASE cafepos_restore_test OWNER cafepos_app;
   ```
2. Restore the latest dump into it.
3. Point a temporary API instance at the test database (change `ConnectionStrings:Default`
   in a copy of the production settings).
4. Confirm the dashboard and a report load, then delete the test database.

Never test a restore by overwriting the live database.

## Before an upgrade

Always take a fresh backup before running a new installer. EF Core migrations normally move
the schema forward; a backup is the reliable way back if a migration fails.
