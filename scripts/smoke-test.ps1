# Smoke test for the QuickConvert worker (Milestone 1 vertical slice).
# Verifies: conversion works, the worker process ALWAYS terminates (hang
# regression test), output lands beside the original, and duplicate outputs
# get a unique name instead of overwriting.
#
# Note: the worker briefly shows its completion toast during these tests.

param(
    [switch]$SkipBuild,
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = if ($Executable) { $Executable } else { Join-Path $root 'src\QuickConvert\bin\Debug\net10.0-windows\QuickConvert.exe' }
$ffmpeg = Join-Path $root 'vendor\ffmpeg\ffmpeg.exe'

$script:failed = 0
function Pass($name) { Write-Host "[PASS] $name" -ForegroundColor Green }
function Fail($name, $detail) { Write-Host "[FAIL] $name - $detail" -ForegroundColor Red; $script:failed++ }

if (-not $SkipBuild) {
    Write-Host "Building QuickConvert..."
    dotnet build (Join-Path $root 'QuickConvert.slnx') -v:quiet -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}

if (-not (Test-Path $exe)) { throw "QuickConvert.exe not found: $exe" }
if (-not (Test-Path $ffmpeg)) { throw "ffmpeg.exe not found: $ffmpeg" }

try {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class QcSmokeNative {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);
}
'@
} catch {
    if ($_.Exception.Message -notmatch 'already exists') { throw }
}

$rect = New-Object QcSmokeNative+RECT
[void][QcSmokeNative]::SystemParametersInfo(0x0030, 0, [ref]$rect, 0)  # SPI_GETWORKAREA

$testDir = Join-Path $env:TEMP ("qc-smoke-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $testDir | Out-Null
$png = Join-Path $testDir 'test.png'

& $ffmpeg -hide_banner -loglevel error -y -f lavfi -i "testsrc=size=320x240:rate=1" -frames:v 1 $png
if (-not (Test-Path $png)) { throw "Failed to create test PNG" }

function Invoke-Worker {
    param([string]$target, [string]$file, [int]$cursorX, [int]$cursorY, [int]$timeoutMs)
    [void][QcSmokeNative]::SetCursorPos($cursorX, $cursorY)
    Start-Sleep -Milliseconds 200
    $p = Start-Process $exe -ArgumentList "--convert", $target, "`"$file`"" -PassThru
    $exited = $p.WaitForExit($timeoutMs)
    if (-not $exited) {
        Stop-Process -Id $p.Id -Force
        Start-Sleep -Milliseconds 200
    }
    return $exited
}

# Test 1: hang regression - cursor parked exactly where the toast appears
# (bottom-right of the work area). The old bug left the worker alive forever here.
$exited = Invoke-Worker 'jpg' $png ($rect.Right - 1) ($rect.Bottom - 1) 30000
if ($exited) {
    Pass 'Worker terminates with cursor parked over the toast'
} else {
    Fail 'Worker terminates with cursor parked over the toast' 'process still alive after 30s'
}

$jpg = Join-Path $testDir 'test.jpg'
if (Test-Path $jpg) {
    Pass 'PNG -> JPG conversion produced output beside the original'
} else {
    Fail 'PNG -> JPG conversion produced output beside the original' 'test.jpg missing'
}

# Test 2: cursor away from the toast - must still exit, and must NOT overwrite
# the existing test.jpg; a unique name is expected.
$exited2 = Invoke-Worker 'jpg' $png 50 50 30000
if ($exited2) {
    Pass 'Worker terminates with cursor away from the toast'
} else {
    Fail 'Worker terminates with cursor away from the toast' 'process still alive after 30s'
}

$jpg2 = Join-Path $testDir 'test (1).jpg'
if (Test-Path $jpg2) {
    Pass 'Duplicate output gets a unique name (test (1).jpg)'
} else {
    Fail 'Duplicate output gets a unique name' "expected: $jpg2"
}

# Test 3: original file must be untouched.
if (Test-Path $png) {
    Pass 'Original file untouched'
} else {
    Fail 'Original file untouched' 'test.png missing'
}

Write-Host ''
Write-Host "Test files left in: $testDir"

if ($script:failed -gt 0) {
    Write-Host "$($script:failed) test(s) failed" -ForegroundColor Red
    exit 1
}
Write-Host 'All smoke tests passed.' -ForegroundColor Green
exit 0
