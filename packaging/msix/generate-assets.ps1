# Renders the MSIX logo assets (the "VD" rounded tile) at every size the package needs.
# Run from the repo root:  pwsh packaging/msix/generate-assets.ps1
# Regenerate whenever the icon design changes; the PNGs are committed so CI needs no image tooling.
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot 'Assets'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function New-Tile([int]$w, [int]$h, [string]$path, [bool]$plated) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $side = [Math]::Min($w, $h)
    $margin = [Math]::Max(1, [int]($side * 0.06))
    $rect = New-Object System.Drawing.Rectangle($margin, $margin, ($w - 2*$margin), ($h - 2*$margin))
    $radius = [int]($side * 0.18)
    $d = $radius * 2

    $path2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path2.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path2.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path2.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path2.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path2.CloseFigure()

    if ($plated) {
        $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 30, 30))
        $g.FillPath($bg, $path2)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 90, 160, 250)), ([Math]::Max(1.0, $side * 0.05))
        $g.DrawPath($pen, $path2)
        $pen.Dispose(); $bg.Dispose()
    }

    $fontSize = [Math]::Max(6.0, $side * 0.42)
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString('VD', $font, $fg, (New-Object System.Drawing.RectangleF(0, 0, $w, $h)), $sf)

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
