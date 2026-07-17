# Génère icon.ico : deux flèches dorées en cercle (la conversion !) sur
# fond bleu nuit arrondi, style famille Stargazer. Aucun asset externe.
Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# --- carré arrondi, dégradé bleu nuit ---
$margin = 10
$rect = New-Object System.Drawing.Rectangle($margin, $margin, ($size - 2*$margin), ($size - 2*$margin))
$radius = 52; $d = $radius * 2
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
$path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
$path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
$path.CloseFigure()
$c1 = [System.Drawing.Color]::FromArgb(255, 26, 34, 74)
$c2 = [System.Drawing.Color]::FromArgb(255, 8, 11, 30)
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 90)
$g.FillPath($bg, $path)

# --- deux arcs dorés avec pointes de flèche (cycle de conversion) ---
$gold = [System.Drawing.Color]::FromArgb(255, 226, 190, 92)
$pen = New-Object System.Drawing.Pen($gold, 20)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

# cercle de référence : centre (128,128), rayon 62 -> boîte (66,66,124,124)
$g.DrawArc($pen, 66, 66, 124, 124, 250, 130)   # arc haut
$g.DrawArc($pen, 66, 66, 124, 124, 70, 130)    # arc bas
$pen.Dispose()

$brush = New-Object System.Drawing.SolidBrush($gold)
# pointe de la flèche haute (fin d'arc vers ~20°, tourne horaire -> pointe vers le bas-droite)
$g.FillPolygon($brush, @(
    (New-Object System.Drawing.Point(212, 96)),
    (New-Object System.Drawing.Point(178, 88)),
    (New-Object System.Drawing.Point(196, 130))
))
# pointe de la flèche basse (symétrique)
$g.FillPolygon($brush, @(
    (New-Object System.Drawing.Point(44, 160)),
    (New-Object System.Drawing.Point(78, 168)),
    (New-Object System.Drawing.Point(60, 126))
))

# --- étoile signature au centre ---
function Star-Path($cx, $cy, $r, $w) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddPolygon(@(
        (New-Object System.Drawing.Point($cx, ($cy - $r))),
        (New-Object System.Drawing.Point(($cx + $w), ($cy - $w))),
        (New-Object System.Drawing.Point(($cx + $r), $cy)),
        (New-Object System.Drawing.Point(($cx + $w), ($cy + $w))),
        (New-Object System.Drawing.Point($cx, ($cy + $r))),
        (New-Object System.Drawing.Point(($cx - $w), ($cy + $w))),
        (New-Object System.Drawing.Point(($cx - $r), $cy)),
        (New-Object System.Drawing.Point(($cx - $w), ($cy - $w)))
    ))
    return $p
}
$g.FillPath($brush, (Star-Path 128 128 26 6))

$g.Dispose()

# --- décliner en plusieurs tailles, empaquetées en ICO (entrées PNG) ---
$sizes = @(256, 128, 64, 48, 32, 24, 16)
$pngs = @()
foreach ($s in $sizes) {
    if ($s -eq 256) { $b = $bmp } else {
        $b = New-Object System.Drawing.Bitmap($s, $s)
        $gg = [System.Drawing.Graphics]::FromImage($b)
        $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gg.DrawImage($bmp, 0, 0, $s, $s)
        $gg.Dispose()
    }
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,@($s, $ms.ToArray())
    $ms.Dispose()
    if ($s -ne 256) { $b.Dispose() }
}
$bmp.Dispose()

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($e in $pngs) {
    $s = $e[0]; $data = $e[1]
    $dim = if ($s -eq 256) { 0 } else { $s }
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim); $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($e in $pngs) { $bw.Write($e[1]) }
$bw.Flush()

$iconPath = Join-Path $PSScriptRoot "icon.ico"
[System.IO.File]::WriteAllBytes($iconPath, $out.ToArray())
$out.Dispose()
Write-Output "OK -> $iconPath"
