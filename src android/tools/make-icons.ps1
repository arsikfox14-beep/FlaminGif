param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Add-Type -AssemblyName System.Drawing

$densities = @(
    @{ Name = 'mdpi'; Size = 48 },
    @{ Name = 'hdpi'; Size = 72 },
    @{ Name = 'xhdpi'; Size = 96 },
    @{ Name = 'xxhdpi'; Size = 144 },
    @{ Name = 'xxxhdpi'; Size = 192 }
)

function New-RoundPath([float]$size, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Icon([int]$size, [switch]$Round) {
    $scale = 4
    $big = $size * $scale
    $bmp = New-Object System.Drawing.Bitmap($big, $big)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    if ($Round) {
        $shape = New-Object System.Drawing.Drawing2D.GraphicsPath
        $shape.AddEllipse(0, 0, $big, $big)
    } else {
        $shape = New-RoundPath $big ($big * 0.22)
    }

    $rect = New-Object System.Drawing.RectangleF(0, 0, $big, $big)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 77, 125, 255),
        [System.Drawing.Color]::FromArgb(255, 154, 77, 255),
        45.0)
    $g.FillPath($brush, $shape)

    $points = @(
        (New-Object System.Drawing.PointF(($big * 0.365), ($big * 0.295))),
        (New-Object System.Drawing.PointF(($big * 0.365), ($big * 0.705))),
        (New-Object System.Drawing.PointF(($big * 0.645), ($big * 0.5)))
    )
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillPolygon($white, $points)

    $small = New-Object System.Drawing.Bitmap($size, $size)
    $g2 = [System.Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.DrawImage($bmp, 0, 0, $size, $size)
    $g2.Dispose()

    $white.Dispose()
    $brush.Dispose()
    $shape.Dispose()
    $g.Dispose()
    $bmp.Dispose()
    return $small
}

foreach ($d in $densities) {
    $dir = Join-Path $Root "res\mipmap-$($d.Name)"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $square = New-Icon $d.Size
    $square.Save((Join-Path $dir 'ic_launcher.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $square.Dispose()
    $round = New-Icon $d.Size -Round
    $round.Save((Join-Path $dir 'ic_launcher_round.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $round.Dispose()
    Write-Host "$($d.Name): $($d.Size)px"
}

$store = Join-Path $Root 'store'
New-Item -ItemType Directory -Force -Path $store | Out-Null
$big = New-Icon 512
$big.Save((Join-Path $store 'icon-512.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$big.Dispose()
Write-Host 'store/icon-512.png'
