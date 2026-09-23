param(
    [string]$Game7D2D = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die",
    [ValidateSet("Debug","Release")][string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $root "src\TelemetryProbe"
$outputDir = Join-Path $root "mod\DonChanTelemetryProbe"
$outputDll = Join-Path $outputDir "DonChanTelemetryProbe.dll"
$outputPdb = Join-Path $outputDir "DonChanTelemetryProbe.pdb"

$managedDir = Join-Path $Game7D2D "7DaysToDie_Data\Managed"
$assembly = Join-Path $managedDir "Assembly-CSharp.dll"
$logLibrary = Join-Path $managedDir "LogLibrary.dll"
$unityCore = Join-Path $managedDir "UnityEngine.CoreModule.dll"
$unityEngine = Join-Path $managedDir "UnityEngine.dll"
$firstPass = Join-Path $managedDir "Assembly-CSharp-firstpass.dll"
$harmonyCandidates = @(
    (Join-Path $Game7D2D "Mods\0_TFP_Harmony\0Harmony.dll"),
    (Join-Path $managedDir "0Harmony.dll")
)
$harmony = $harmonyCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

foreach ($required in @($assembly, $logLibrary, $unityCore)) {
    if (!(Test-Path $required)) { throw "Required game assembly not found: $required" }
}
if (!$harmony) {
    throw ("0Harmony.dll not found. Checked:`n  " + ($harmonyCandidates -join "`n  "))
}

# Compiler executable only: use the C# compiler already shipped with Windows.
$frameworkCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319")
)
$frameworkDir = $frameworkCandidates | Where-Object { Test-Path (Join-Path $_ "csc.exe") } | Select-Object -First 1
if (!$frameworkDir) {
    throw ("Windows C# compiler (csc.exe) not found. Checked:`n  " + (($frameworkCandidates | ForEach-Object { Join-Path $_ "csc.exe" }) -join "`n  "))
}
$csc = Join-Path $frameworkDir "csc.exe"

# IMPORTANT: 7DTD V3 / current Unity assemblies target the Unity Mono/.NET Standard 2.1 profile.
# Do not let csc.exe silently pull Windows .NET Framework mscorlib/System assemblies.
# Instead compile against the runtime libraries shipped with this exact game install.
$monoRoots = @(
    (Join-Path $Game7D2D "MonoBleedingEdge\lib\mono\unityjit"),
    (Join-Path $Game7D2D "MonoBleedingEdge\lib\mono\4.5"),
    (Join-Path $Game7D2D "MonoBleedingEdge\lib\mono\4.7.1-api"),
    (Join-Path $Game7D2D "MonoBleedingEdge\lib\mono\4.7.2-api"),
    (Join-Path $Game7D2D "MonoBleedingEdge\lib\mono\4.8-api")
) | Where-Object { Test-Path $_ }

$searchDirs = New-Object System.Collections.Generic.List[string]
$searchDirs.Add($managedDir)
foreach ($d in $monoRoots) { if (!$searchDirs.Contains($d)) { $searchDirs.Add($d) } }
foreach ($d in $monoRoots) {
    $facades = Join-Path $d "Facades"
    if (Test-Path $facades) { if (!$searchDirs.Contains($facades)) { $searchDirs.Add($facades) } }
}

function Find-GameReference([string]$Name, [bool]$Required = $true) {
    foreach ($dir in $searchDirs) {
        $candidate = Join-Path $dir $Name
        if (Test-Path $candidate) { return $candidate }
    }
    if ($Required) {
        throw ("Game runtime reference not found: $Name`nSearched:`n  " + ($searchDirs -join "`n  "))
    }
    return $null
}

$runtimeRefs = New-Object System.Collections.Generic.List[string]
foreach ($name in @("mscorlib.dll", "System.dll", "System.Core.dll", "System.Xml.dll", "System.Xml.Linq.dll", "netstandard.dll")) {
    $runtimeRefs.Add((Find-GameReference $name $true))
}

# Optional runtime/facade assemblies commonly used by Unity/.NET Standard metadata.
foreach ($name in @(
    "UnityEngine.InputLegacyModule.dll",
    "System.Runtime.dll",
    "System.Runtime.Extensions.dll",
    "System.Runtime.InteropServices.dll",
    "System.Collections.dll",
    "System.Collections.Concurrent.dll",
    "System.Reflection.dll",
    "System.Reflection.Extensions.dll",
    "System.Threading.dll",
    "System.Threading.Tasks.dll",
    "System.Memory.dll",
    "System.Buffers.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.ValueTuple.dll"
)) {
    $found = Find-GameReference $name $false
    if ($found -and !$runtimeRefs.Contains($found)) { $runtimeRefs.Add($found) }
}

$sourceFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs" -File | Sort-Object Name
$sharedClassifier = Join-Path $root 'shared\NearbyTargetClassifier.cs'
if (!(Test-Path $sharedClassifier)) { throw 'Shared classifier missing; build from the repository checkout' }
$sourceFiles = @($sourceFiles) + @(Get-Item $sharedClassifier)
if ($sourceFiles.Count -eq 0) { throw "No C# source files found: $sourceDir" }

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
if (Test-Path $outputDll) { Remove-Item $outputDll -Force }
if (Test-Path $outputPdb) { Remove-Item $outputPdb -Force }

$references = New-Object System.Collections.Generic.List[string]
foreach ($r in $runtimeRefs) { if (!$references.Contains($r)) { $references.Add($r) } }
foreach ($r in @($assembly, $harmony, $logLibrary, $unityCore)) { if (!$references.Contains($r)) { $references.Add($r) } }
if (Test-Path $unityEngine) { if (!$references.Contains($unityEngine)) { $references.Add($unityEngine) } }
if (Test-Path $firstPass) { if (!$references.Contains($firstPass)) { $references.Add($firstPass) } }

foreach ($reference in $references) {
    if (!(Test-Path $reference)) { throw "Reference not found: $reference" }
}

# Response file avoids Windows command-line quoting/length problems.
$rsp = Join-Path $env:TEMP ("DonChanTelemetryProbe-" + [Guid]::NewGuid().ToString("N") + ".rsp")
$diagnosticRsp = Join-Path $root "build-last.rsp"
$diagnosticEnv = Join-Path $root "build-env.txt"
try {
    $argsList = New-Object System.Collections.Generic.List[string]
    $argsList.Add("/nologo")
    $argsList.Add("/target:library")
    $argsList.Add("/platform:anycpu")
    $argsList.Add("/langversion:5")
    $argsList.Add("/warn:4")
    $argsList.Add("/nostdlib+")
    $argsList.Add("/out:`"$outputDll`"")
    if ($Configuration -eq "Release") {
        $argsList.Add("/optimize+")
    } else {
        $argsList.Add("/optimize-")
        $argsList.Add("/debug:pdbonly")
        $argsList.Add("/pdb:`"$outputPdb`"")
    }

    foreach ($reference in $references) { $argsList.Add("/reference:`"$reference`"") }
    foreach ($source in $sourceFiles) { $argsList.Add("`"$($source.FullName)`"") }

    [IO.File]::WriteAllLines($rsp, $argsList.ToArray(), (New-Object Text.UTF8Encoding($false)))

    $envLines = New-Object System.Collections.Generic.List[string]
    $envLines.Add("Compiler=$csc")
    $envLines.Add("Game=$Game7D2D")
    $envLines.Add("Managed=$managedDir")
    $envLines.Add("Harmony=$harmony")
    $envLines.Add("SearchDirs=")
    foreach ($d in $searchDirs) { $envLines.Add("  $d") }
    $envLines.Add("References=")
    foreach ($r in $references) { $envLines.Add("  $r") }
    [IO.File]::WriteAllLines($diagnosticEnv, $envLines.ToArray(), (New-Object Text.UTF8Encoding($false)))

    Write-Host "Compiler : $csc"
    Write-Host "Game     : $Game7D2D"
    Write-Host "StdLib   : $($runtimeRefs[0])"
    Write-Host "NetStd   : $($runtimeRefs | Where-Object { [IO.Path]::GetFileName($_) -ieq 'netstandard.dll' } | Select-Object -First 1)"
    Write-Host "Harmony  : $harmony"
    Write-Host "UnityCore: $unityCore"
    Write-Host "Building : $outputDll"

    # /noconfig must be passed on the csc.exe command line. If it is placed
    # inside the response file, Framework csc still loads its own csc.rsp and
    # injects Windows System/System.Core/System.Xml references, which collide
    # with the 7DTD/Unity runtime copies selected above.
    & $csc '/noconfig' ("@" + $rsp)
    $compilerExitCode = $LASTEXITCODE
    if ($compilerExitCode -ne 0) {
        Copy-Item $rsp $diagnosticRsp -Force
        Write-Host "Compiler arguments saved: $diagnosticRsp"
        Write-Host "Detected build environment saved: $diagnosticEnv"
        throw "Build failed (csc exit code $compilerExitCode)."
    }

    if (!(Test-Path $outputDll)) { throw "Compiler returned success but DLL was not created: $outputDll" }
    if (Test-Path $diagnosticRsp) { Remove-Item $diagnosticRsp -Force -ErrorAction SilentlyContinue }
    $size = (Get-Item $outputDll).Length
    Write-Host "Build succeeded: $outputDll ($size bytes)"
}
finally {
    if (Test-Path $rsp) { Remove-Item $rsp -Force -ErrorAction SilentlyContinue }
}


