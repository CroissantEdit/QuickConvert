param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository,
    [string]$Tag
)

$ErrorActionPreference = 'Stop'

function Invoke-QuickConvertInstallerScript {
    param(
        [Parameter(Mandatory)][string]$Path,
        [string[]]$ArgumentList = @()
    )

    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $Path @ArgumentList
    if ($LASTEXITCODE -ne 0) { throw "QuickConvert installer script failed: $Path" }
}

function Test-Administrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    $elevatedScript = Join-Path ([IO.Path]::GetTempPath()) "quickconvert-install-$([guid]::NewGuid().ToString('N')).ps1"
    try {
        Invoke-WebRequest `
            -Uri "https://raw.githubusercontent.com/$Repository/main/scripts/install-from-github.ps1" `
            -OutFile $elevatedScript `
            -Headers @{ 'User-Agent' = 'QuickConvert installer' }

        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $elevatedScript), '-Repository', $Repository)
        if ($Tag) { $arguments += @('-Tag', $Tag) }
        $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0) { throw "QuickConvert installation failed with exit code $($process.ExitCode)." }
    }
    finally {
        Remove-Item -LiteralPath $elevatedScript -Force -ErrorAction SilentlyContinue
    }
    return
}

$releaseUri = if ($Tag) {
    "https://api.github.com/repos/$Repository/releases/tags/$Tag"
} else {
    "https://api.github.com/repos/$Repository/releases/latest"
}
$release = Invoke-RestMethod -Uri $releaseUri -Headers @{ 'User-Agent' = 'QuickConvert installer' }
$asset = @($release.assets | Where-Object Name -like 'QuickConvert-*.zip' | Select-Object -First 1)
if (-not $asset) { throw "No QuickConvert release ZIP was found for $Repository." }

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "quickconvert-install-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
try {
    $archive = Join-Path $temporaryRoot $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive -Headers @{ 'User-Agent' = 'QuickConvert installer' }
    $extract = Join-Path $temporaryRoot 'extract'
    Expand-Archive -LiteralPath $archive -DestinationPath $extract
    $releaseFolder = Get-ChildItem -LiteralPath $extract -Directory | Where-Object {
        Test-Path -LiteralPath (Join-Path $_.FullName 'QuickConvert.Identity.msix')
    } | Select-Object -First 1
    if (-not $releaseFolder) { throw 'The release ZIP does not contain a QuickConvert package.' }

    $certificatePath = Join-Path $releaseFolder.FullName 'QuickConvert.cer'
    if (-not (Test-Path -LiteralPath $certificatePath)) { throw 'The release ZIP does not contain its signing certificate.' }
    $certificate = Get-PfxCertificate -FilePath $certificatePath
    if ($certificate.Subject -ne 'CN=QuickConvert Development') { throw "Unexpected package certificate: $($certificate.Subject)" }
    if (-not (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $certificate.Thumbprint)) {
        Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    }

    # Keep executable files below app so updates never remove user settings stored in
    # %LOCALAPPDATA%\QuickConvert.
    $installRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'QuickConvert\app'))
    $localAppData = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $installRoot.StartsWith($localAppData, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing to replace an unexpected install path.' }

    $existing = Get-AppxPackage -Name 'QuickConvert.Desktop'
    if ($existing) {
        Invoke-QuickConvertInstallerScript -Path (Join-Path $releaseFolder.FullName 'uninstall-shell.ps1') -ArgumentList '-SkipExplorerRestart'
    }
    if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
    Move-Item -LiteralPath $releaseFolder.FullName -Destination $installRoot
    Invoke-QuickConvertInstallerScript -Path (Join-Path $installRoot 'install-shell.ps1')
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
