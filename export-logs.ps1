param([string]$ModsRoot = "")
$ErrorActionPreference = "Stop"
if (Get-Process -Name "7DaysToDie", "7DaysToDieServer" -ErrorAction SilentlyContinue) { throw "Close the game before exporting logs." }
if (!$ModsRoot) { $ModsRoot = Join-Path $env:APPDATA "7DaysToDie\Mods" }
$logs = Join-Path $ModsRoot "DonChanTelemetryProbe\Telemetry"
if (!(Test-Path $logs)) { throw "Telemetry directory not found: $logs" }
$out = Join-Path $PSScriptRoot ("combat-validation-logs-" + (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + [Guid]::NewGuid().ToString("N").Substring(0,6) + ".zip")
Compress-Archive -Path $logs -DestinationPath $out
Write-Host "Send this ZIP: $out"
