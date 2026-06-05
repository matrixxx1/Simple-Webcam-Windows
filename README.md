# Simple Webcam for Windows

Preview camera devices and capture local snapshots.

This is the initial Microsoft Store-oriented Windows desktop app scaffold for $(System.Collections.Hashtable.Title). It uses .NET 8 and WPF, keeps the first implementation local-first, and includes a repo-root Store-Assets folder for listing and privacy handoff material.

## Initial scope

- Camera selection surface
- Preview/capture workflow
- Device setting notes
- Local media workflow

## Build

``powershell
dotnet build .\SimpleWebcam\SimpleWebcam.csproj -c Release
``

## Store notes

Before final packaging, reserve the exact Microsoft Store product name in Partner Center and update package identity values to match that reservation.