# Renders the DeskCue mark (the "DC" rounded tile) at every size the package needs, plus the
# Windows exe icon (../../app.ico). One script for both so the Store tiles, the taskbar icon and
# the installer icon can never drift apart.
# Run from the repo root:  pwsh packaging/msix/generate-assets.ps1
# Regenerate whenever the icon design changes; the PNGs are committed so CI needs no image tooling.
# The tray icon is drawn in code from the same recipe — see BuildIcon() in App.xaml.cs and
# IconFactory.cs in the Linux build; keep the three in step.
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot 'Assets'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Draws the mark onto an existing Graphics. Split out from New-Tile so the .ico writer below
# can render the same artwork without going through a PNG file first.
function Draw-Tile([System.Drawing.Graphics]$g, [int]$w, [int]$h, [bool]$plated) {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $side = [Math]::Min($w, $h)
    $margin = [Math]::Max(1, [int]($side * 0.06))
    $rect = New-Object System.Drawing.Rectangle($margin, $margin, ($w - 2*$margin), ($h - 2*$margin))
    $radius = [int]($side * 0.18)
    $d = $radius * 2

    $shape = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shape.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $shape.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $shape.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $shape.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $shape.CloseFigure()

    if ($plated) {
        $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 30, 30))
        $g.FillPath($bg, $shape)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 90, 160, 250)), ([Math]::Max(1.0, $side * 0.05))
        $g.DrawPath($pen, $shape)
        $pen.Dispose(); $bg.Dispose()
    }

    # "DC" is a letter wider than "VD" was, so the glyph is set a touch smaller to keep the
    # same optical margin inside the plate.
    $fontSize = [Math]::Max(6.0, $side * 0.38)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString('DC', $font, $fg, (New-Object System.Drawing.RectangleF(0, 0, $w, $h)), $sf)

    $sf.Dispose(); $fg.Dispose(); $font.Dispose(); $shape.Dispose()
}

function New-Tile([int]$w, [int]$h, [string]$path, [bool]$plated) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Draw-Tile $g $w $h $plated
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "wrote $path ($w x $h)"
}

# Tiles get the plated (rounded dark) background; the unplated app-list icon is glyph-only.
New-Tile 44  44  (Join-Path $outDir 'Square44x44Logo.png')  $true
New-Tile 150 150 (Join-Path $outDir 'Square150x150Logo.png') $true
New-Tile 71  71  (Join-Path $outDir 'Square71x71Logo.png')   $true
New-Tile 310 310 (Join-Path $outDir 'Square310x310Logo.png') $true
New-Tile 310 150 (Join-Path $outDir 'Wide310x150Logo.png')   $true
New-Tile 50  50  (Join-Path $outDir 'StoreLogo.png')         $true
New-Tile 620 300 (Join-Path $outDir 'SplashScreen.png')      $true
# Unplated target-size variants for the taskbar/app-list icon.
New-Tile 44  44  (Join-Path $outDir 'Square44x44Logo.targetsize-44_altform-unplated.png') $false
New-Tile 24  24  (Join-Path $outDir 'Square44x44Logo.targetsize-24_altform-unplated.png') $false
New-Tile 16  16  (Join-Path $outDir 'Square44x44Logo.targetsize-16_altform-unplated.png') $false
New-Tile 256 256 (Join-Path $outDir 'Square44x44Logo.targetsize-256_altform-unplated.png') $false

# --- app.ico -------------------------------------------------------------------
# The exe / installer / window icon. Written by hand because System.Drawing's Icon.Save only
# round-trips an icon it already loaded: an ICO is a 6-byte ICONDIR, one 16-byte entry per
# image, then the payloads. PNG payloads (rather than BMP) keep the 256x256 entry small and
# are understood by Windows Vista and later.
function New-Ico([int[]]$sizes, [string]$path) {
    $images = @()
    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($size, $size)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        Draw-Tile $g $size $size $true
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $images += ,@{ Size = $size; Bytes = $ms.ToArray() }
        $ms.Dispose()
    }

    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($out)
    $w.Write([UInt16]0)                    # reserved
    $w.Write([UInt16]1)                    # resource type: icon
    $w.Write([UInt16]$images.Count)
    $offset = 6 + 16 * $images.Count       # payloads start after the directory
    foreach ($img in $images) {
        # 256 is stored as 0 in the single-byte width/height fields.
        $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
        $w.Write([Byte]$dim); $w.Write([Byte]$dim)
        $w.Write([Byte]0)                  # palette entries (0 = no palette)
        $w.Write([Byte]0)                  # reserved
        $w.Write([UInt16]1)                # colour planes
        $w.Write([UInt16]32)               # bits per pixel
        $w.Write([UInt32]$img.Bytes.Length)
        $w.Write([UInt32]$offset)
        $offset += $img.Bytes.Length
    }
    foreach ($img in $images) { $w.Write($img.Bytes) }
    $w.Flush()
    [System.IO.File]::WriteAllBytes($path, $out.ToArray())
    $w.Dispose(); $out.Dispose()
    Write-Host "wrote $path ($($images.Count) sizes: $($sizes -join ', '))"
}

New-Ico @(16, 24, 32, 48, 64, 128, 256) (Join-Path $PSScriptRoot '..\..\app.ico')
