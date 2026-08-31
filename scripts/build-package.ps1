param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$RunFullConversionMatrix,
    [switch]$TrustCertificate
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'QuickConvert'
$layout = Join-Path $artifacts 'package-layout'
$native = Join-Path $artifacts 'native'
$nativeTests = Join-Path $artifacts 'native-tests'

foreach ($target in @($publish, $layout)) {
    $full = [IO.Path]::GetFullPath($target)
    if (-not $full.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected path: $full"
    }
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
    New-Item -ItemType Directory -Path $full | Out-Null
}

$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) { throw 'Visual Studio C++ tools are not installed.' }
$msbuild = Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe'
$sdkBin = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64'
$makeAppx = Join-Path $sdkBin 'makeappx.exe'
$signTool = Join-Path $sdkBin 'signtool.exe'

# FFmpeg is intentionally not committed: its Windows executable exceeds GitHub's
# file-size limit. Fetch this fixed, hash-checked BtbN build when a clean machine
# packages QuickConvert (for example, the GitHub release workflow).
$ffmpegBuild = 'N-126335-gb32f8d1c23'
$ffmpegRelease = 'autobuild-2026-08-30-13-12'
$ffmpegArchiveName = "ffmpeg-$ffmpegBuild-win64-gpl.zip"
$ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/$ffmpegRelease/$ffmpegArchiveName"
$ffmpegSha256 = 'ed59cb91788b6893bf78ac8a732cc2c1367f8e17ebcbdef61c72e4865cc3df2b'
$ffmpegDir = Join-Path $root 'vendor\ffmpeg'
$ffmpeg = Join-Path $ffmpegDir 'ffmpeg.exe'

