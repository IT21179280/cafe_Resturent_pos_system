param(
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [Parameter(Mandatory = $true)][string]$SettingsFile
)

$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $ConfigPath) {
    Write-Host "Production configuration already exists at $ConfigPath - keeping it."
    exit 0
}

if (-not (Test-Path -LiteralPath $SettingsFile)) {
    throw "Server settings file not found: $SettingsFile"
}

$values = @(Get-Content -LiteralPath $SettingsFile -Encoding UTF8)
if ($values.Count -lt 5) {
    throw 'The installer did not supply all PostgreSQL connection values.'
}

$dbHost = $values[0].Trim()
$dbPort = 0
if (-not [int]::TryParse($values[1].Trim(), [ref]$dbPort)) {
    throw "Invalid database port: $($values[1])"
}
$dbName = $values[2].Trim()
$dbUser = $values[3].Trim()
$dbPassword = $values[4]

if ([string]::IsNullOrWhiteSpace($dbHost) -or [string]::IsNullOrWhiteSpace($dbName) -or
    [string]::IsNullOrWhiteSpace($dbUser) -or [string]::IsNullOrWhiteSpace($dbPassword)) {
    throw 'Database host, name, user and password are required.'
}

$keyBytes = New-Object byte[] 48
$rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
$rng.GetBytes($keyBytes)
$jwtKey = [Convert]::ToBase64String($keyBytes)

$connectionString = "Host=$dbHost;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$dbPassword"

$config = [ordered]@{
    Serilog = [ordered]@{
        MinimumLevel = [ordered]@{
            Default = 'Information'
            Override = [ordered]@{
                Microsoft = 'Warning'
                'Microsoft.AspNetCore' = 'Warning'
                'Microsoft.EntityFrameworkCore.Database.Command' = 'Warning'
                System = 'Warning'
            }
        }
        WriteTo = @(
            @{ Name = 'Console' },
            @{
                Name = 'File'
                Args = @{
                    path = 'C:/ProgramData/CafePOS/logs/cafepos-api-.log'
                    rollingInterval = 'Day'
                    retainedFileCountLimit = 30
                    shared = $true
                }
            }
        )
    }
    ConnectionStrings = [ordered]@{ Default = $connectionString }
    Jwt = [ordered]@{
        Key = $jwtKey
        Issuer = 'CafePOS.Api'
        Audience = 'CafePOS.Desktop'
        AccessTokenMinutes = 60
        RefreshTokenDays = 7
    }
    Database = [ordered]@{ MigrateOnStartup = $true }
    Storage = [ordered]@{ UploadsPath = 'C:/ProgramData/CafePOS/uploads' }
    Kestrel = [ordered]@{
        Endpoints = [ordered]@{
            Http = [ordered]@{ Url = 'http://0.0.0.0:5080' }
        }
    }
    AllowedHosts = '*'
}

$json = $config | ConvertTo-Json -Depth 10
$utf8 = New-Object System.Text.UTF8Encoding($false)
$directory = Split-Path -Parent $ConfigPath
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
[System.IO.File]::WriteAllText($ConfigPath, $json, $utf8)
Write-Host "Wrote production configuration to $ConfigPath"
