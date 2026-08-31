# Generates QuickConvert's transparent blue photo-document icon for Explorer and the taskbar.
Add-Type -AssemblyName System.Drawing

$assetDir = Join-Path $PSScriptRoot '..\src\QuickConvert\Assets'
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null

function New-RoundedPath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-QuickConvertBitmap([int]$size) {
    $bitmap = New-Object Drawing.Bitmap $size, $size
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $scale = $size / 256.0
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        # Upright document silhouette, with only transparent space outside it.
        $page = New-RoundedPath (34*$scale) (18*$scale) (188*$scale) (220*$scale) (24*$scale)
        $shadow = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(34, 37, 89, 170))
        $graphics.TranslateTransform(0, 5*$scale)
        $graphics.FillPath($shadow, $page)
        $graphics.ResetTransform()

        $pageBrush = New-Object Drawing.Drawing2D.LinearGradientBrush `
            (New-Object Drawing.PointF (34*$scale), (18*$scale)), `
            (New-Object Drawing.PointF (222*$scale), (238*$scale)), `
            ([Drawing.Color]::White), ([Drawing.Color]::FromArgb(255, 225, 240, 255))
        $pageOutline = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 137, 190, 247)), (2*$scale)
        $graphics.FillPath($pageBrush, $page)
        $graphics.DrawPath($pageOutline, $page)

        # Folded corner makes it unmistakably a file rather than a generic image tile.
        $fold = New-Object Drawing.Drawing2D.GraphicsPath
        $fold.AddLine((166*$scale), (19*$scale), (166*$scale), (70*$scale))
        $fold.AddArc((166*$scale), (45*$scale), (50*$scale), (50*$scale), 180, 90)
        $fold.AddLine((216*$scale), (70*$scale), (166*$scale), (19*$scale))
        $fold.CloseFigure()
        $foldBrush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 239, 247, 255))
        $graphics.FillPath($foldBrush, $fold)
        $graphics.DrawPath($pageOutline, $fold)

        $photo = New-RoundedPath (59*$scale) (79*$scale) (138*$scale) (96*$scale) (18*$scale)
        $photoBrush = New-Object Drawing.Drawing2D.LinearGradientBrush `
            (New-Object Drawing.PointF (59*$scale), (79*$scale)), `
            (New-Object Drawing.PointF (197*$scale), (175*$scale)), `
            ([Drawing.Color]::FromArgb(255, 97, 173, 255)), ([Drawing.Color]::FromArgb(255, 38, 111, 224))
        $graphics.FillPath($photoBrush, $photo)

        $sun = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 232, 247, 255))
        $graphics.FillEllipse($sun, (82*$scale), (98*$scale), (22*$scale), (22*$scale))
        $mountainBack = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 191, 222, 255))
        $graphics.FillPolygon($mountainBack, @(
            (New-Object Drawing.PointF (60*$scale), (165*$scale)),
            (New-Object Drawing.PointF (112*$scale), (112*$scale)),
            (New-Object Drawing.PointF (163*$scale), (165*$scale))
        ))
        $mountainFront = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 224, 240, 255))
        $graphics.FillPolygon($mountainFront, @(
            (New-Object Drawing.PointF (104*$scale), (175*$scale)),
            (New-Object Drawing.PointF (148*$scale), (126*$scale)),
            (New-Object Drawing.PointF (197*$scale), (175*$scale))
        ))

        $line = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255, 95, 164, 244)), (10*$scale)
        $line.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $line.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($line, (66*$scale), (194*$scale), (187*$scale), (194*$scale))
        $graphics.DrawLine($line, (66*$scale), (214*$scale), (160*$scale), (214*$scale))

        foreach ($resource in @($page, $shadow, $pageBrush, $pageOutline, $fold, $foldBrush, $photo, $photoBrush, $sun, $mountainBack, $mountainFront, $line)) { $resource.Dispose() }
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
