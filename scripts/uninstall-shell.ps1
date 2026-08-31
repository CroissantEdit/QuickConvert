param([switch]$SkipExplorerRestart)

$ErrorActionPreference = 'Stop'
$packageName = 'QuickConvert.Desktop'
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'QuickConvert' -ErrorAction SilentlyContinue
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

Get-AppxPackage -Name $packageName | Remove-AppxPackage
if (Get-AppxPackage -Name $packageName) { throw "Package still installed: $packageName" }

if (-not $SkipExplorerRestart) { Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue }
Write-Host "Uninstalled $packageName"
