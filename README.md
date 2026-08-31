<p align="center">
  <img src="src/QuickConvert/Assets/App.png" alt="QuickConvert" width="96" />
</p>

<h1 align="center">QuickConvert</h1>

<p align="center">
  A fast, local Windows 11 right-click converter for images, audio, and video.
</p>

<p align="center">
  <strong>Private</strong> · <strong>No account</strong> · <strong>No background process</strong>
</p>

## Install

### One command from GitHub

After the first release is published, paste this into a normal PowerShell window. Replace `OWNER/REPO` with the GitHub repository name, then accept the one Windows UAC prompt.

```powershell
& ([scriptblock]::Create((Invoke-RestMethod https://raw.githubusercontent.com/OWNER/REPO/main/scripts/install-from-github.ps1))) -Repository OWNER/REPO
```

QuickConvert downloads the latest release, installs it in `%LOCALAPPDATA%\QuickConvert\app`, preserves its settings, and restarts Explorer so the right-click menu is ready immediately.

### Download a release ZIP

1. Download and extract `QuickConvert-*.zip` from GitHub Releases.
2. Double-click `Install QuickConvert.cmd`.
3. Accept the UAC prompt.

The installer copies the app to `%LOCALAPPDATA%\QuickConvert\app`. You can delete the ZIP and extracted Downloads folder afterward.

### Build from source

For development, use an Administrator PowerShell with .NET 10, Visual Studio C++ Build Tools, and the Windows SDK installed:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build-package.ps1
.\artifacts\QuickConvert\install-release.ps1
```

The normal package build runs focused app, UI, and Explorer-extension checks. The broad conversion matrix is optional:

```powershell
.\scripts\build-package.ps1 -RunFullConversionMatrix
```

## Use

1. Right-click one or more supported files in File Explorer.
2. Choose **QuickConvert**.
3. Pick a one-click format, or choose **Convert...** for quality and output-folder settings.

Video-to-audio exports, such as MP4 → MP3, are available in **Convert...** so they are not accidental one-click actions.

## Supported formats

| Type | Inputs | Outputs |
| --- | --- | --- |
| Images | PNG, JPG, WEBP, AVIF, JPEG XL, GIF, TIFF, ICO, HEIC, PSD, and more | JPG, PNG, WEBP, AVIF, JPEG XL, GIF, BMP, TIFF, ICO, APNG |
| Audio | MP3, M4A, AAC, FLAC, WAV, AIFF, OGG, OPUS, WMA, AC3 | MP3, M4A, AAC, FLAC, WAV, AIFF, OGG, OPUS, WMA, AC3 |
| Video | MP4, MKV, WEBM, MOV, AVI, WMV, MPEG, 3GP, FLV, TS, and more | MP4, MKV, WEBM, MOV, AVI, WMV, MPEG, MPEG-TS, 3GP, FLV, GIF, or audio-only formats |

## Designed to stay out of your way

- QuickConvert is not a Startup app, service, tray process, or always-running helper.
- At idle, it has no `QuickConvert.exe` process and therefore uses no QuickConvert CPU or RAM.
- The Explorer command is activated only when Explorer needs the right-click menu.
- Conversions run locally on your PC. Files are not uploaded anywhere.

## Uninstall

Run this from the installed app folder if you ever want to remove it:

```powershell
& "$env:LOCALAPPDATA\QuickConvert\app\uninstall-shell.ps1"
```
