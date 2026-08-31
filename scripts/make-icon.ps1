# Generates QuickConvert's simple document-and-picture app icon for Explorer and the taskbar.
Add-Type -AssemblyName System.Drawing

$assetDir = Join-Path $PSScriptRoot '..\src\QuickConvert\Assets'
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null

function New-QuickConvertBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $scale = $size / 256.0
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        # A single, bold photo document: legible even in Explorer's smallest icon sizes.
        $page = New-Object Drawing.Drawing2D.GraphicsPath
        $page.AddArc((48*$scale), (24*$scale), (32*$scale), (32*$scale), 180, 90)
        $page.AddLine((64*$scale), (24*$scale), (164*$scale), (24*$scale))
        $page.AddLine((164*$scale), (24*$scale), (208*$scale), (68*$scale))
        $page.AddLine((208*$scale), (68*$scale), (208*$scale), (208*$scale))
        $page.AddArc((176*$scale), (208*$scale), (32*$scale), (32*$scale), 0, 90)
        $page.AddLine((192*$scale), (232*$scale), (64*$scale), (232*$scale))
        $page.AddArc((48*$scale), (208*$scale), (32*$scale), (32*$scale), 90, 90)
        $page.CloseFigure()

        $shadow = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(35, 0, 0, 0))
        $graphics.TranslateTransform(0, 5*$scale)
        $graphics.FillPath($shadow, $page)
        $graphics.ResetTransform()

        $pageBrush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 24, 110, 191))
        $graphics.FillPath($pageBrush, $page)

        $fold = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 139, 210, 255))
        $graphics.FillPolygon($fold, @(
            (New-Object Drawing.PointF (164*$scale), (24*$scale)),
            (New-Object Drawing.PointF (164*$scale), (68*$scale)),
            (New-Object Drawing.PointF (208*$scale), (68*$scale))
        ))

        $photoRect = New-Object Drawing.RectangleF (68*$scale), (92*$scale), (120*$scale), (92*$scale)
        $photo = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 231, 248, 255))
        $graphics.FillRectangle($photo, $photoRect)

        $sun = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 255, 193, 54))
        $graphics.FillEllipse($sun, (84*$scale), (108*$scale), (24*$scale), (24*$scale))

        $backMountain = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 28, 166, 174))
        $graphics.FillPolygon($backMountain, @(
            (New-Object Drawing.PointF (76*$scale), (174*$scale)),
            (New-Object Drawing.PointF (126*$scale), (118*$scale)),
            (New-Object Drawing.PointF (180*$scale), (174*$scale))
        ))
        $frontMountain = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 36, 189, 104))
        $graphics.FillPolygon($frontMountain, @(
            (New-Object Drawing.PointF (108*$scale), (184*$scale)),
            (New-Object Drawing.PointF (148*$scale), (136*$scale)),
            (New-Object Drawing.PointF (188*$scale), (184*$scale))
        ))

        foreach ($resource in @($page, $shadow, $pageBrush, $fold, $photo, $sun, $backMountain, $frontMountain)) { $resource.Dispose() }
        return $bitmap
    }
    finally { $graphics.Dispose() }
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$frames = New-Object System.Collections.ArrayList
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
try {
    foreach ($size in $sizes) { $null = $frames.Add((New-QuickConvertBitmap $size)) }
    $preview = New-QuickConvertBitmap 256
    try { $preview.Save((Join-Path $assetDir 'App.png'), [Drawing.Imaging.ImageFormat]::Png) }
    finally { $preview.Dispose() }

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

Write-Output "Wrote $assetDir\App.png and App.ico"
