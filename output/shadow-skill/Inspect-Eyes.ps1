Add-Type -AssemblyName System.Drawing
[System.Drawing.Graphics].GetInterfaces() | Out-Null
$refs = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Rectangle].Assembly.Location, [System.ComponentModel.Component].Assembly.Location) + @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -like 'System.Private.Windows.*' } | ForEach-Object Location)
Add-Type -ReferencedAssemblies $refs -TypeDefinition @'
using System;
using System.Drawing;
public static class EyeRegions {
 public static string Inspect(string path) {
  using(var b=new Bitmap(path)) {
   int w=b.Width,h=b.Height,n=w*h; bool[] mask=new bool[n],seen=new bool[n]; int[] q=new int[n]; string result="";
   for(int y=200;y<330;y++) for(int x=80;x<435;x++) {
    var c=b.GetPixel(x,y); mask[y*w+x]=c.A>128 && c.R>150 && (c.G>c.B*1.25+15 || c.R>c.G*1.6+20);
   }
   for(int seed=0;seed<n;seed++) {
    if(!mask[seed]||seen[seed])continue; int head=0,tail=1,l=w,t=h,r=0,bot=0; q[0]=seed; seen[seed]=true;
    while(head<tail) {int p=q[head++],x=p%w,y=p/w;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);
     int[] next={p-1,p+1,p-w,p+w};foreach(int a in next)if(a>=0&&a<n&&mask[a]&&!seen[a]){seen[a]=true;q[tail++]=a;}
    }
    if(tail>20 && r-l<42 && bot-t>10 && bot-t<65) result+=$" ({l},{t},{r},{bot};area={tail})";
   }
   return result;
  }
 }
}
'@
foreach($character in @('FloatingWizard','DarkFloatingWizard','LeafFloatingWizard','AutumnFloatingWizard')) {
 foreach($facing in @('front','left','right')) {
  $assetPath=Join-Path $PSScriptRoot "../../UnityProject/Assets/_Game/Resources/Art/$character/Idle/idle_$facing.png"
  Write-Output "$character/$facing $([EyeRegions]::Inspect([IO.Path]::GetFullPath($assetPath)))"
 }
}
