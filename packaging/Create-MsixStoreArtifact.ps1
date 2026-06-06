param(
    [string]$Project = ".\\SimpleWebcam\\SimpleWebcam.csproj",
    [string]$Runtime = "win-x64",
    [string]$ConfigName = ".\\SimpleWebcam\\store\\manifest.config.json",
    [string]$MsixOutput = ".\\artifacts\\msix"
)

$ErrorActionPreference = "Stop"

function Resolve-SdkTool([string]$toolName) {
    $patterns = @(
        "$env:ProgramFiles\Windows Kits\10\bin\10.0*\x64\$toolName",
        "$env:ProgramFiles(x86)\Windows Kits\10\bin\10.0*\x64\$toolName"
    )

    foreach ($pattern in $patterns) {
        $match = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    return $null
}

$config = @{
    IdentityName = "SimpleWebcam"
    Publisher = "CN=SimpleWebcamDev"
    Version = "0.1.0.0"
}

if (Test-Path $ConfigName) {
    try {
        $parsed = Get-Content -Raw $ConfigName | ConvertFrom-Json
        if ($parsed -and $parsed.PSObject.Properties.Name.Count -gt 0) {
            $config = @{}
            foreach ($p in $parsed.PSObject.Properties) {
                $config[$p.Name] = $p.Value
            }
        } else {
            Write-Host "Could not parse $ConfigName. Using defaults."
        }
    } catch {
        Write-Host "Could not parse $ConfigName. Using defaults."
    }
}

$identityName = $config["IdentityName"]
$publisher = $config["Publisher"]
$version = $config["Version"]

if (-not $identityName) { $identityName = "SimpleWebcam" }
if (-not $publisher) { $publisher = "CN=SimpleWebcamDev" }
if (-not $version) { $version = "0.1.0.0" }

New-Item -ItemType Directory -Path $MsixOutput -Force | Out-Null
New-Item -ItemType Directory -Path ".\\artifacts\\package" -Force | Out-Null

$publishDir = Join-Path ".\\SimpleWebcam\\bin\\Release\\net8.0-windows" ("$Runtime\\publish")
dotnet publish $Project -c Release -r $Runtime --self-contained true /p:PublishSingleFile=false /p:IncludeNativeLibrariesForSelfExtract=true /p:Version=$version | Out-Host

if (-not (Test-Path $publishDir)) {
    throw "Publish output was not found: $publishDir"
}

$storeFolder = ".\\artifacts\\package\\SimpleWebcam-$Runtime\\store"
if (Test-Path $storeFolder) { Remove-Item -Recurse -Force $storeFolder }
New-Item -ItemType Directory -Path $storeFolder -Force | Out-Null

$template = Get-Content -Raw ".\\SimpleWebcam\\Package.appxmanifest"
$manifest = $template `
    -replace 'Name="[^"]+"', "Name=""$identityName""" `
    -replace 'Publisher="[^"]+"', "Publisher=""$publisher""" `
    -replace 'Version="[^"]+"', "Version=""$version"""
$manifestPath = Join-Path $storeFolder "AppxManifest.xml"
Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

$msixRoot = Join-Path $MsixOutput "SimpleWebcam_$($version)_$identityName"
if (Test-Path $msixRoot) { Remove-Item -Recurse -Force $msixRoot }
New-Item -ItemType Directory -Path $msixRoot -Force | Out-Null

Copy-Item -Path "$publishDir\\*" -Destination $msixRoot -Recurse -Force
Copy-Item -Path $manifestPath -Destination (Join-Path $msixRoot "AppxManifest.xml") -Force

$assetsPath = Join-Path $msixRoot "Assets"
New-Item -ItemType Directory -Path $assetsPath -Force | Out-Null

Add-Type -AssemblyName System.Drawing
$logoSpecs = @(
    @{ Name = "Square44x44Logo.png"; Width = 44; Height = 44 },
    @{ Name = "Square150x150Logo.png"; Width = 150; Height = 150 },
    @{ Name = "Wide310x150Logo.png"; Width = 310; Height = 150 },
    @{ Name = "LargeTile.png"; Width = 310; Height = 310 },
    @{ Name = "StoreLogo.png"; Width = 300; Height = 300 }
)

foreach ($entry in $logoSpecs) {
    $bmp = New-Object System.Drawing.Bitmap $entry.Width, $entry.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(219, 39, 119))
    $g.Dispose()
    $out = Join-Path $assetsPath $entry.Name
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

foreach ($f in @("PrivacyPolicy.txt","StoreListing.md","StoreSubmissionChecklist.md")) {
    if (Test-Path "Store-Assets\\$f") {
        Copy-Item "Store-Assets\\$f" -Destination (Join-Path $msixRoot $f) -Force
    }
}

$makeAppx = Resolve-SdkTool "makeappx.exe"
$signTool = Resolve-SdkTool "signtool.exe"
$msixFile = Join-Path $MsixOutput "SimpleWebcam_$($version)_$identityName.msix"

if ($makeAppx) {
    & $makeAppx pack /d $msixRoot /p $msixFile /o | Out-Host
    Write-Host "MSIX output: $msixFile"
} else {
    Write-Host "Windows SDK not detected (makeappx.exe missing). Skipping .msix creation."
}

if ($signTool -and (Test-Path $msixFile)) {
    $thumb = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -match [regex]::Escape($publisher.Replace("CN=", "")) } | Select-Object -First 1
    if ($thumb) {
        & $signTool sign /fd SHA256 /a /n $($thumb.Subject.Split("CN=")[1]) $msixFile | Out-Host
        Write-Host "Signed MSIX using cert: $($thumb.Thumbprint)"
    } else {
        Write-Host "No local certificate for $publisher. Leave unsigned for local testing."
    }
}

$submissionZip = Join-Path $MsixOutput "SimpleWebcam-StoreBundle-$((Get-Date).ToString('yyyyMMdd-HHmmss')).zip"
Compress-Archive -Path "$msixRoot\\*" -DestinationPath $submissionZip -Force
Write-Host "Submission bundle: $submissionZip"
