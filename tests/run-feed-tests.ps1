$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $env:TEMP ('DonChanFeedTests-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $csc /nologo /langversion:5 /target:exe "/out:$out" /reference:System.Web.Extensions.dll `
        (Join-Path $root 'src\TelemetryProbe\JsonUtil.cs') `
        (Join-Path $root 'src\TelemetryProbe\TelemetryFeed.cs') `
        (Join-Path $root 'src\TelemetryProbe\TelemetryServer.cs') `
        (Join-Path $PSScriptRoot 'FeedTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Feed test compile failed' }
    & $out
    if ($LASTEXITCODE -ne 0) { throw 'Feed tests failed' }
} finally {
    Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
}
