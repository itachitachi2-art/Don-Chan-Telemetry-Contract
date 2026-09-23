$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $env:TEMP ('DonChanNearbyTests-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $csc /nologo /langversion:5 /target:exe "/out:$out" (Join-Path $root 'src\TelemetryProbe\NearbyObservation.cs') (Join-Path $PSScriptRoot 'NearbyObservationTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Nearby test compile failed' }
    & $out
    if ($LASTEXITCODE -ne 0) { throw 'Nearby tests failed' }
} finally {
    Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
}
