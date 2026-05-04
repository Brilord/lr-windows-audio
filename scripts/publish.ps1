param(
    [switch]$Installer
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "BalanceDock.csproj"
$publishDir = Join-Path $repoRoot "bin\Release\net8.0-windows\win-x64\publish"
$installerScript = Join-Path $repoRoot "installer\BalanceDock.iss"

Write-Host "Stopping running BalanceDock instances..."
Get-Process BalanceDock -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Publishing portable release..."
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

Write-Host "Portable EXE:"
Write-Host (Join-Path $publishDir "BalanceDock.exe")

if ($Installer) {
    $iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if (-not $iscc) {
        throw "Inno Setup compiler ISCC.exe was not found on PATH. Install Inno Setup, then rerun scripts\publish.ps1 -Installer."
    }

    Write-Host "Building installer..."
    & $iscc.Source $installerScript
}
