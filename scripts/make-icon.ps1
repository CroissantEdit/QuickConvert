# Generates the clean QuickConvert exchange-arrows icon used by Explorer/taskbar.
# The utility window itself hides its title-bar icon; Explorer extracts this resource from the EXE.
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$images = New-Object System.Collections.ArrayList

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $scale = $size / 64.0
    $blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 120, 212))
    $teal = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 153, 153))

    # Right-pointing upper arrow.
    $topBody = New-Object System.Drawing.RectangleF (11*$scale), (18.5*$scale), (38*$scale), (7*$scale)
    $g.FillRectangle($blue, $topBody)
    $g.FillPolygon($blue, @(
        (New-Object System.Drawing.PointF (47*$scale), (12*$scale)),
        (New-Object System.Drawing.PointF (59*$scale), (22*$scale)),
        (New-Object System.Drawing.PointF (47*$scale), (32*$scale))
    ))

    # Left-pointing lower arrow.
    $bottomBody = New-Object System.Drawing.RectangleF (15*$scale), (38.5*$scale), (38*$scale), (7*$scale)
    $g.FillRectangle($teal, $bottomBody)
    $g.FillPolygon($teal, @(
        (New-Object System.Drawing.PointF (17*$scale), (32*$scale)),
        (New-Object System.Drawing.PointF (5*$scale), (42*$scale)),
        (New-Object System.Drawing.PointF (17*$scale), (52*$scale))
    ))

    $blue.Dispose(); $teal.Dispose(); $g.Dispose()
    $null = $images.Add($bmp)
}

# Encode each frame as PNG inside a multi-size ICO. Windows handles PNG-compressed ICO frames natively.
$mem = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $mem
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$dataOffset = 6 + 16 * $sizes.Count
$buffers = New-Object System.Collections.ArrayList

foreach ($bmp in $images) {
    $png = New-Object System.IO.MemoryStream
    $bmp.Save($png, [Drawing.Imaging.ImageFormat]::Png)
    $bytes = $png.ToArray()
    $w = $bmp.Width; $h = $bmp.Height
    $bw.Write([Byte]($(if ($w -ge 256) { 0 } else { $w })))
    $bw.Write([Byte]($(if ($h -ge 256) { 0 } else { $h })))
    $bw.Write([Byte]0); $bw.Write([Byte]0); $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$bytes.Length); $bw.Write([UInt32]$dataOffset)
    $dataOffset += $bytes.Length
    $null = $buffers.Add($bytes)
    $png.Dispose(); $bmp.Dispose()
}

foreach ($bytes in $buffers) { $bw.Write($bytes) }
$bw.Flush()

$assetDir = "src\QuickConvert\Assets"
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
[System.IO.File]::WriteAllBytes((Join-Path $assetDir 'App.ico'), $mem.ToArray())
# Also emit a 256px PNG used to generate MSIX visual assets.
$pngBmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($pngBmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 120, 212))
$teal = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 153, 153))
$g.FillRectangle($blue, (44), (74), (152), (28))
$g.FillPolygon($blue, @((New-Object System.Drawing.PointF 188,48),(New-Object System.Drawing.PointF 236,88),(New-Object System.Drawing.PointF 188,128)))
$g.FillRectangle($teal, (60), (154), (152), (28))
$g.FillPolygon($teal, @((New-Object System.Drawing.PointF 68,128),(New-Object System.Drawing.PointF 20,168),(New-Object System.Drawing.PointF 68,208)))
$pngBmp.Save((Join-Path $assetDir 'App.png'), [Drawing.Imaging.ImageFormat]::Png)
$blue.Dispose(); $teal.Dispose(); $g.Dispose(); $pngBmp.Dispose(); $bw.Dispose(); $mem.Dispose()
Write-Output "Wrote $assetDir\App.ico and App.png"
