param([Parameter(Mandatory)][string]$CertificatePath)

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an Administrator PowerShell.'
}
$certificate = Import-Certificate `
    -FilePath (Resolve-Path -LiteralPath $CertificatePath) `
    -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'

if (-not $certificate) { throw 'Certificate import failed.' }
