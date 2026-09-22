$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$mod = Join-Path $root "mod\DonChanTelemetryProbe"
$modInfo = Join-Path $mod "ModInfo.xml"
$dll = Join-Path $mod "DonChanTelemetryProbe.dll"

if (!(Test-Path $dll)) { throw "Build first: build.bat" }
if (!(Test-Path $modInfo)) { throw "ModInfo.xml not found: $modInfo" }

[xml]$xml = Get-Content -LiteralPath $modInfo
$version = $xml.xml.Version.value
if ([string]::IsNullOrWhiteSpace($version)) { throw "Version not found in ModInfo.xml" }

$out = Join-Path $root ("DonChanTelemetryProbe-" + $version + ".zip")
if (Test-Path $out) { Remove-Item $out -Force }
Compress-Archive -Path $mod -DestinationPath $out
Write-Host "Created: $out"
