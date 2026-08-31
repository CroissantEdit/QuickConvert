<p align="center">
  <img src="src/QuickConvert/Assets/App.png" alt="QuickConvert" width="96" />
</p>

<h1 align="center">QuickConvert</h1>

<p align="center">
  A lightweight Windows 11 context-menu converter for images, audio, and video.
</p>

<p align="center">
  <strong>Local processing</strong> · <strong>No ads</strong> · <strong>No account</strong> · <strong>No resident process</strong>
</p>

## Overview

QuickConvert adds a **QuickConvert** entry to File Explorer. Select one or more files, choose an output format, and the converted files are written to your selected output folder.

Conversions run on your PC. QuickConvert does not upload files, require an account, or keep a tray application running.

## Installation

### PowerShell installer

Open PowerShell and run the following command. Windows will ask for administrator approval once:

```powershell
& ([scriptblock]::Create((Invoke-RestMethod https://raw.githubusercontent.com/CroissantEdit/QuickConvert/main/scripts/install-from-github.ps1))) -Repository CroissantEdit/QuickConvert
```

For a manual install, download the latest package from [GitHub Releases](https://github.com/CroissantEdit/QuickConvert/releases).

## Usage

1. Right-click one or more supported files in File Explorer.
2. Select **QuickConvert**.
3. Select a format, or open **Convert...** to choose quality and an output folder.

Video-to-audio exports, such as MP4 → MP3, are available from **Convert...**.

## Supported formats

| Type | Inputs | Outputs |
| --- | --- | --- |
| Images | PNG, JPG, WEBP, AVIF, JPEG XL, GIF, TIFF, ICO, HEIC, PSD, and more | JPG, PNG, WEBP, AVIF, JPEG XL, GIF, BMP, TIFF, ICO, APNG |
| Audio | MP3, M4A, AAC, FLAC, WAV, AIFF, OGG, OPUS, WMA, AC3 | MP3, M4A, AAC, FLAC, WAV, AIFF, OGG, OPUS, WMA, AC3 |
| Video | MP4, MKV, WEBM, MOV, AVI, WMV, MPEG, 3GP, FLV, TS, and more | MP4, MKV, WEBM, MOV, AVI, WMV, MPEG, MPEG-TS, 3GP, FLV, GIF, or audio-only formats |

## Performance and privacy

- No ads, accounts, or cloud uploads.
- No tray application, Windows service, or startup process.
- No resident QuickConvert process when idle, so idle CPU and memory usage are zero.
- The converter starts on demand for a job and exits when the job is complete.

## Uninstall

Run this from the installed app folder if you ever want to remove it:

```powershell
& "$env:LOCALAPPDATA\QuickConvert\app\uninstall-shell.ps1"
```
