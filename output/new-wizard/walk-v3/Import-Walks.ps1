param(
    [switch]$MeasureOnly,
    [ValidateSet('front','back','left','right')]
    [string[]]$Directions = @('front','back','left','right')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Import-Character02.ps1') -FunctionsOnly

[System.Drawing.Graphics].GetInterfaces() | Out-Null
$drawingReferences = @(
    ([System.Drawing.Bitmap].Assembly.Location),
    ([System.Drawing.Rectangle].Assembly.Location),
    ([System.ComponentModel.Component].Assembly.Location)
) + @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object {
    $_.GetName().Name -like 'System.Private.Windows.*'
} | ForEach-Object Location)
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class Character02WalkAlignment
{
    public static Rectangle Bounds(Bitmap image)
    {
        int l=image.Width, t=image.Height, r=-1, b=-1;
        for(int y=0;y<image.Height;y++)
        for(int x=0;x<image.Width;x++)
        {
            if(image.GetPixel(x,y).A <= 16) continue;
            l=Math.Min(l,x); t=Math.Min(t,y); r=Math.Max(r,x); b=Math.Max(b,y);
        }
        if(r<0) throw new InvalidOperationException("Empty animation cell");
        return Rectangle.FromLTRB(l,t,r+1,b+1);
    }
    public static Rectangle HatBounds(Bitmap image)
    {
        Rectangle body=Bounds(image);
        // Stop above the blue scarf so its pixels cannot inflate the measured hat.
        int limit=body.Top+(int)(body.Height*0.52f);
        int l=image.Width, t=image.Height, r=-1, b=-1;
        for(int y=body.Top;y<limit;y++)
        for(int x=body.Left;x<body.Right;x++)
        {
            Color c=image.GetPixel(x,y);
            if(c.A<=32 || c.B<=45 || c.B<=c.R*1.08f || c.B<=c.G*1.05f) continue;
            l=Math.Min(l,x); t=Math.Min(t,y); r=Math.Max(r,x); b=Math.Max(b,y);
        }
        if(r<0) throw new InvalidOperationException("Blue reference hat not found");
        return Rectangle.FromLTRB(l,t,r+1,b+1);
    }
    public static int CollarRow(Bitmap image, Rectangle hat)
    {
        Rectangle body=Bounds(image);
        int center=hat.Left+hat.Width/2;
        int radius=Math.Max(8,hat.Width/10);
        int left=Math.Max(0,center-radius), right=Math.Min(image.Width,center+radius);
        int limit=body.Top+(int)(body.Height*0.80f), streak=0;
        for(int y=hat.Bottom+6;y<limit;y++)
        {
            int blue=0;
            for(int x=left;x<right;x++)
            {
                Color c=image.GetPixel(x,y);
                if(c.A>32 && c.B>45 && c.B>c.R*1.08f && c.B>c.G*1.05f) blue++;
            }
            streak=blue>(right-left)*0.20f ? streak+1 : 0;
            if(streak==3) return y-2;
        }
        throw new InvalidOperationException("Blue collar landmark not found");
    }
    public static Bitmap Register(Bitmap image, Rectangle hat, Rectangle referenceHat,
        int sourceCollar, int referenceCollar, float sourceBaseline)
    {
        const int size=512;
        const float targetBaseline=494;
        Bitmap result=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        float sx=(float)referenceHat.Width/hat.Width;
        float sy=(float)referenceHat.Height/hat.Height;
        float x=referenceHat.Left+referenceHat.Width*0.5f-(hat.Left+hat.Width*0.5f)*sx;
        float top=referenceHat.Top-hat.Top*sy;
        // Register the hat, face/collar and feet independently so matching the hat does not squash the face.
        float faceScale=(float)(referenceCollar-referenceHat.Bottom)/(sourceCollar-hat.Bottom);
        float bodyScale=(targetBaseline-referenceCollar)/(sourceBaseline-sourceCollar);
        using(Graphics g=Graphics.FromImage(result))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            g.SetClip(new Rectangle(0,0,size,referenceHat.Bottom));
            g.DrawImage(image,new RectangleF(x,top,image.Width*sx,image.Height*sy));
            g.ResetClip();
            g.SetClip(new Rectangle(0,referenceHat.Bottom,size,referenceCollar-referenceHat.Bottom));
            g.DrawImage(image,new RectangleF(x,referenceHat.Bottom-hat.Bottom*faceScale,
                image.Width*sx,image.Height*faceScale));
            g.ResetClip();
            g.SetClip(new Rectangle(0,referenceCollar,size,size-referenceCollar));
            g.DrawImage(image,new RectangleF(x,referenceCollar-sourceCollar*bodyScale,
                image.Width*sx,image.Height*bodyScale));
        }
        return result;
    }
}
'@

$resourceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../UnityProject/Assets/_Game/Resources/Art/Character02'))
foreach ($direction in $Directions) {
    $sourceFolder = $PSScriptRoot
    $armRevision = Join-Path $PSScriptRoot '../walk-v4-arms'
    if (Test-Path -LiteralPath (Join-Path $armRevision "walk-$direction-4-frames.png")) {
        $sourceFolder = $armRevision
    }
    $reference = [System.Drawing.Bitmap]::new((Join-Path $resourceRoot "Idle/idle_$direction.png"))
    $sheet = [System.Drawing.Bitmap]::new((Join-Path $sourceFolder "walk-$direction-4-frames.png"))
    $frames = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    try {
        $referenceHat = [Character02WalkAlignment]::HatBounds($reference)
        $referenceBody = [Character02WalkAlignment]::Bounds($reference)
        $referenceCollar = [Character02WalkAlignment]::CollarRow($reference,$referenceHat)
        for ($i=0; $i -lt 4; $i++) {
            $left=[Math]::Max(0,[int]($i*$sheet.Width/4)-64)
            $right=[Math]::Min($sheet.Width,[int](($i+1)*$sheet.Width/4)+64)
            $rect=[System.Drawing.Rectangle]::new($left,0,$right-$left,$sheet.Height)
            $frame=$sheet.Clone($rect,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            [Character02CellCleanup]::TrimBleed($frame)
            $frames.Add($frame)
        }
        $hats=@($frames | ForEach-Object { [Character02WalkAlignment]::HatBounds($_) })
        $bodies=@($frames | ForEach-Object { [Character02WalkAlignment]::Bounds($_) })
        if ($MeasureOnly) {
            [pscustomobject]@{
                direction=$direction
                idleHat=@($referenceHat.X,$referenceHat.Y,$referenceHat.Width,$referenceHat.Height)
                idleBody=@($referenceBody.X,$referenceBody.Y,$referenceBody.Width,$referenceBody.Height)
                walkHat=$hats | ForEach-Object { @($_.X,$_.Y,$_.Width,$_.Height) -join ',' }
                walkBody=$bodies | ForEach-Object { @($_.X,$_.Y,$_.Width,$_.Height) -join ',' }
            } | ConvertTo-Json -Compress
            continue
        }

        $folder = (Get-Culture).TextInfo.ToTitleCase($direction)
        $destination = Join-Path $resourceRoot $folder
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        $baseline = ($bodies | ForEach-Object Bottom | Measure-Object -Maximum).Maximum
        $registeredSheet = [System.Drawing.Bitmap]::new(2048,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $sheetGraphics = [System.Drawing.Graphics]::FromImage($registeredSheet)
        try {
            $sheetGraphics.Clear([System.Drawing.Color]::Transparent)
            for ($i=0; $i -lt 4; $i++) {
                $sourceCollar = [Character02WalkAlignment]::CollarRow($frames[$i],$hats[$i])
                $registered = [Character02WalkAlignment]::Register($frames[$i], $hats[$i], $referenceHat,
                    $sourceCollar, $referenceCollar, $baseline)
                try {
                    $registered.Save((Join-Path $destination ('walk_{0:d2}.png' -f $i)),[System.Drawing.Imaging.ImageFormat]::Png)
                    $sheetGraphics.DrawImageUnscaled($registered,$i*512,0)
                }
                finally { $registered.Dispose() }
            }
            $registeredSheet.Save((Join-Path $sourceFolder "walk-$direction-registered.png"),[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $sheetGraphics.Dispose(); $registeredSheet.Dispose() }
        Write-Output ("Imported {0}: 4 frames, hat registered to idle ({1} x {2}px), idle unchanged" -f $direction,$referenceHat.Width,$referenceHat.Height)
    }
    finally {
        foreach($frame in $frames) { $frame.Dispose() }
        $sheet.Dispose(); $reference.Dispose()
    }
}
