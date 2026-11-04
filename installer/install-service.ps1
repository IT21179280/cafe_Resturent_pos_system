param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$ServiceName = 'CafePOSApi'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Server executable not found: $ExePath"
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

$binaryPath = '"' + $ExePath + '"'
& sc.exe create $ServiceName binPath= $binaryPath start= auto DisplayName= 'CafePOS API' | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Could not register the CafePOS API service (sc.exe exit code $LASTEXITCODE)."
}

& sc.exe description $ServiceName 'CafePOS restaurant point-of-sale API' | Out-Null
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Start-Service -Name $ServiceName
Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName
if ($service.Status -ne 'Running') {
    throw "The CafePOS API service was installed but did not start. Check $env:ProgramData\CafePOS\logs and the Windows Event Viewer."
}

Write-Host "CafePOS API service is running."
