param(
    [string]$SourcePath,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
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
public static class FloatingWizardArt
{
    public static void Isolate(Bitmap image)
    {
        int w=image.Width,h=image.Height,n=w*h;
        bool[] opaque=new bool[n],visited=new bool[n];
        int[] queue=new int[n],largest=null;
        for(int y=0;y<h;y++)
        for(int x=0;x<w;x++) opaque[y*w+x]=image.GetPixel(x,y).A>16;
        for(int seed=0;seed<n;seed++)
        {
            if(!opaque[seed] || visited[seed]) continue;
            int head=0,tail=1; queue[0]=seed; visited[seed]=true;
            while(head<tail)
            {
                int p=queue[head++],x=p%w,y=p/w;
                if(x>0) Add(p-1,opaque,visited,queue,ref tail);
                if(x<w-1) Add(p+1,opaque,visited,queue,ref tail);
                if(y>0) Add(p-w,opaque,visited,queue,ref tail);
                if(y<h-1) Add(p+w,opaque,visited,queue,ref tail);
            }
            if(largest==null || tail>largest.Length)
            {
                largest=new int[tail]; Array.Copy(queue,largest,tail);
            }
        }
        if(largest==null) throw new InvalidOperationException("Empty character cell");
        bool[] keep=new bool[n];
        foreach(int p in largest)
        {
            int x=p%w,y=p/w;
            for(int dy=-2;dy<=2;dy++)
            for(int dx=-2;dx<=2;dx++)
            {
                int px=x+dx,py=y+dy;
                if(px>=0 && px<w && py>=0 && py<h) keep[py*w+px]=true;
            }
        }
        for(int y=0;y<h;y++)
        for(int x=0;x<w;x++) if(!keep[y*w+x]) image.SetPixel(x,y,Color.Transparent);
    }
    private static void Add(int p,bool[] opaque,bool[] visited,int[] queue,ref int tail)
    {
        if(opaque[p] && !visited[p]) { visited[p]=true; queue[tail++]=p; }
    }
    public static Rectangle Bounds(Bitmap image)
    {
        int l=image.Width,t=image.Height,r=-1,b=-1;
        for(int y=0;y<image.Height;y++)
        for(int x=0;x<image.Width;x++)
        {
            if(image.GetPixel(x,y).A<=16) continue;
            l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);
        }
        if(r<0) throw new InvalidOperationException("Empty character cell");
        return Rectangle.FromLTRB(l,t,r+1,b+1);
    }
    public static float BodyCenter(Bitmap image,Rectangle bounds)
    {
        double weightedX=0,total=0;
        int start=bounds.Bottom-(int)(bounds.Height*0.22f);
        for(int y=start;y<bounds.Bottom;y++)
        for(int x=bounds.Left;x<bounds.Right;x++)
        {
            double alpha=image.GetPixel(x,y).A/255.0;
            weightedX+=(x+0.5)*alpha;total+=alpha;
        }
        return (float)(weightedX/total);
    }
}
'@

if (-not $SourcePath) { $SourcePath = Join-Path $PSScriptRoot 'directions-transparent.png' }
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot '../../UnityProject/Assets/_Game/Resources/Art/FloatingWizard/Idle'
}
$source = [System.Drawing.Bitmap]::new([System.IO.Path]::GetFullPath($SourcePath))
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$names = @('front','left','right','back')
$frames = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
try {
    for($i=0;$i -lt 4;$i++) {
        $column=$i%2; $row=[int][Math]::Floor($i/2)
        $left=[Math]::Max(0,[int]($column*$source.Width/2)-32)
        $top=[Math]::Max(0,[int]($row*$source.Height/2)-32)
        $right=[Math]::Min($source.Width,[int](($column+1)*$source.Width/2)+32)
        $bottom=[Math]::Min($source.Height,[int](($row+1)*$source.Height/2)+32)
        $frame=$source.Clone([System.Drawing.Rectangle]::FromLTRB($left,$top,$right,$bottom),
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        [FloatingWizardArt]::Isolate($frame)
        $frames.Add($frame)
    }
    $bounds=@($frames | ForEach-Object { [FloatingWizardArt]::Bounds($_) })
    $scale=440.0/($bounds | ForEach-Object Height | Measure-Object -Maximum).Maximum
    $centers=for($i=0;$i -lt 4;$i++){ [FloatingWizardArt]::BodyCenter($frames[$i],$bounds[$i]) }
    for($i=0;$i -lt 4;$i++) {
        $extent=[Math]::Max($centers[$i]-$bounds[$i].Left,$bounds[$i].Right-$centers[$i])
        $scale=[Math]::Min($scale,234.0/$extent)
    }
    New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
    for($i=0;$i -lt 4;$i++) {
        $canvas=[System.Drawing.Bitmap]::new(512,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics=[System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $rect=[System.Drawing.RectangleF]::new([single](256-$centers[$i]*$scale),
                [single](494-$bounds[$i].Bottom*$scale),[single]($frames[$i].Width*$scale),[single]($frames[$i].Height*$scale))
            $graphics.DrawImage($frames[$i],$rect)
            $canvas.Save((Join-Path $outputRoot ('idle_'+$names[$i]+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose();$canvas.Dispose() }
    }
    Write-Output 'Imported floating wizard: front, left, right, back; 512 x 512, common scale, robe baseline 494.'
}
finally {
    foreach($frame in $frames){$frame.Dispose()}
    $source.Dispose()
}
