param([switch]$TrustMachine)

$ErrorActionPreference = 'Stop'
$subject = 'CN=QuickConvert Development'
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -FriendlyName 'QuickConvert Development Signing' `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyUsage DigitalSignature `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3') `
        -NotAfter (Get-Date).AddYears(2)
}

$stores = @('TrustedPeople', 'Root')
foreach ($store in $stores) {
    $trusted = Get-ChildItem "Cert:\CurrentUser\$store" |
        Where-Object { $_.Thumbprint -eq $cert.Thumbprint } |
        Select-Object -First 1
    if ($trusted) { continue }

    $publicCertificate = Join-Path ([IO.Path]::GetTempPath()) "QuickConvert-$($cert.Thumbprint).cer"
    try {
        Export-Certificate -Cert $cert -FilePath $publicCertificate -Force | Out-Null
        Import-Certificate -FilePath $publicCertificate -CertStoreLocation "Cert:\CurrentUser\$store" | Out-Null
    }
    finally {
        Remove-Item -LiteralPath $publicCertificate -Force -ErrorAction SilentlyContinue
    }
}

$machineTrusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople |
    Where-Object { $_.Thumbprint -eq $cert.Thumbprint } |
    Select-Object -First 1
if (-not $machineTrusted) {
    $publicCertificate = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\artifacts')) "QuickConvert-$($cert.Thumbprint).cer"
    Export-Certificate -Cert $cert -FilePath $publicCertificate -Force | Out-Null
    if (-not $TrustMachine) {
        throw "Development certificate is not trusted for sparse package deployment. Run trust-dev-certificate-machine.ps1 from an Administrator PowerShell with -CertificatePath `"$publicCertificate`", then rebuild."
    }
    Import-Certificate -FilePath $publicCertificate -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
}

if (-not (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $cert.Thumbprint)) {
    throw 'Development certificate is not trusted for sparse package deployment.'
}

$cert.Thumbprint
