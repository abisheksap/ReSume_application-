<#
.SYNOPSIS
    Registers the Chrome/Edge native messaging host for local development.
.DESCRIPTION
    Run this script after loading the ReSume extension in Chrome (chrome://extensions -> Load unpacked -> extensions/chrome).
    It will ask for the extension ID, then create the manifest and registry key.
#>

$ErrorActionPreference = "Stop"

# Detect project root (where this script is saved)
$projectRoot = Split-Path $PSCommandPath -Parent
$nativeHostExe = Join-Path $projectRoot "src\ReSume.NativeHost\bin\Debug\net8.0\ReSume.NativeHost.exe"

if (-not (Test-Path $nativeHostExe)) {
    Write-Host "NativeHost executable not found. Please build the solution first:" -ForegroundColor Red
    Write-Host "  dotnet build" -ForegroundColor Yellow
    exit 1
}

Write-Host "NativeHost found: $nativeHostExe" -ForegroundColor Green

# Ask for extension ID
$extId = Read-Host "Enter your Chrome extension ID (from chrome://extensions)"
if ([string]::IsNullOrWhiteSpace($extId)) {
    Write-Host "Extension ID is required." -ForegroundColor Red
    exit 1
}

# Create manifest
$manifest = @{
    name = "com.resume.nativehost"
    description = "ReSume Native Messaging Host (Local Dev)"
    path = $nativeHostExe
    type = "stdio"
    allowed_origins = @("chrome-extension://$extId/")
}

$manifestDir = Join-Path $projectRoot "local-nativehost"
New-Item -ItemType Directory -Force -Path $manifestDir | Out-Null
$manifestPath = Join-Path $manifestDir "nativehost-manifest.json"
$manifest | ConvertTo-Json -Depth 3 | Out-File -Encoding utf8 -FilePath $manifestPath
Write-Host "Manifest written to: $manifestPath" -ForegroundColor Green

# Register for Chrome
$regPath = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.resume.nativehost"
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name "(Default)" -Value $manifestPath
Write-Host "Chrome native host registered." -ForegroundColor Green

# Optionally register for Edge
$edgeExtId = Read-Host "Enter Edge extension ID (or press Enter to skip)"
if ($edgeExtId) {
    $edgeManifest = @{
        name = "com.resume.nativehost"
        description = "ReSume Native Messaging Host (Local Dev - Edge)"
        path = $nativeHostExe
        type = "stdio"
        allowed_origins = @("extension://$edgeExtId/")
    }
    $edgeManifestPath = Join-Path $manifestDir "nativehost-manifest-edge.json"
    $edgeManifest | ConvertTo-Json -Depth 3 | Out-File -Encoding utf8 -FilePath $edgeManifestPath
    $edgeRegPath = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.resume.nativehost"
    New-Item -Path $edgeRegPath -Force | Out-Null
    Set-ItemProperty -Path $edgeRegPath -Name "(Default)" -Value $edgeManifestPath
    Write-Host "Edge native host registered." -ForegroundColor Green
}

Write-Host "`nDone! Now:"
Write-Host "1. Start ReSume.exe (if not already running)."
Write-Host "2. In Chrome, open the extension popup and click 'Save Now'."
Write-Host "3. If nothing happens, go to chrome://extensions, click 'Inspect views: service worker' and look for errors."