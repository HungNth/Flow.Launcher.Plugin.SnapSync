# Standalone .NET 9 synchronization core

SnapSync keeps synchronization and configuration validation in a standalone `SnapSync.Core` library targeting `net9.0`, without Flow Launcher or WPF dependencies. The Windows Flow/WPF adapter owns launcher interaction and settings presentation; real temporary-filesystem tests exercise the core's public operations, preserving reuse for a future CLI without introducing a CLI now.
