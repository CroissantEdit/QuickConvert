# Builds the multi-resolution Explorer/taskbar ICO from the approved App.png artwork.
Add-Type -AssemblyName System.Drawing

$assetDir = Join-Path $PSScriptRoot '..\src\QuickConvert\Assets'
$pngPath = Join-Path $assetDir 'App.png'
if (-not (Test-Path -LiteralPath $pngPath)) { throw "Missing source artwork: $pngPath" }

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$source = [Drawing.Image]::FromFile($pngPath)
$frames = New-Object System.Collections.ArrayList
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
        $null = $frames.Add($bitmap)
    }
}
finally { $source.Dispose() }

# PNG-compressed frames keep the ICO sharp at all Windows icon sizes.
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    $buffers = New-Object System.Collections.ArrayList
    foreach ($frame in $frames) {
        $png = New-Object System.IO.MemoryStream
        try {
            $frame.Save($png, [Drawing.Imaging.ImageFormat]::Png)
            $bytes = $png.ToArray()
            $writer.Write([Byte]($(if ($frame.Width -ge 256) { 0 } else { $frame.Width })))
            $writer.Write([Byte]($(if ($frame.Height -ge 256) { 0 } else { $frame.Height })))
            $writer.Write([Byte]0); $writer.Write([Byte]0); $writer.Write([UInt16]1); $writer.Write([UInt16]32)
            $writer.Write([UInt32]$bytes.Length); $writer.Write([UInt32]$offset)
            $offset += $bytes.Length
            $null = $buffers.Add($bytes)
        }
        finally { $png.Dispose(); $frame.Dispose() }
    }
    foreach ($buffer in $buffers) { $writer.Write($buffer) }
    $writer.Flush()
    [IO.File]::WriteAllBytes((Join-Path $assetDir 'App.ico'), $stream.ToArray())
}
finally { $writer.Dispose(); $stream.Dispose() }

Write-Output "Wrote $assetDir\App.ico from App.png"
