param(
    [string]$Project = ".\\SimpleWebcam\\SimpleWebcam.csproj",
    [string]$Output = ".\\artifacts\\package",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$outputPath = Resolve-Path $Output -ErrorAction SilentlyContinue
if (-not $outputPath) {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}

dotnet clean $Project
dotnet publish $Project -c Release -r $Runtime --self-contained true /p:PublishSingleFile=false /p:IncludeNativeLibrariesForSelfExtract=true

$publishDir = Join-Path "SimpleWebcam\\bin\\Release\\net8.0-windows" ("$Runtime\\publish")
if (-not (Test-Path $publishDir)) {
    throw "Publish directory not found: $publishDir"
}

$artifact = Join-Path $Output "SimpleWebcam-$Runtime"
if (Test-Path $artifact) {
    Remove-Item -Recurse -Force $artifact
}
New-Item -ItemType Directory -Path $artifact -Force | Out-Null
Copy-Item -Path "$publishDir\\*" -Destination $artifact -Recurse -Force

if (Test-Path "Store-Assets\\PrivacyPolicy.txt") {
    Copy-Item "Store-Assets\\PrivacyPolicy.txt" -Destination $artifact -Force
}
if (Test-Path "Store-Assets\\StoreListing.md") {
    Copy-Item "Store-Assets\\StoreListing.md" -Destination $artifact -Force
}
if (Test-Path "Store-Assets\\StoreSubmissionChecklist.md") {
    Copy-Item "Store-Assets\\StoreSubmissionChecklist.md" -Destination $artifact -Force
}

$zipPath = Join-Path $Output "SimpleWebcam-Windows-$Runtime.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$artifact\\*" -DestinationPath $zipPath

Write-Host "Package artifact: $zipPath"
