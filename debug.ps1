$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$projectFile = Join-Path $scriptDir "Flow.Launcher.Plugin.SnapSync.csproj"
$publishDir = Join-Path $scriptDir "bin\Debug\win-x64\publish"

Write-Host "Publishing plugin..." -ForegroundColor Cyan
dotnet publish $projectFile -c Debug -r win-x64 --no-self-contained

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Exclude host-provided assemblies per ADR 0005
$excludedFiles = @(
    Join-Path $publishDir "Microsoft.Windows.SDK.NET.dll"
    Join-Path $publishDir "Flow.Launcher.Plugin.dll"
)
Remove-Item -Path $excludedFiles -Force -ErrorAction SilentlyContinue

$appDataFolder = [Environment]::GetFolderPath("ApplicationData")
$flowLauncherExe = "$env:LOCALAPPDATA\FlowLauncher\Flow.Launcher.exe"

if (Test-Path $flowLauncherExe) {
    Write-Host "Stopping Flow.Launcher..." -ForegroundColor Cyan
    Stop-Process -Name "Flow.Launcher" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    $targetPluginDir = Join-Path $appDataFolder "FlowLauncher\Plugins\Flow.Launcher.Plugin.SnapSync"
    if (Test-Path $targetPluginDir) {
        Write-Host "Removing existing plugin folder..." -ForegroundColor Cyan
        Remove-Item -Recurse -Force $targetPluginDir
    }

    Write-Host "Deploying new build to $targetPluginDir..." -ForegroundColor Cyan
    Copy-Item $publishDir $targetPluginDir -Recurse -Force

    Start-Sleep -Seconds 1
    Write-Host "Restarting Flow.Launcher..." -ForegroundColor Green
    Start-Process $flowLauncherExe
} else {
    Write-Warning "Flow.Launcher.exe not found at '$flowLauncherExe'. Please ensure Flow Launcher is installed."
}
