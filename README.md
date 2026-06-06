# Simple Webcam for Windows

Simple Webcam is now a practical WPF utility for:
- Detecting available camera devices
- Live preview from the selected webcam
- One-shot and timed snapshots
- Capture format/quality control and auto-start settings
- First-run diagnostics for camera troubleshooting
- Local output folder organization and quick gallery access

This is a Microsoft Store-oriented Windows desktop app scaffold for $(System.Collections.Hashtable.Title). It uses .NET 8 and WPF, keeps the first implementation local-first, and includes a repo-root `Store-Assets` folder for listing and privacy handoff material.

## Current feature set

- `Refresh Cameras` scans connected camera indices and builds a device list.
- `Start Preview` opens a live preview loop and updates the center video panel.
- `Capture Snapshot` saves JPG or PNG based on chosen format into `Pictures\Simple Webcam`.
- `Image quality` controls encoding quality for faster or higher-quality output.
- `Auto-start preview` persists across launches.
- `Timed capture` repeatedly saves snapshots every N seconds while preview is active.
- `Run Diagnostics` verifies camera-device health and writes step-by-step status to the activity log.
- Gallery list shows captured files from the session and disk.
- Output folder can be opened directly from the app.

## Build

```powershell
dotnet build .\SimpleWebcam\SimpleWebcam.csproj -c Release
```

## Run

```powershell
& .\SimpleWebcam\bin\Release\net8.0-windows\SimpleWebcam.exe
```

## Packaging helper

```powershell
.\packaging\Create-WindowsPackage.ps1
```

That helper creates:
- `artifacts/package/SimpleWebcam-win-x64` folder with published output
- `artifacts/package/SimpleWebcam-Windows-win-x64.zip` archive

## Store workflow (MSIX + identity)

1. Reserve your name in Partner Center and set identity values in:
   - `SimpleWebcam/store/manifest.config.json`

2. Update manifest placeholders in:
   - `SimpleWebcam/Package.appxmanifest`

3. Build Store handoff artifacts:

```powershell
powershell -ExecutionPolicy Bypass -File .\packaging\Create-MsixStoreArtifact.ps1
```

That helper produces:
- `artifacts\msix\SimpleWebcam_<version>_<identity>.msix` (if Windows SDK tools are installed)
- `artifacts\msix\SimpleWebcam-StoreBundle-<timestamp>.zip` (submission folder + docs + manifest payload)

## Store notes

Before final packaging, reserve the exact Microsoft Store product name in Partner Center and update package identity values to match that reservation.
