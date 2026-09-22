# Build troubleshooting

## CS0012 System.Object / netstandard 2.1

Cause: current 7DTD/Unity assemblies target the Unity Mono/.NET Standard 2.1 profile, while the Windows Framework `csc.exe` normally injects the Windows .NET Framework `mscorlib.dll` and `System*.dll`. Mixing those profiles breaks type resolution.

Fix in v0.1.5: use `/nostdlib+` and reference the runtime assemblies shipped by the installed 7DTD build (`mscorlib.dll`, `netstandard.dll`, `System.dll`, `System.Core.dll`, `System.Xml*.dll`). The script searches `7DaysToDie_Data\Managed` and common `MonoBleedingEdge\lib\mono` locations automatically.

## CS1684 Span / ReadOnlySpan

In v0.1.4 these warnings were a symptom of the same mixed-runtime problem. v0.1.5 no longer intentionally references Windows Framework standard libraries, so these warnings should disappear or materially reduce. If they remain, keep `build-env.txt` and `build-last.rsp`; they show exactly which runtime profile was selected.

## CS0012 UnityEngine.Component

`UnityEngine.CoreModule.dll` is referenced explicitly from the installed game.

## CS0103 Log does not exist

`LogLibrary.dll` is referenced explicitly from the installed game.

## Diagnostics

On every build, `build-env.txt` records the detected runtime search directories and references. On compiler failure, `build-last.rsp` is also preserved with the exact compiler arguments.
