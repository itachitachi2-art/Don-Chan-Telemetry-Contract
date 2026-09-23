param(
    [string]$ModsRoot = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "mod\DonChanTelemetryProbe"
if ([string]::IsNullOrWhiteSpace($ModsRoot)) {
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
        throw "APPDATA is unavailable. Pass -ModsRoot explicitly."
    }
    $ModsRoot = Join-Path $env:APPDATA "7DaysToDie\Mods"
}
$dest = Join-Path $ModsRoot "DonChanTelemetryProbe"
if (!(Test-Path (Join-Path $source "DonChanTelemetryProbe.dll"))) {
    throw "DLL is missing. Run build.bat first."
}
New-Item -ItemType Directory -Path $ModsRoot -Force | Out-Null
if (Get-Process -Name "7DaysToDie", "7DaysToDieServer" -ErrorAction SilentlyContinue) { throw "Close the game before installation." }
if (Test-Path $dest) {
    $backupRoot = Join-Path (Split-Path -Parent $ModsRoot) "DonChanModBackups"
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $backup = Join-Path $backupRoot ("DonChanTelemetryProbe-" + [Guid]::NewGuid().ToString("N"))
    Move-Item -LiteralPath $dest -Destination $backup
    Write-Host "Previous mod and logs preserved: $backup"
}
Copy-Item $source $dest -Recurse -Force
Write-Host "Installed: $dest"
Write-Host "Telemetry output will be written to: $(Join-Path $dest 'Telemetry')"
Write-Host "Launch 7 Days to Die with Easy Anti-Cheat disabled."

