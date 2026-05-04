param(
    [int]$LaunchSeconds = 5
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "BalanceDock.sln"
$project = Join-Path $repoRoot "BalanceDock.csproj"
$publishDir = Join-Path $repoRoot "bin\Release\net8.0-windows\win-x64\publish"
$exe = Join-Path $publishDir "BalanceDock.exe"
$logPath = Join-Path $env:LOCALAPPDATA "BalanceDock\logs\balancedock.log"

function Stop-BalanceDock {
    Get-Process BalanceDock -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Assert-FileExists($path, $message) {
    if (-not (Test-Path $path)) {
        throw $message
    }
}

Write-Host "Stopping existing BalanceDock instances..."
Stop-BalanceDock

Write-Host "Building Debug..."
dotnet build $solution

Write-Host "Building Release..."
dotnet build $solution -c Release

Write-Host "Publishing portable release..."
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

Assert-FileExists $exe "Published EXE was not found at $exe"
Assert-FileExists (Join-Path $publishDir "Assets\BalanceDock.ico") "Published icon asset was not found."

Write-Host "Launching published app in tray mode..."
$process = Start-Process -FilePath $exe -ArgumentList "--tray" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds $LaunchSeconds

$running = Get-Process BalanceDock -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exe }

if (-not $running) {
    if ($process.HasExited) {
        throw "BalanceDock exited early with code $($process.ExitCode)."
    }

    throw "BalanceDock did not stay running after launch."
}

Write-Host "Checking single-instance behavior..."
$secondProcess = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
$null = $secondProcess.WaitForExit(5000)
$runningAfterSecondLaunch = @(Get-Process BalanceDock -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exe })

if ($runningAfterSecondLaunch.Count -ne 1) {
    throw "Expected one BalanceDock instance after second launch, found $($runningAfterSecondLaunch.Count)."
}

Assert-FileExists $logPath "Expected log file was not created at $logPath"

$recentLog = Get-Content $logPath -Tail 20 -ErrorAction Stop
if (-not ($recentLog -match "BalanceDock starting")) {
    throw "Log file exists, but no recent startup entry was found."
}

Write-Host "Stopping launched app..."
Stop-BalanceDock

Write-Host ""
Write-Host "Smoke test passed."
Write-Host "Published EXE: $exe"
Write-Host "Log file: $logPath"
