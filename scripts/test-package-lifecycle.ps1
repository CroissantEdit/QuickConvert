$ErrorActionPreference = 'Stop'
$packageName = 'QuickConvert.Desktop'
$install = Join-Path $PSScriptRoot 'install-shell.ps1'
$uninstall = Join-Path $PSScriptRoot 'uninstall-shell.ps1'
$executable = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) 'artifacts\QuickConvert\QuickConvert.exe'

function Invoke-QuickConvert([string]$Argument) {
    $process = Start-Process -FilePath $executable -ArgumentList $Argument -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "QuickConvert $Argument failed with exit code $($process.ExitCode)." }
}

& $install -SkipExplorerRestart
& $install -SkipExplorerRestart
$packages = @(Get-AppxPackage -Name $packageName)
if ($packages.Count -ne 1) { throw "Expected one package, found $($packages.Count)." }
if ($packages[0].Status -ne 'Ok') { throw "Package status: $($packages[0].Status)" }
if ($packages[0].Version -ne [version]'0.6.0.0') { throw "Package version: $($packages[0].Version)" }

$categories = @((Get-AppxPackageManifest -Package $packages[0]).Package.Applications.Application.Extensions.Extension |
    ForEach-Object Category)
if ('windows.comServer' -notin $categories -or 'windows.fileExplorerContextMenus' -notin $categories) {
    throw 'Modern Explorer package extensions are missing.'
}
if ($categories | Where-Object { $_ -match 'fileTypeAssociation' }) { throw 'Unexpected file association registration.' }

$type = [type]::GetTypeFromCLSID([guid]'4FEE7C82-2A07-4F48-BA44-7F4B294CA79C', $true)
$instance = [Activator]::CreateInstance($type)
if (-not $instance) { throw 'Packaged COM activation returned null.' }
[Runtime.InteropServices.Marshal]::ReleaseComObject($instance) | Out-Null

foreach ($file in @('QuickConvert.exe', 'QuickConvert.Shell.dll')) {
    $signature = Get-AuthenticodeSignature (Join-Path (Split-Path $executable) $file)
    if ($signature.Status -ne 'Valid') { throw "Invalid signature: $file ($($signature.Status))" }
}

Import-Module "$PSScriptRoot\ShellCleanup.psm1" -Force
$legacy = @(Get-QuickConvertLegacyShellEntries `
    -ClassesRoot 'HKCU:\Software\Classes' `
    -CommandStoreRoot 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell')
$legacy += @(Get-QuickConvertLegacyShellEntries `
    -ClassesRoot 'HKLM:\Software\Classes' `
    -CommandStoreRoot 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell')
if ($legacy.Count) { throw "Legacy registrations remain: $($legacy -join ', ')" }

& $uninstall -SkipExplorerRestart
& $uninstall -SkipExplorerRestart
if (Get-AppxPackage -Name $packageName) { throw 'Package still installed.' }

try {
    Invoke-QuickConvert '--install'
    if (-not (Get-AppxPackage -Name $packageName)) { throw 'QuickConvert --install did not register the package.' }
    Invoke-QuickConvert '--uninstall'
    if (Get-AppxPackage -Name $packageName) { throw 'QuickConvert --uninstall left the package installed.' }
}
finally {
    & $uninstall -SkipExplorerRestart
}

Write-Host '[PASS] Idempotent package install and uninstall'
