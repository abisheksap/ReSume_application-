param([string]$Configuration = "Release")
Push-Location (Split-Path $PSScriptRoot -Parent)
dotnet restore
dotnet build -c $Configuration
Pop-Location