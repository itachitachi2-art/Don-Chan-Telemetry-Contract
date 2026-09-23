param([string]$ModsRoot = "")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "mod\DonChanZombieRadar"
if ([string]::IsNullOrWhiteSpace($ModsRoot)) {
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) { throw "APPDATA is unavailable. Pass -ModsRoot explicitly." }
    $ModsRoot = Join-Path $env:APPDATA "7DaysToDie\Mods"
}
$dest = Join-Path $ModsRoot "DonChanZombieRadar"
if (!(Test-Path (Join-Path $source "DonChanZombieRadar.dll"))) { throw "DLL is missing. Run build.bat first." }
New-Item -ItemType Directory -Path $ModsRoot -Force | Out-Null
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Copy-Item $source $dest -Recurse -Force
Write-Host "Installed: $dest"
Write-Host "Launch 7 Days to Die with Easy Anti-Cheat disabled."
