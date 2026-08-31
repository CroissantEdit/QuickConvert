$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot\ShellCleanup.psm1" -Force

Remove-QuickConvertLegacyShellEntries `
    -ClassesRoot 'HKLM:\Software\Classes' `
    -CommandStoreRoot 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell'
