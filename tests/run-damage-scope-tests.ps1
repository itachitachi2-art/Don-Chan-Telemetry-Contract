$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $env:TEMP ('DonChanDamageScopeTests-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $csc /nologo /langversion:5 /target:exe "/out:$out" (Join-Path $root 'src\TelemetryProbe\DamageActionScope.cs') (Join-Path $PSScriptRoot 'DamageActionScopeTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Damage scope test compile failed' }
    & $out
    if ($LASTEXITCODE -ne 0) { throw 'Damage scope tests failed' }
} finally {
    Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
}