if (-not (Test-Path -LiteralPath $ffmpeg)) {
    Write-Host "Fetching pinned FFmpeg $ffmpegBuild build dependency..."
    New-Item -ItemType Directory -Path $ffmpegDir -Force | Out-Null
    $download = Join-Path ([IO.Path]::GetTempPath()) "quickconvert-ffmpeg-$([guid]::NewGuid().ToString('N')).zip"
    $extract = Join-Path ([IO.Path]::GetTempPath()) "quickconvert-ffmpeg-$([guid]::NewGuid().ToString('N'))"
    try {
        Invoke-WebRequest -Uri $ffmpegUrl -OutFile $download -UseBasicParsing
        $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $download).Hash.ToLowerInvariant()
        if ($actualHash -ne $ffmpegSha256) {
            throw "FFmpeg download hash mismatch. Expected $ffmpegSha256 but got $actualHash."
        }

        Expand-Archive -LiteralPath $download -DestinationPath $extract -Force
        $found = Get-ChildItem -LiteralPath $extract -Filter 'ffmpeg.exe' -Recurse -File | Select-Object -First 1
        if (-not $found) { throw 'Pinned FFmpeg archive did not contain ffmpeg.exe.' }
        Copy-Item -LiteralPath $found.FullName -Destination $ffmpeg -Force
    }
    finally {
        if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $extract) { Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# Use libavif's official Windows encoder for AVIF still images. The bundled FFmpeg
# build can decode AVIF, but its libaom monochrome alpha path fails on some Windows
# builds. Pin and hash-check the official tools so transparent AVIF is deterministic.
$libavifVersion = '1.4.2'
$libavifUrl = "https://github.com/AOMediaCodec/libavif/releases/download/v$libavifVersion/windows-artifacts.zip"
$libavifMirrorUrl = "https://sourceforge.net/projects/libavif.mirror/files/v$libavifVersion/windows-artifacts.zip/download"
$libavifSha256 = 'cb2d9fea43dcbab1d0707e3b37eb7b08070ad2fb60a2c188c39ec12382c0484a'
$libavifDir = Join-Path $root 'vendor\libavif'
$avifenc = Join-Path $libavifDir 'avifenc.exe'
$avifdec = Join-Path $libavifDir 'avifdec.exe'

if (-not (Test-Path -LiteralPath $avifenc) -or -not (Test-Path -LiteralPath $avifdec)) {
    Write-Host "Fetching official libavif $libavifVersion tools (one-time build dependency)..."
    New-Item -ItemType Directory -Path $libavifDir -Force | Out-Null
    $download = Join-Path ([IO.Path]::GetTempPath()) "quickconvert-libavif-$([guid]::NewGuid().ToString('N')).zip"
    $extract = Join-Path ([IO.Path]::GetTempPath()) "quickconvert-libavif-$([guid]::NewGuid().ToString('N'))"
    try {
        $downloaded = $false
        foreach ($url in @($libavifUrl, $libavifMirrorUrl)) {
            try {
                Invoke-WebRequest -Uri $url -OutFile $download -UseBasicParsing
                $downloaded = $true
                break
            }
            catch {
                if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force }
            }
        }
        if (-not $downloaded) { throw 'Could not download the pinned libavif Windows tools from GitHub or the mirror.' }

        $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $download).Hash.ToLowerInvariant()
        if ($actualHash -ne $libavifSha256) {
            throw "libavif download hash mismatch. Expected $libavifSha256 but got $actualHash."
        }

        Expand-Archive -LiteralPath $download -DestinationPath $extract -Force
        foreach ($name in @('avifenc.exe', 'avifdec.exe')) {
            $found = Get-ChildItem -LiteralPath $extract -Filter $name -Recurse -File | Select-Object -First 1
            if (-not $found) { throw "Pinned libavif archive did not contain $name." }
            Copy-Item -LiteralPath $found.FullName -Destination (Join-Path $libavifDir $name) -Force
        }
    }
    finally {
        if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue }
        if (Test-Path -LiteralPath $extract) { Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

$env:QUICKCONVERT_AVIFENC = $avifenc
$env:QUICKCONVERT_AVIFDEC = $avifdec

& dotnet publish (Join-Path $root 'src\QuickConvert\QuickConvert.csproj') `
    -c $Configuration -r win-x64 --self-contained false -o $publish -nologo
if ($LASTEXITCODE -ne 0) { throw 'QuickConvert publish failed.' }

& dotnet run --project (Join-Path $root 'src\QuickConvert.FormatTests\QuickConvert.FormatTests.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Format catalog self-check failed.' }
& dotnet run --project (Join-Path $root 'src\QuickConvert.UiTests\QuickConvert.UiTests.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Native UI self-check failed.' }
if ($RunFullConversionMatrix) {
    & dotnet run --project (Join-Path $root 'src\QuickConvert.ConversionTests\QuickConvert.ConversionTests.csproj') -c $Configuration -- --matrix
    if ($LASTEXITCODE -ne 0) { throw 'Full conversion matrix failed.' }
}

& $msbuild (Join-Path $root 'src\QuickConvert.Shell.Tests\QuickConvert.Shell.Tests.vcxproj') `
    /nologo /m "/p:Configuration=$Configuration" /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Native shell self-check build failed.' }

& (Join-Path $nativeTests 'QuickConvert.Shell.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Native shell self-check failed.' }

& $msbuild (Join-Path $root 'src\QuickConvert.Shell\QuickConvert.Shell.vcxproj') `
    /nologo /m "/p:Configuration=$Configuration" /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Native shell DLL build failed.' }
Copy-Item -LiteralPath (Join-Path $native 'QuickConvert.Shell.dll') -Destination $publish -Force

$certificateParameters = if ($TrustCertificate) { @('-TrustMachine') } else { @() }
$thumbprint = (& (Join-Path $PSScriptRoot 'ensure-dev-certificate.ps1') @certificateParameters | Select-Object -Last 1).Trim()
if (-not $thumbprint) { throw 'Development signing certificate was not created.' }
$certificate = Get-ChildItem "Cert:\CurrentUser\My\$thumbprint"
if (-not $certificate) { throw "Signing certificate was not found: $thumbprint" }
Export-Certificate -Cert $certificate -FilePath (Join-Path $publish 'QuickConvert.cer') -Force | Out-Null
foreach ($binary in @('QuickConvert.exe', 'QuickConvert.Shell.dll')) {
    $path = Join-Path $publish $binary
    & $signTool sign /fd SHA256 /sha1 $thumbprint $path
    if ($LASTEXITCODE -ne 0) { throw "SignTool failed for $binary." }
    & $signTool verify /pa $path
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $binary." }
}

$assets = Join-Path $layout 'Assets'
New-Item -ItemType Directory -Path $assets | Out-Null
Add-Type -AssemblyName System.Drawing
$iconPng = Join-Path $root 'src\QuickConvert\Assets\App.png'
foreach ($asset in @(
    @{ Name = 'Square44x44Logo.png'; Size = 44 },
    @{ Name = 'Square150x150Logo.png'; Size = 150 },
    @{ Name = 'StoreLogo.png'; Size = 50 }
)) {
    $bitmap = New-Object System.Drawing.Bitmap $asset.Size, $asset.Size
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        if (Test-Path -LiteralPath $iconPng) {
            $source = [Drawing.Image]::FromFile($iconPng)
            try { $graphics.DrawImage($source, 0, 0, $asset.Size, $asset.Size) }
            finally { $source.Dispose() }
        }
        $bitmap.Save((Join-Path $assets $asset.Name), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
Copy-Item -LiteralPath (Join-Path $root 'package\AppxManifest.xml') -Destination $layout

$package = Join-Path $publish 'QuickConvert.Identity.msix'
& $makeAppx pack /d $layout /p $package /o /nv
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx pack failed.' }

& $signTool sign /fd SHA256 /sha1 $thumbprint $package
if ($LASTEXITCODE -ne 0) { throw 'SignTool failed.' }
& $signTool verify /pa $package
if ($LASTEXITCODE -ne 0) { throw 'Package signature verification failed.' }

@('QuickConvert.exe', 'QuickConvert.Shell.dll', 'ffmpeg.exe', 'avifenc.exe', 'QuickConvert.Identity.msix', 'QuickConvert.cer', 'install-release.ps1', 'Install QuickConvert.cmd') |
    ForEach-Object {
        if (-not (Test-Path -LiteralPath (Join-Path $publish $_))) {
            throw "Missing package output: $_"
        }
    }

 $version = ([xml](Get-Content (Join-Path $root 'src\QuickConvert\QuickConvert.csproj'))).Project.PropertyGroup.Version
$archive = Join-Path $artifacts "QuickConvert-$version.zip"
Compress-Archive -Path $publish -DestinationPath $archive -Force

Write-Host "Package ready: $package"
Write-Host "Release archive: $archive"
Write-Host "Development certificate: $thumbprint"
