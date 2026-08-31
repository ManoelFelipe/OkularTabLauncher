# SPDX-License-Identifier: MIT

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'assets\OkularTabLauncher.ico'))
$previewPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'assets\OkularTabLauncher.png'))

if (-not $outputPath.StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Icon output is outside the repository: $outputPath"
}

if (-not $previewPath.StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Preview output is outside the repository: $previewPath"
}

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPngBytes {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $stream = [System.IO.MemoryStream]::new()

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $scale = $Size / 256.0
        $background = New-RoundedRectanglePath (20 * $scale) (20 * $scale) (216 * $scale) (216 * $scale) (48 * $scale)
        $backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 22, 42, 68))
        $graphics.FillPath($backgroundBrush, $background)

        $rearPage = New-RoundedRectanglePath (66 * $scale) (48 * $scale) (124 * $scale) (148 * $scale) (13 * $scale)
        $rearBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 46, 196, 182))
        $graphics.FillPath($rearBrush, $rearPage)

        $frontPage = New-RoundedRectanglePath (48 * $scale) (68 * $scale) (142 * $scale) (150 * $scale) (14 * $scale)
        $frontBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 245, 248, 252))
        $graphics.FillPath($frontBrush, $frontPage)

        $tabBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 69, 218, 202))
        $graphics.FillRectangle($tabBrush, 78 * $scale, 52 * $scale, 52 * $scale, 24 * $scale)

        $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 241, 91, 93))
        $graphics.FillRectangle($accentBrush, 72 * $scale, 116 * $scale, 94 * $scale, 14 * $scale)
        $graphics.FillRectangle($accentBrush, 72 * $scale, 148 * $scale, 68 * $scale, 14 * $scale)
        $graphics.FillRectangle($accentBrush, 72 * $scale, 180 * $scale, 82 * $scale, 14 * $scale)

        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
        if ($background) { $background.Dispose() }
        if ($backgroundBrush) { $backgroundBrush.Dispose() }
        if ($rearPage) { $rearPage.Dispose() }
        if ($rearBrush) { $rearBrush.Dispose() }
        if ($frontPage) { $frontPage.Dispose() }
        if ($frontBrush) { $frontBrush.Dispose() }
        if ($tabBrush) { $tabBrush.Dispose() }
        if ($accentBrush) { $accentBrush.Dispose() }
    }
}

$sizes = @(16, 32, 48, 256)
$images = @($sizes | ForEach-Object { New-IconPngBytes -Size $_ })
$fileStream = [System.IO.File]::Create($outputPath)
$writer = [System.IO.BinaryWriter]::new($fileStream)

try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)

    $offset = 6 + (16 * $images.Count)
    for ($index = 0; $index -lt $images.Count; $index++) {
        $size = $sizes[$index]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

[System.IO.File]::WriteAllBytes($previewPath, $images[-1])

Write-Host "Generated $outputPath"
Write-Host "Generated $previewPath"
