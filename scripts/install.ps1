param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Write-Host "Building..."
Push-Location $root
dotnet publish src\ReSume\ReSume.csproj -c $Configuration -o src\ReSume\bin\Release\net8.0-windows\publish
dotnet publish src\ReSume.NativeHost\ReSume.NativeHost.csproj -c $Configuration -o src\ReSume.NativeHost\bin\Release\net8.0\publish
Pop-Location
$installDir = "$env:ProgramFiles\ReSume"
$nativeHostDir = "$installDir\NativeHost"
New-Item -ItemType Directory -Force -Path $installDir, $nativeHostDir | Out-Null
Copy-Item "$root\src\ReSume\bin\Release\net8.0-windows\publish\*" -Destination $installDir -Recurse -Force
Copy-Item "$root\src\ReSume.NativeHost\bin\Release\net8.0\publish\*" -Destination $nativeHostDir -Recurse -Force
Copy-Item "$root\src\ReSume.Installer\nativehost-manifest.json" $installDir
Copy-Item "$root\src\ReSume.Installer\nativehost-manifest-edge.json" $installDir
$regChrome = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.resume.nativehost"
New-Item -Path $regChrome -Force | Out-Null
Set-ItemProperty -Path $regChrome -Name "(Default)" -Value "$installDir\nativehost-manifest.json"
$regEdge = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.resume.nativehost"
New-Item -Path $regEdge -Force | Out-Null
Set-ItemProperty -Path $regEdge -Name "(Default)" -Value "$installDir\nativehost-manifest-edge.json"
$shortcutDir = [Environment]::GetFolderPath('CommonStartMenu') + "\Programs\ReSume"
New-Item -ItemType Directory -Force -Path $shortcutDir | Out-Null
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$shortcutDir\ReSume.lnk")
$Shortcut.TargetPath = "$installDir\ReSume.exe"
$Shortcut.Save()
Write-Host "Installed. Load extension from extensions/chrome or extensions/edge (Developer mode) and update manifest IDs."