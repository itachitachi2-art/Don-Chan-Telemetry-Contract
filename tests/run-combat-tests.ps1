$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $env:TEMP ('DonChanCombatPolicyTests-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $csc /nologo /langversion:5 /target:exe "/out:$out" (Join-Path $root 'src\TelemetryProbe\CombatEventPolicy.cs') (Join-Path $PSScriptRoot 'CombatPolicyTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Combat policy test compile failed' }
    & $out
    if ($LASTEXITCODE -ne 0) { throw 'Combat policy tests failed' }
} finally {
    Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
}

