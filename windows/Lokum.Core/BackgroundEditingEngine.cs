using ImageMagick;
using System.Text.RegularExpressions;

namespace Lokum.Core;

public readonly record struct ForegroundPoint(double X,double Y);
public sealed record ForegroundRegion(ForegroundPoint[] Points,bool Subtract=false);
public enum BackgroundFill { Transparent,Color,Image }
public sealed record BackgroundFillOptions(BackgroundFill Fill=BackgroundFill.Transparent,string Color="#FFFFFF",byte[]? ImagePng=null);
public sealed record PreparedCutout(string Input,byte[] Png);

public sealed class BackgroundEditingEngine:IDisposable
{
    private BackgroundRemovalEngine? automaticEngine;
    private bool disposed;
    public byte[] CreateCutout(string input,IReadOnlyList<ForegroundRegion> regions,bool automatic=false,CancellationToken token=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);token.ThrowIfCancellationRequested();
        if(!automatic&&!regions.Any(r=>!r.Subtract))throw new ArgumentException("Select an area to keep or use automatic removal.");
        if(regions.Count>256)throw new ArgumentException("Use at most 256 selection areas.");
        foreach(var region in regions)
            if(region.Points.Length is <3 or >4096||region.Points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||p.X<0||p.X>1||p.Y<0||p.Y>1))
                throw new ArgumentException("Keep selection points inside the image.");
        using var image=PhotoCodec.Read(input);image.Alpha(AlphaAction.On);
        var width=checked((int)image.Width);var height=checked((int)image.Height);
        byte[] rgba;using(var pixels=image.GetPixels())rgba=pixels.ToByteArray(PixelMapping.RGBA)!;
        var sourceAlpha=new byte[width*height];for(var i=0;i<sourceAlpha.Length;i++){sourceAlpha[i]=rgba[i*4+3];rgba[i*4+3]=0;}
        if(automatic)
        {
            automaticEngine??=new();var png=automaticEngine.Process(input,token);
            using var inferred=Decode(png);using var pixels=inferred.GetPixels();var alpha=pixels.ToByteArray(PixelMapping.RGBA)!;
            if(inferred.Width!=image.Width||inferred.Height!=image.Height)throw new InvalidDataException("The automatic mask dimensions changed.");
            for(var i=0;i<sourceAlpha.Length;i++)rgba[i*4+3]=alpha[i*4+3];
        }
        foreach(var region in regions)ApplyRegion(rgba,sourceAlpha,width,height,region,token);
        token.ThrowIfCancellationRequested();using var output=new MagickImage();
        output.ReadPixels(rgba,new PixelReadSettings((uint)width,(uint)height,StorageType.Char,PixelMapping.RGBA));
        output.ColorSpace=ColorSpace.sRGB;output.Strip();return output.ToByteArray(MagickFormat.Png32);
    }

    // Scan-convert the polygon with four vertical samples and fractional horizontal coverage.
    // Coordinates are normalized, so fitting/zooming the editor cannot move the saved selection.
    private static void ApplyRegion(byte[] rgba,byte[] originalAlpha,int width,int height,ForegroundRegion region,CancellationToken token)
    {
        var points=region.Points;var first=Math.Max(0,(int)Math.Floor(points.Min(p=>p.Y)*height));
        var last=Math.Min(height,(int)Math.Ceiling(points.Max(p=>p.Y)*height));
        var coverage=new double[width];var crossings=new List<double>(points.Length);
        for(var row=first;row<last;row++)
        {
            token.ThrowIfCancellationRequested();Array.Clear(coverage);
            for(var sample=0;sample<4;sample++)
            {
                var y=(row+(sample+.5)/4)/height;crossings.Clear();
                for(var i=0;i<points.Length;i++)
                {
                    var a=points[i];var b=points[(i+1)%points.Length];
                    if((a.Y>y)==(b.Y>y))continue;
                    crossings.Add((a.X+(y-a.Y)*(b.X-a.X)/(b.Y-a.Y))*width);
                }
                crossings.Sort();
                for(var i=0;i+1<crossings.Count;i+=2)
                {
                    var left=Math.Clamp(crossings[i],0,width);var right=Math.Clamp(crossings[i+1],0,width);
                    for(var x=(int)Math.Floor(left);x<Math.Min(width,(int)Math.Ceiling(right));x++)
                        coverage[x]+=Math.Max(0,Math.Min(x+1,right)-Math.Max(x,left))/4;
                }
            }
            for(var x=0;x<width;x++)
            {
                var amount=Math.Clamp(coverage[x],0,1);var index=row*width+x;var offset=index*4+3;
                rgba[offset]=region.Subtract?(byte)Math.Round(rgba[offset]*(1-amount)):(byte)Math.Max(rgba[offset],Math.Round(originalAlpha[index]*amount));
            }
        }
    }

    public static byte[] Compose(byte[] foreground,BackgroundFillOptions options,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();using var cutout=Decode(foreground);
        if(options.Fill==BackgroundFill.Transparent)return (byte[])foreground.Clone();
        if(options.Fill==BackgroundFill.Color&&!Regex.IsMatch(options.Color,"^#[0-9a-fA-F]{6}$"))throw new ArgumentException("Choose a color in #RRGGBB format.");
        if(options.Fill==BackgroundFill.Image&&options.ImagePng is null)throw new ArgumentException("Choose a background image.");
        if(options.Fill is not (BackgroundFill.Color or BackgroundFill.Image))throw new ArgumentException("Choose a supported background type.");
        using var background=options.Fill==BackgroundFill.Color?new MagickImage(new MagickColor(options.Color),cutout.Width,cutout.Height):Decode(options.ImagePng!);
        if(options.Fill==BackgroundFill.Image)
        {
            background.FilterType=FilterType.Lanczos;
            background.Resize(new MagickGeometry(cutout.Width,cutout.Height){FillArea=true});
            background.Crop(cutout.Width,cutout.Height,Gravity.Center);background.ResetPage();
        }
        token.ThrowIfCancellationRequested();background.Composite(cutout,CompositeOperator.Over);background.Strip();
        return background.ToByteArray(MagickFormat.Png32);
    }

    public static IReadOnlyList<string> Export(IReadOnlyList<PreparedCutout> inputs,string destination,BackgroundFillOptions options,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();if(inputs.Count==0)throw new ArgumentException("Choose one or more images.");
        using var transaction=new ExportTransaction(destination);var outputs=new List<string>();
        foreach(var input in inputs)
        {
            token.ThrowIfCancellationRequested();var png=Compose(input.Png,options,token);
            var suffix=options.Fill==BackgroundFill.Transparent?" Cutout.png":" Background.png";
            var path=SafePaths.Unique(transaction.StagingDirectory,Path.GetFileNameWithoutExtension(input.Input)+suffix);
            File.WriteAllBytes(path,png);outputs.Add(path);progress?.Report(new(outputs.Count/(double)inputs.Count*.98,"Saving background edits…"));
        }
        return transaction.Publish(outputs,token);
    }

    private static MagickImage Decode(byte[] png)
    {
        using var stream=new MemoryStream(png,false);var settings=new MagickReadSettings{Format=MagickFormat.Png};
        var info=new MagickImageInfo(stream,settings);
        if((ulong)info.Width*info.Height>100_000_000||info.Width>30000||info.Height>30000)throw new ArgumentException("This image exceeds 100 MP or 30,000 pixels.");
        stream.Position=0;return new MagickImage(stream,settings);
    }
    public void Dispose(){if(disposed)return;disposed=true;automaticEngine?.Dispose();automaticEngine=null;}
}
