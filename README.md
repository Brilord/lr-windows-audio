# BalanceDock

BalanceDock is a Windows tray app for quickly changing left/right audio balance.

It targets the current default Windows output device. When Windows exposes real endpoint left/right controls, BalanceDock applies the balance at the device level. If the device does not expose those controls, BalanceDock falls back to changing the left/right channel volume of active app audio sessions.

## Features

- One large left/right balance slider.
- Exact `L` and `R` percentage labels.
- Reset button for `50% / 50%`.
- Shows the active default output device.
- Runs from the Windows system tray.
- Saves the last balance and reapplies it on launch.
- Watches for output-device changes.
- Start with Windows option.
- Global hotkeys:
  - `Ctrl + Alt + Left`: move balance left
  - `Ctrl + Alt + Right`: move balance right
  - `Ctrl + Alt + Down`: reset center

## How Balance Works

BalanceDock tries two methods:

1. Endpoint balance

   This is the best path. It changes the left/right channel volume on the default Windows output device itself, so audio routed through that device is affected system-wide.

2. Per-app session fallback

   If the device driver does not expose endpoint left/right channels, BalanceDock applies balance to active app audio sessions on the default output device. While this fallback is active, BalanceDock reapplies it every few seconds so newly-created audio sessions can pick it up.

Some audio paths still cannot be forced by a normal desktop app. Examples include exclusive-mode audio, unusual Bluetooth/USB drivers, DRM-protected audio paths, and driver-level audio paths. Covering every possible stream would require a virtual audio driver or system audio APO.

## Requirements

- Windows 10 or Windows 11
- .NET SDK to build from source

Check .NET:

```powershell
dotnet --info
```

## Run From Source

From this repo:

```powershell
dotnet run --project BalanceDock.csproj
```

## Fast Build EXE

Use this while developing:

```powershell
dotnet build BalanceDock.sln -c Release
```

The fast build EXE will be here:

```text
bin\Release\net8.0-windows\BalanceDock.exe
```

This is the quickest way to get an EXE, but it requires the .NET Desktop Runtime to be installed on the target machine.

Important: close BalanceDock before rebuilding. If `BalanceDock.exe` is still running in the tray, Windows may lock the output files and the build can fail.

## Portable Release EXE

Run this from the repo root:

```powershell
dotnet publish BalanceDock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The EXE will be here:

```text
bin\Release\net8.0-windows\win-x64\publish\BalanceDock.exe
```

This is a portable self-contained EXE. It is not an installer.

To use it like a normal app, put the published files somewhere permanent, for example:

```text
%LOCALAPPDATA%\Programs\BalanceDock\
```

Then run:

```text
%LOCALAPPDATA%\Programs\BalanceDock\BalanceDock.exe
```

If you enable Start with Windows, keep the EXE in the same location. If you move it later, disable and re-enable Start with Windows.

The portable release build is slower than the fast build because it bundles the .NET runtime into the app output. Use it when you want to share the app with another Windows machine.

## Start Hidden In Tray

```powershell
dotnet run --project BalanceDock.csproj -- --tray
```

The Start with Windows option launches BalanceDock with `--tray`.

## Settings

Settings file:

```text
%LOCALAPPDATA%\BalanceDock\settings.json
```

Startup registry entry:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BalanceDock
```

## Build Check

```powershell
dotnet build BalanceDock.sln
```

## Manual Test Checklist

- Move the slider left and right.
- Press Reset.
- Test the global hotkeys.
- Switch the Windows default output device while BalanceDock is running.
- Test at least one stereo output device.
- Test a device that does not expose endpoint L/R channels and confirm the per-app fallback message appears.
