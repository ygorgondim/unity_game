param(
    [string]$FrontGif = (Join-Path $PSScriptRoot 'walk-front-source.gif'),
    [string]$SideGif = (Join-Path $PSScriptRoot 'walk-side-source.gif'),
    [switch]$IdleOnly,
    [switch]$FunctionsOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# Trim neighboring-cell fragments while retaining the complete main sprite and its antialiased edge.
Add-Type -ReferencedAssemblies ([System.Drawing.Bitmap].Assembly.Location),
    ([System.Drawing.Rectangle].Assembly.Location), ([System.ComponentModel.Component].Assembly.Location) -TypeDefinition @'
using System;
using System.Drawing;
public static class Character02CellCleanup
{
    public static void TrimBleed(Bitmap bitmap)
    {
        int w = bitmap.Width, h = bitmap.Height, n = w * h;
        bool[] opaque = new bool[n], visited = new bool[n];
        int[] queue = new int[n];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++) opaque[y * w + x] = bitmap.GetPixel(x, y).A > 16;
        int best = 0;
        int[] mainPixels = null;
        for (int seed = 0; seed < n; seed++)
        {
            if (!opaque[seed] || visited[seed]) continue;
            int head = 0, tail = 1;
            queue[0] = seed; visited[seed] = true;
            while (head < tail)
            {
                int index = queue[head++], x = index % w, y = index / w;
                if (x > 0) Add(index - 1, opaque, visited, queue, ref tail);
                if (x < w - 1) Add(index + 1, opaque, visited, queue, ref tail);
                if (y > 0) Add(index - w, opaque, visited, queue, ref tail);
                if (y < h - 1) Add(index + w, opaque, visited, queue, ref tail);
            }
            if (tail > best)
            {
                best = tail;
                mainPixels = new int[tail];
                Array.Copy(queue, mainPixels, tail);
            }
        }
        if (mainPixels == null) return;
        bool[] keep = new bool[n];
        foreach (int index in mainPixels)
        {
            int x = index % w, y = index / w;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int px = x + dx, py = y + dy;
                if (px >= 0 && px < w && py >= 0 && py < h) keep[py * w + px] = true;
            }
        }
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if (!keep[y * w + x])
                bitmap.SetPixel(x, y, Color.Transparent);
    }
    private static void Add(int index, bool[] opaque, bool[] visited, int[] queue, ref int tail)
    {
        if (opaque[index] && !visited[index])
        {
            visited[index] = true; queue[tail++] = index;
        }
    }
}
'@
$characterOutput = Join-Path $PSScriptRoot '../../UnityProject/Assets/_Game/Resources/Art/Character02'
$canvasSize = 512
$footBaseline = 494
$visibleHeight = 460

function Get-OpaqueBounds([System.Drawing.Bitmap]$Bitmap) {
    $left = $Bitmap.Width; $top = $Bitmap.Height; $right = -1; $bottom = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -le 16) { continue }
            $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
            $top = [Math]::Min($top, $y); $bottom = [Math]::Max($bottom, $y)
        }
    }
    if ($right -lt 0) { throw 'An animation cell contains no visible character.' }
    return [System.Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
}

function Get-FootCenter([System.Drawing.Bitmap]$Bitmap, [System.Drawing.Rectangle]$Bounds) {
    $left = $Bitmap.Width; $right = -1
    $startY = $Bounds.Bottom - [int]($Bounds.Height * 0.13)
    for ($y = $startY; $y -lt $Bounds.Bottom; $y++) {
        for ($x = $Bounds.Left; $x -lt $Bounds.Right; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -le 32) { continue }
            $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
        }
    }
    return ($left + $right + 1) * 0.5
}

