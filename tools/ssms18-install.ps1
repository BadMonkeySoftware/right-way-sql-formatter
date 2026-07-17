# Installs the Right Way SQL Formatter extension into SSMS 18 (also SSMS 17/19/20-era shells).
# Usage:  powershell -ExecutionPolicy Bypass -File .\install.ps1
#         powershell -ExecutionPolicy Bypass -File .\install.ps1 -SsmsRoot "C:\Program Files (x86)\Microsoft SQL Server Management Studio 18"
# Requires admin (self-elevates): SSMS 18 lives under Program Files and needs `Ssms.exe /setup`.
param(
    [string]$SsmsRoot = "C:\Program Files (x86)\Microsoft SQL Server Management Studio 18"
)

$ErrorActionPreference = 'Stop'

# Self-elevate
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting administrator rights..."
    Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`" -SsmsRoot `"$SsmsRoot`""
    exit
}

$ssmsExe = Join-Path $SsmsRoot 'Common7\IDE\Ssms.exe'
if (-not (Test-Path $ssmsExe)) {
    Write-Error "SSMS not found at '$ssmsExe'. Pass -SsmsRoot pointing at your SSMS 18 install folder."
}

$vsix = Get-ChildItem -Path $PSScriptRoot -Filter 'RightWaySqlFormatter.SSMS18.vsix' | Select-Object -First 1
if (-not $vsix) { Write-Error "RightWaySqlFormatter.SSMS18.vsix not found next to this script." }

if (Get-Process Ssms -ErrorAction SilentlyContinue) {
    Write-Error "SSMS is running. Close all SSMS windows (any version) and re-run."
}

$dest = Join-Path $SsmsRoot 'Common7\IDE\Extensions\RightWaySqlFormatter'
if (Test-Path $dest) {
    Write-Host "Removing previous installation..."
    Remove-Item -Recurse -Force $dest
}

# A .vsix is a zip: extract its payload into the Extensions folder
Write-Host "Extracting extension to $dest ..."
$tmpZip = Join-Path $env:TEMP 'RightWaySqlFormatter.SSMS18.zip'
Copy-Item $vsix.FullName $tmpZip -Force
Expand-Archive -Path $tmpZip -DestinationPath $dest -Force
Remove-Item $tmpZip -Force

Write-Host "Rebuilding the SSMS package cache (Ssms.exe /setup) - this can take a minute..."
Start-Process -FilePath $ssmsExe -ArgumentList '/setup' -Wait

Write-Host ""
Write-Host "Done. Start SSMS 18 - 'Format T-SQL Code (Right Way)' appears in the Tools menu (Ctrl+K, F)."
