param(
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][int]$GridSize,
    [string]$Prefix = 'prop'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
[System.Drawing.Graphics].GetInterfaces() | Out-Null
$refs=@([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Rectangle].Assembly.Location,[System.ComponentModel.Component].Assembly.Location) + @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -like 'System.Private.Windows.*' } | ForEach-Object Location)
Add-Type -ReferencedAssemblies $refs -TypeDefinition @'
using System;
using System.Drawing;
public static class SwampAtlasArt {
 struct Cell { public int Area; public Rectangle Rect; }
 public static Rectangle[] Cells(Bitmap b,int grid) {
  int w=b.Width,h=b.Height,n=w*h;bool[] mask=new bool[n],seen=new bool[n];int[] q=new int[n];
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)mask[y*w+x]=b.GetPixel(x,y).A>24;
  Cell[] cells=new Cell[256];int cellCount=0;
  for(int seed=0;seed<n;seed++) {
   if(!mask[seed]||seen[seed])continue;int head=0,tail=1,l=w,t=h,r=0,bot=0;q[0]=seed;seen[seed]=true;
   while(head<tail){int p=q[head++],x=p%w,y=p/w;l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bot=Math.Max(bot,y);
    if(x>0)Add(p-1,mask,seen,q,ref tail);if(x<w-1)Add(p+1,mask,seen,q,ref tail);
    if(y>0)Add(p-w,mask,seen,q,ref tail);if(y<h-1)Add(p+w,mask,seen,q,ref tail);}
   if(tail>1000 && cellCount<cells.Length)cells[cellCount++]=new Cell { Area=tail,Rect=Rectangle.FromLTRB(Math.Max(0,l-4),Math.Max(0,t-4),Math.Min(w,r+5),Math.Min(h,bot+5)) };
  }
  for(int i=0;i<cellCount;i++)for(int j=i+1;j<cellCount;j++)if(cells[j].Area>cells[i].Area){Cell c=cells[i];cells[i]=cells[j];cells[j]=c;}
  int count=grid*grid;
  if(cellCount<count)throw new InvalidOperationException("Atlas objects are connected or missing; expected "+count+", got "+cellCount);
  for(int i=0;i<count;i++)for(int j=i+1;j<count;j++)if(cells[j].Rect.Top+cells[j].Rect.Bottom<cells[i].Rect.Top+cells[i].Rect.Bottom){Cell c=cells[i];cells[i]=cells[j];cells[j]=c;}
  var result=new Rectangle[count];
  for(int row=0;row<grid;row++){
   int start=row*grid;for(int i=start;i<start+grid;i++)for(int j=i+1;j<start+grid;j++)if(cells[j].Rect.Left<cells[i].Rect.Left){Cell c=cells[i];cells[i]=cells[j];cells[j]=c;}
   for(int col=0;col<grid;col++)result[row*grid+col]=cells[start+col].Rect;}
  return result;
 }
 public static Bitmap Extract(Bitmap source,Rectangle cell) {
  using(var b=source.Clone(cell,System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
   int w=b.Width,h=b.Height,n=w*h; bool[] mask=new bool[n],seen=new bool[n]; int[] q=new int[n],largest=null;
   for(int y=0;y<h;y++)for(int x=0;x<w;x++)mask[y*w+x]=b.GetPixel(x,y).A>24;
   for(int seed=0;seed<n;seed++) {
    if(!mask[seed]||seen[seed])continue;int head=0,tail=1;q[0]=seed;seen[seed]=true;
    while(head<tail) {
     int p=q[head++],x=p%w,y=p/w;
     if(x>0)Add(p-1,mask,seen,q,ref tail);if(x<w-1)Add(p+1,mask,seen,q,ref tail);
     if(y>0)Add(p-w,mask,seen,q,ref tail);if(y<h-1)Add(p+w,mask,seen,q,ref tail);
    }
    if(largest==null||tail>largest.Length){largest=new int[tail];Array.Copy(q,largest,tail);}
   }
   if(largest==null)throw new InvalidOperationException("Empty atlas cell");
   int l=w,t=h,r=0,bot=0;bool[] keep=new bool[n];
   foreach(int p in largest){int x=p%w,y=p/w;l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bot=Math.Max(bot,y);
    for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++){int px=x+dx,py=y+dy;if(px>=0&&px<w&&py>=0&&py<h)keep[py*w+px]=true;}}
   for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(!keep[y*w+x])b.SetPixel(x,y,Color.Transparent);
   int pad=4;int left=Math.Max(0,l-pad),top=Math.Max(0,t-pad),right=Math.Min(w,r+pad+1),bottom=Math.Min(h,bot+pad+1);
   return b.Clone(Rectangle.FromLTRB(left,top,right,bottom),System.Drawing.Imaging.PixelFormat.Format32bppArgb);
  }
 }
 static void Add(int p,bool[] mask,bool[] seen,int[] q,ref int tail){if(mask[p]&&!seen[p]){seen[p]=true;q[tail++]=p;}}
}
'@
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$source=[System.Drawing.Bitmap]::new([IO.Path]::GetFullPath($SourcePath))
try {
    $cells=[SwampAtlasArt]::Cells($source,$GridSize)
    for($i=0;$i -lt ($GridSize*$GridSize);$i++) {
        $frame=[SwampAtlasArt]::Extract($source,$cells[$i])
        try {$frame.Save((Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ($Prefix+'_'+$i+'.png')),[Drawing.Imaging.ImageFormat]::Png)} finally {$frame.Dispose()}
    }
    Write-Output "Imported $($GridSize*$GridSize) isolated $Prefix sprites."
} finally {$source.Dispose()}