function Export-Frames([System.Drawing.Bitmap[]]$Frames, [string]$Folder, [string[]]$Names,
    [bool]$DirectionalIdle = $false) {
    $bounds = @($Frames | ForEach-Object { Get-OpaqueBounds $_ })
    $centers = for ($i = 0; $i -lt $Frames.Length; $i++) { Get-FootCenter $Frames[$i] $bounds[$i] }
    $top = ($bounds | ForEach-Object Top | Measure-Object -Minimum).Minimum
    $bottom = ($bounds | ForEach-Object Bottom | Measure-Object -Maximum).Maximum
    $commonCenter = ($centers | Measure-Object -Average).Average
    $scale = $visibleHeight / ($bottom - $top)
    # Leave enough padding for the identity outline, even for the widest hat frame.
    for ($i = 0; $i -lt $Frames.Length; $i++) {
        $center = if ($DirectionalIdle) { $centers[$i] } else { $commonCenter }
        $extent = [Math]::Max($center - $bounds[$i].Left, $bounds[$i].Right - $center)
        $scale = [Math]::Min($scale, ($canvasSize * 0.5 - 18) / $extent)
    }
    $destination = Join-Path $characterOutput $Folder
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    for ($i = 0; $i -lt $Frames.Length; $i++) {
        $center = if ($DirectionalIdle) { $centers[$i] } else { $commonCenter }
        $baseline = if ($DirectionalIdle) { $bounds[$i].Bottom } else { $bottom }
        $canvas = [System.Drawing.Bitmap]::new($canvasSize, $canvasSize,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $rect = [System.Drawing.RectangleF]::new(
                [single]($canvasSize * 0.5 - $center * $scale), [single]($footBaseline - $baseline * $scale),
                [single]($Frames[$i].Width * $scale), [single]($Frames[$i].Height * $scale))
            $graphics.DrawImage($Frames[$i], $rect)
            $canvas.Save((Join-Path $destination ($Names[$i] + '.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $canvas.Dispose() }
    }
    Write-Output ("Imported {0}: {1} frames, scale {2:N3}" -f $Folder, $Frames.Length, $scale)
}

function Import-Gif([string]$Path, [string]$Folder) {
    $source = [System.Drawing.Image]::FromFile($Path)
    $frames = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    try {
        $dimension = [System.Drawing.Imaging.FrameDimension]::new($source.FrameDimensionsList[0])
        $count = $source.GetFrameCount($dimension)
        for ($i = 0; $i -lt $count; $i++) {
            $source.SelectActiveFrame($dimension, $i) | Out-Null
            $frame = [System.Drawing.Bitmap]::new($source.Width, $source.Height,
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($frame)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.DrawImageUnscaled($source, 0, 0)
            }
            finally { $graphics.Dispose() }
            $frames.Add($frame)
        }
        $names = for ($i = 0; $i -lt $count; $i++) { 'walk_{0:d2}' -f $i }
        Export-Frames $frames.ToArray() $Folder $names
    }
    finally {
        foreach ($frame in $frames) { $frame.Dispose() }
        $source.Dispose()
    }
}

function Import-Sheet([string]$Path, [string]$Folder, [string[]]$Names, [bool]$DirectionalIdle = $false) {
    $source = [System.Drawing.Bitmap]::new($Path)
    $frames = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    try {
        $cellWidth = $source.Width / $Names.Length
        for ($i = 0; $i -lt $Names.Length; $i++) {
            # The idle hats extend beyond the nominal cell boundaries. Recover the overhang
            # first, then isolate the main character instead of cutting the hat at the cell edge.
            $margin = if ($DirectionalIdle) { 64 } else { 0 }
            $left = [Math]::Max(0, [int]($i * $cellWidth) - $margin)
            $right = [Math]::Min($source.Width, [int](($i + 1) * $cellWidth) + $margin)
            $rect = [System.Drawing.Rectangle]::new($left, 0, $right - $left, $source.Height)
            $frame = $source.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            [Character02CellCleanup]::TrimBleed($frame)
            if ($DirectionalIdle -and $i -lt 2) {
                $frame.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
            }
            $frames.Add($frame)
        }
        Export-Frames $frames.ToArray() $Folder $Names $DirectionalIdle
    }
    finally {
        foreach ($frame in $frames) { $frame.Dispose() }
        $source.Dispose()
    }
}

if ($FunctionsOnly) { return }
$revisedWalkImporter = Join-Path $PSScriptRoot 'walk-v3/Import-Walks.ps1'
if (-not $IdleOnly -and (Test-Path -LiteralPath $revisedWalkImporter)) {
    # Current walking assets are based on the corrected idles; do not restore the obsolete GIF walks.
    & $revisedWalkImporter
    return
}
if (-not $IdleOnly) {
    Import-Gif $FrontGif 'Front'
    Import-Gif $SideGif 'Side'
    Import-Sheet (Join-Path $PSScriptRoot 'walk-back-4-frames-v2.png') 'Back' @('walk_00','walk_01','walk_02','walk_03')
}
Import-Sheet (Join-Path $PSScriptRoot 'idle-4-directions.png') 'Idle' @('idle_front','idle_back','idle_left','idle_right') $true
