param([switch]$SkipExplorerRestart)

$ErrorActionPreference = 'Stop'

function Test-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
    if ($SkipExplorerRestart) { $arguments += '-SkipExplorerRestart' }
    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
}

$certificatePath = Join-Path $PSScriptRoot 'QuickConvert.cer'
$packagePath = Join-Path $PSScriptRoot 'QuickConvert.Identity.msix'
if (-not (Test-Path -LiteralPath $certificatePath) -or -not (Test-Path -LiteralPath $packagePath)) {
    throw 'This folder is not a complete QuickConvert release.'
}

$certificate = Get-PfxCertificate -FilePath $certificatePath
if ($certificate.Subject -ne 'CN=QuickConvert Development') {
    throw "Unexpected package certificate: $($certificate.Subject)"
}
if (-not (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $certificate.Thumbprint)) {
    Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
}

$sourceRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$installRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'QuickConvert\app'))
$localAppData = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $installRoot.StartsWith($localAppData, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to replace an unexpected install path.'
}

if (-not [string]::Equals($sourceRoot, $installRoot, [StringComparison]::OrdinalIgnoreCase)) {
    if (Get-AppxPackage -Name 'QuickConvert.Desktop') {
        $uninstallArguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $sourceRoot 'uninstall-shell.ps1'), '-SkipExplorerRestart')
        & powershell.exe @uninstallArguments
        if ($LASTEXITCODE -ne 0) { throw "QuickConvert removal failed with exit code $LASTEXITCODE." }
    }

    if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $sourceRoot '*') -Destination $installRoot -Recurse -Force
}

$arguments = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $installRoot 'install-shell.ps1'))
if ($SkipExplorerRestart) { $arguments += '-SkipExplorerRestart' }
& powershell.exe @arguments
if ($LASTEXITCODE -ne 0) { throw "QuickConvert installation failed with exit code $LASTEXITCODE." }
