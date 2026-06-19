$installDir = "$env:ProgramFiles\ReSume"
if (Test-Path $installDir) { Remove-Item -Recurse -Force $installDir }
Remove-Item "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.resume.nativehost" -ErrorAction SilentlyContinue
Remove-Item "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.resume.nativehost" -ErrorAction SilentlyContinue
$shortcutDir = [Environment]::GetFolderPath('CommonStartMenu') + "\Programs\ReSume"
if (Test-Path $shortcutDir) { Remove-Item -Recurse -Force $shortcutDir }
Write-Host "Uninstalled."