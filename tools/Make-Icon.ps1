Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\Setup\Icons"
New-Item -ItemType Directory -Force $out | Out-Null

function New-Frame([int]$n) {
    $bmp = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 24, 22, 38))
    $r = [int]($n * 0.2)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($n - $d - 1, 0, $d, $d, 270, 90)
    $path.AddArc($n - $d - 1, $n - $d - 1, $d, $d, 0, 90)
    $path.AddArc(0, $n - $d - 1, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bg, $path)
    $gold = [System.Drawing.Color]::FromArgb(255, 226, 178, 90)
    $pen = New-Object System.Drawing.Pen $gold, ([Math]::Max(1.0, $n / 14.0))
    $m = $n * 0.18
    $g.DrawEllipse($pen, [single]$m, [single]$m, [single]($n - 2 * $m), [single]($n - 2 * $m))
    $c = $n / 2.0
    $k = $n * 0.2
    $pts = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF $c, ($c - $k)),
        (New-Object System.Drawing.PointF ($c + $k), $c),
        (New-Object System.Drawing.PointF $c, ($c + $k)),
        (New-Object System.Drawing.PointF ($c - $k), $c))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush $gold), $pts)
    $k2 = $k * 0.5
    $pts2 = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF $c, ($c - $k2)),
        (New-Object System.Drawing.PointF ($c + $k2), $c),
        (New-Object System.Drawing.PointF $c, ($c + $k2)),
        (New-Object System.Drawing.PointF ($c - $k2), $c))
    $g.FillPolygon($bg, $pts2)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 32, 48, 256
$frames = foreach ($n in $sizes) { ,(New-Frame $n) }
$ms = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ms
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $n = $sizes[$i]
    $b = if ($n -ge 256) { 0 } else { $n }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
[IO.File]::WriteAllBytes((Join-Path $out "UOTinker.ico"), $ms.ToArray())