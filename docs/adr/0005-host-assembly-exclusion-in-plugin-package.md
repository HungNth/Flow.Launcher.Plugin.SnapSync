# Exclude Flow Launcher Host Assemblies from Release Package

## Context
Flow.Launcher.Plugin 5.3.2 targets `net9.0-windows10.0.19041`. Publishing for `win-x64` includes Windows Runtime projection assemblies (`Microsoft.Windows.SDK.NET.dll` ~25MB and `WinRT.Runtime.dll`), as well as `Flow.Launcher.Plugin.dll`.

However, Flow Launcher executes plugins in-process and bundles these assemblies in its application root. Distributing redundant copies inflates the release archive from ~250KB to >25MB and strains CDN distribution.

## Decision
Retain `TargetFramework=net9.0-windows10.0.19041` in `Flow.Launcher.Plugin.SnapSync.csproj` to satisfy package requirements, but explicitly exclude host-provided runtime assemblies (`Microsoft.Windows.SDK.NET.dll` and `Flow.Launcher.Plugin.dll`) during the release packaging phase before generating `Flow.Launcher.Plugin.SnapSync.zip`.

## Consequences
- Archive size reduces by ~99% (~250KB total).
- Package installation remains fast and bandwidth-friendly.
- Flow Launcher hosts and resolves all runtime SDK types in-process without version conflicts.
