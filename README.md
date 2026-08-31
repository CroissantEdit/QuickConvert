<p align="center">
  <img src="src/QuickConvert/Assets/App.png" alt="QuickConvert" width="96" />
</p>

<h1 align="center">QuickConvert</h1>

<p align="center">
  Convert files straight from the Windows 11 right-click menu.
</p>

<p align="center">
  <strong>Fast</strong> · <strong>No ads</strong> · <strong>No account</strong> · <strong>Runs locally</strong>
</p>

## Install

Choose whichever install method is easiest for you.

### PowerShell (recommended)

Open PowerShell, paste this command, and approve the one Windows UAC prompt:

```powershell
& ([scriptblock]::Create((Invoke-RestMethod https://raw.githubusercontent.com/CroissantEdit/QuickConvert/main/scripts/install-from-github.ps1))) -Repository CroissantEdit/QuickConvert
```

The installer downloads the latest release, puts QuickConvert in `%LOCALAPPDATA%\QuickConvert\app`, and refreshes Explorer. The right-click menu is ready when it finishes.

### Release ZIP

1. Download and extract `QuickConvert-*.zip` from GitHub Releases.
2. Double-click `Install QuickConvert.cmd`.
3. Approve the UAC prompt.

The installer copies the app to `%LOCALAPPDATA%\QuickConvert\app`. You can delete the ZIP and extracted folder afterward.

### Build it yourself

For development, install .NET 10, Visual Studio C++ Build Tools, and the Windows SDK. Then run an Administrator PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build-package.ps1
.\artifacts\QuickConvert\install-release.ps1
```

## Use

1. Right-click a supported file in File Explorer.
2. Select **QuickConvert**.
3. Choose a format, or open **Convert...** for quality and output-folder settings.

Video-to-audio exports (for example, MP4 → MP3) are in **Convert...**.

## Supported formats

| Type | Inputs | Outputs |
| --- | --- | --- |
| Images | PNG, JPG, WEBP, AVIF, JPEG XL, GIF, TIFF, ICO, HEIC, PSD, and more | JPG, PNG, WEBP, AVIF, JPEG XL, GIF, BMP, TIFF, ICO, APNG |
| Audio | MP3, M4A, AAC, FLAC, WAV, AIFF, OGG, OPUS, WMA, AC3 | MP3, M4A, AAC, FLAC, WAV, AIFF, OGG, OPUS, WMA, AC3 |
| Video | MP4, MKV, WEBM, MOV, AVI, WMV, MPEG, 3GP, FLV, TS, and more | MP4, MKV, WEBM, MOV, AVI, WMV, MPEG, MPEG-TS, 3GP, FLV, GIF, or audio-only formats |

## Fast and lightweight

- No ads, telemetry, accounts, or cloud uploads.
- No tray app, service, or startup process running in the background.
- When idle, QuickConvert uses no CPU or RAM because no QuickConvert process is running.
- It starts only for a conversion, then exits when the job is done.

## Uninstall

Run this from the installed app folder if you ever want to remove it:

```powershell
& "$env:LOCALAPPDATA\QuickConvert\app\uninstall-shell.ps1"
```
