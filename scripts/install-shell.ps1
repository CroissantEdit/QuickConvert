param(
    [string]$PackagePath,
    [string]$ExternalLocation,
    [switch]$SkipExplorerRestart
)

$ErrorActionPreference = 'Stop'
$packageName = 'QuickConvert.Desktop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$adjacentPackage = Join-Path $PSScriptRoot 'QuickConvert.Identity.msix'

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'QuickConvert' -ErrorAction SilentlyContinue

if (-not $PackagePath) {
    $PackagePath = if (Test-Path -LiteralPath $adjacentPackage) {
        $adjacentPackage
    } else {
        Join-Path $root 'artifacts\QuickConvert\QuickConvert.Identity.msix'
    }
}
if (-not $ExternalLocation) {
    $ExternalLocation = if (Test-Path -LiteralPath $adjacentPackage) {
        $PSScriptRoot
    } else {
        Join-Path $root 'artifacts\QuickConvert'
    }
}

$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$ExternalLocation = (Resolve-Path -LiteralPath $ExternalLocation).Path
Import-Module "$PSScriptRoot\ShellCleanup.psm1" -Force

Remove-QuickConvertLegacyShellEntries `
    -ClassesRoot 'HKCU:\Software\Classes' `
    -CommandStoreRoot 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell'

$machineEntries = @(Get-QuickConvertLegacyShellEntries `
    -ClassesRoot 'HKLM:\Software\Classes' `
    -CommandStoreRoot 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell')
if ($machineEntries.Count) {
    throw 'Machine-wide legacy entries remain. Run cleanup-machine-shell.ps1 from an Administrator PowerShell, then retry.'
}

foreach ($file in @('QuickConvert.exe', 'QuickConvert.Shell.dll')) {
    $signature = Get-AuthenticodeSignature (Join-Path $ExternalLocation $file)
    if ($signature.Status -ne 'Valid') { throw "Invalid signature: $file ($($signature.Status))" }
}

Get-AppxPackage -Name $packageName | Remove-AppxPackage
Add-AppxPackage -Path $PackagePath -ExternalLocation $ExternalLocation

$packages = @(Get-AppxPackage -Name $packageName)
if ($packages.Count -ne 1 -or $packages[0].Status -ne 'Ok') {
    throw "Package registration verification failed for $packageName."
}

$legacy = @(Get-QuickConvertLegacyShellEntries `
    -ClassesRoot 'HKCU:\Software\Classes' `
    -CommandStoreRoot 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell')
if ($legacy.Count) { throw "Legacy registrations remain: $($legacy -join ', ')" }

if (-not $SkipExplorerRestart) { Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue }
Write-Host "Installed $($packages[0].PackageFullName)"
