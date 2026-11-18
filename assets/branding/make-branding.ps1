# Draws the logo, the multi-size icons and the social banner. Run:  powershell -File make-branding.ps1
# The geometry matches logo.svg. Original artwork; it uses nothing from Crusader Kings II.
Add-Type -AssemblyName System.Drawing
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function C($hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
$wood = C '#241810'; $leather = C '#44301f'; $gold = C '#c4993a'; $vellum = C '#e8d9b3'; $madder = C '#7b1f1f'

function Draw-Logo([System.Drawing.Graphics]$g, [float]$size, [bool]$tile) {
    $k = $size / 256.0
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    if ($tile) {
        $r = 48 * $k; $d = 2 * $r
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($size - $d, 0, $d, $d, 270, 90)
        $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90); $path.AddArc(0, $size - $d, $d, $d, 90, 90); $path.CloseFigure()
        $b = New-Object System.Drawing.SolidBrush $wood; $g.FillPath($b, $path)
    }
    $cx = 128 * $k; $cy = 132 * $k; $ro = 94 * $k; $w = 14 * $k
    # inner disc
    $disc = New-Object System.Drawing.SolidBrush $leather
    $g.FillEllipse($disc, $cx - ($ro - $w/2), $cy - ($ro - $w/2), 2 * ($ro - $w/2), 2 * ($ro - $w/2))
    # figure, clipped to the disc
    $clip = New-Object System.Drawing.Drawing2D.GraphicsPath
    $clip.AddEllipse($cx - ($ro - $w/2), $cy - ($ro - $w/2), 2 * ($ro - $w/2), 2 * ($ro - $w/2))
    $state = $g.Save(); $g.SetClip($clip)
    $fig = New-Object System.Drawing.SolidBrush $vellum
    $g.FillEllipse($fig, $cx - 31*$k, $cy - 52*$k, 62*$k, 62*$k)                 # head
    $g.FillEllipse($fig, $cx - 62*$k, $cy + 24*$k, 124*$k, 110*$k)               # shoulders
    $g.Restore($state)
    # gilt ring
    $pen = New-Object System.Drawing.Pen $gold, $w
    $g.DrawEllipse($pen, $cx - $ro + $w/2, $cy - $ro + $w/2, 2*($ro - $w/2), 2*($ro - $w/2))
    # a small crown: this is a portrait of a ruler, not a stock avatar
    $gb = New-Object System.Drawing.SolidBrush $gold
    $cp = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    foreach ($p in @(@(-30,-44), @(-30,-72), @(-15,-57), @(0,-80), @(15,-57), @(30,-72), @(30,-44))) { $cp.Add([System.Drawing.PointF]::new($cx + $p[0] * $k, $cy + $p[1] * $k)) }
    $g.FillPolygon($gb, $cp.ToArray())}

function Render-Png([int]$size, [bool]$tile) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::Transparent)
    Draw-Logo $g $size $tile; $g.Dispose(); return $bmp
}

function Png-Bytes($bmp) { $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); return ,$ms.ToArray() }

# An .ico that holds PNG images (supported since Windows Vista).
function Write-Ico($path, [int[]]$sizes) {
    $images = foreach ($s in $sizes) { ,(Png-Bytes (Render-Png $s $true)) }
    $fs = [System.IO.File]::Create($path); $bw = New-Object System.IO.BinaryWriter $fs
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $dim = if ($s -ge 256) { 0 } else { $s }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$images[$i].Length); $bw.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $bw.Write($img) }
    $bw.Dispose(); $fs.Dispose()
}

foreach ($s in 16, 32, 48, 64, 128, 256, 512) { $b = Render-Png $s $true; $b.Save((Join-Path $here "logo-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose() }
Write-Ico (Join-Path $here 'app.ico') @(16, 24, 32, 48, 64, 128, 256)
Write-Ico (Join-Path $here 'favicon.ico') @(16, 32, 48)

# Social preview: 1280 x 640
$W = 1280; $H = 640
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear($wood)
$ruleOuter = New-Object System.Drawing.Pen $gold, 4; $ruleInner = New-Object System.Drawing.Pen (C '#8a6c2c'), 1
$g.DrawRectangle($ruleOuter, 28, 28, $W - 56, $H - 56); $g.DrawRectangle($ruleInner, 44, 44, $W - 88, $H - 88)
$state = $g.Save(); $g.TranslateTransform(120, 150); Draw-Logo $g 340 $false; $g.Restore($state)
$title = New-Object System.Drawing.Font 'Palatino Linotype', 68, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$sub = New-Object System.Drawing.Font 'Palatino Linotype', 30, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$small = New-Object System.Drawing.Font 'Segoe UI', 22, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$gb = New-Object System.Drawing.SolidBrush $gold; $vb = New-Object System.Drawing.SolidBrush $vellum; $mb = New-Object System.Drawing.SolidBrush (C '#b9a77f')
$g.DrawString('Portrait Builder', $title, $gb, 520, 190)
$g.DrawString('A modernized fork for Crusader Kings II portraits', $sub, $vb, 524, 285)
$lines = 'Coded entirely by AI (Claude, by Anthropic).', 'Forked from earlier, human-written projects.', 'GPL-3.0. Not affiliated with Paradox Interactive.', 'Needs your own Crusader Kings II install.'
for ($i = 0; $i -lt $lines.Count; $i++) { $g.DrawString($lines[$i], $small, $mb, 526, 362 + 36 * $i) }
$g.Dispose(); $bmp.Save((Join-Path $here 'social-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
'done'
