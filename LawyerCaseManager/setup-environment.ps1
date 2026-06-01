# LawyerCaseManager — one-time environment setup for Windows
# Run in PowerShell:  .\setup-environment.ps1

$ErrorActionPreference = "Stop"
$dotnetRoot = "C:\Program Files\dotnet"

if (-not (Test-Path "$dotnetRoot\dotnet.exe")) {
    Write-Host "Installing .NET 8 SDK via winget..."
    winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
}

$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$dotnetRoot*") {
    $newPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $dotnetRoot } else { "$dotnetRoot;$userPath" }
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Write-Host "Added to User PATH: $dotnetRoot"
}

[Environment]::SetEnvironmentVariable("DOTNET_ROOT", $dotnetRoot, "User")
$env:DOTNET_ROOT = $dotnetRoot
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")

& "$dotnetRoot\dotnet.exe" --version
Write-Host "Environment ready. Restart Cursor/terminals if 'dotnet' is still not found."
