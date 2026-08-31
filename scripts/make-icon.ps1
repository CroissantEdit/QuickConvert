# Builds the multi-resolution Explorer/taskbar ICO from the approved App.png artwork.
Add-Type -AssemblyName System.Drawing

$assetDir = Join-Path $PSScriptRoot '..\src\QuickConvert\Assets'
$pngPath = Join-Path $assetDir 'App.png'
if (-not (Test-Path -LiteralPath $pngPath)) { throw "Missing source artwork: $pngPath" }

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$source = [Drawing.Image]::FromFile($pngPath)
$images = New-Object System.Collections.ArrayList

try {
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap $size, $size
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, $size, $size)
        }
        finally { $graphics.Dispose() }
        $null = $images.Add($bitmap)
    }
}
finally { $source.Dispose() }

# Encode PNG frames in a multi-size ICO. Modern Windows Explorer handles these natively.
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    $frames = New-Object System.Collections.ArrayList

    foreach ($bitmap in $images) {
        $frame = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($frame, [Drawing.Imaging.ImageFormat]::Png)
            $bytes = $frame.ToArray()
            $writer.Write([Byte]($(if ($bitmap.Width -ge 256) { 0 } else { $bitmap.Width })))
            $writer.Write([Byte]($(if ($bitmap.Height -ge 256) { 0 } else { $bitmap.Height })))
            $writer.Write([Byte]0); $writer.Write([Byte]0); $writer.Write([UInt16]1); $writer.Write([UInt16]32)
            $writer.Write([UInt32]$bytes.Length); $writer.Write([UInt32]$offset)
            $offset += $bytes.Length
            $null = $frames.Add($bytes)
        }
        finally { $frame.Dispose(); $bitmap.Dispose() }
    }
    foreach ($frame in $frames) { $writer.Write($frame) }
    $writer.Flush()
    [IO.File]::WriteAllBytes((Join-Path $assetDir 'App.ico'), $stream.ToArray())
}
finally { $writer.Dispose(); $stream.Dispose() }

Write-Output "Wrote $assetDir\App.ico from App.png"
