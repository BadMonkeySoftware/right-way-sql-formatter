# Removes the Right Way SQL Formatter extension from SSMS 18.
# Usage:  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
param(
    [string]$SsmsRoot = "C:\Program Files (x86)\Microsoft SQL Server Management Studio 18"
)

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`" -SsmsRoot `"$SsmsRoot`""
    exit
}

$ssmsExe = Join-Path $SsmsRoot 'Common7\IDE\Ssms.exe'
$dest = Join-Path $SsmsRoot 'Common7\IDE\Extensions\RightWaySqlFormatter'

if (Get-Process Ssms -ErrorAction SilentlyContinue) {
    Write-Error "SSMS is running. Close all SSMS windows and re-run."
}

if (Test-Path $dest) {
    Remove-Item -Recurse -Force $dest
    Write-Host "Extension files removed."
} else {
    Write-Host "Extension folder not found ($dest) - nothing to remove."
}

if (Test-Path $ssmsExe) {
    Write-Host "Rebuilding the SSMS package cache..."
    Start-Process -FilePath $ssmsExe -ArgumentList '/setup' -Wait
}

Write-Host "Done."
