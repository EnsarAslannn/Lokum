using ImageMagick;
using Lokum.Core;
using System.Security.Cryptography;

internal static class BackgroundEditingChecks
{
    internal static void Run(Action<bool,string> check,string root)
    {
        Directory.CreateDirectory(root);
        var input=Path.Combine(root,"Özgün seçim.png");
        using(var image=new MagickImage(new MagickColor(220,60,20,128),8,8))image.Write(input,MagickFormat.Png32);
        var originalHash=SHA256.HashData(File.ReadAllBytes(input));
        ForegroundRegion Rectangle(double x,double y,double right,double bottom,bool subtract=false)=>new([new(x,y),new(right,y),new(right,bottom),new(x,bottom)],subtract);
        var region=Rectangle(.25,.25,.75,.75);
        using var engine=new BackgroundEditingEngine();
        var png=engine.CreateCutout(input,[region]);
        using(var image=new MagickImage(png))using(var pixels=image.GetPixels())
        {
            check(image.Width==8&&image.Height==8&&image.HasAlpha,"Manual selection changed the canvas or lost transparency");
            check(pixels.GetPixel(3,3).GetChannel(3)==128,"Selected pixels did not retain their original alpha");
            check(pixels.GetPixel(0,0).GetChannel(3)==0&&pixels.GetPixel(7,7).GetChannel(3)==0,"Pixels outside the selection were not removed");
            check(pixels.GetPixel(3,3).GetChannel(0)==220&&pixels.GetPixel(3,3).GetChannel(1)==60&&pixels.GetPixel(3,3).GetChannel(2)==20,"Selected RGB colors were changed");
        }
        var union=engine.CreateCutout(input,[Rectangle(0,0,.25,.25),Rectangle(.75,.75,1,1)]);
        using(var image=new MagickImage(union))using(var pixels=image.GetPixels())
            check(pixels.GetPixel(0,0).GetChannel(3)==128&&pixels.GetPixel(7,7).GetChannel(3)==128&&pixels.GetPixel(4,4).GetChannel(3)==0,"Multiple kept regions were not combined");
        var hole=engine.CreateCutout(input,[Rectangle(0,0,1,1),Rectangle(.25,.25,.75,.75,true)]);
        using(var image=new MagickImage(hole))using(var pixels=image.GetPixels())
            check(pixels.GetPixel(0,0).GetChannel(3)==128&&pixels.GetPixel(3,3).GetChannel(3)==0,"Subtracting an inner background area did not cut a hole");
        var triangle=engine.CreateCutout(input,[new([new(0,0),new(1,0),new(0,1)])]);
        using(var image=new MagickImage(triangle))using(var pixels=image.GetPixels())
        {
            check(pixels.GetPixel(1,1).GetChannel(3)==128&&pixels.GetPixel(6,6).GetChannel(3)==0,"Freehand polygon was replaced by its bounding rectangle");
            check(Enumerable.Range(0,8).Any(x=>pixels.GetPixel(x,7-x).GetChannel(3) is >0 and <128),"Selection edges have no antialiasing");
        }
        var transparent=BackgroundEditingEngine.Compose(png,new());
        using(var image=new MagickImage(transparent))using(var pixels=image.GetPixels())check(pixels.GetPixel(0,0).GetChannel(3)==0,"Transparent export was flattened");
        var color=BackgroundEditingEngine.Compose(png,new(BackgroundFill.Color,"#0000FF"));
        using(var image=new MagickImage(color))using(var pixels=image.GetPixels())
        {
            var background=pixels.GetPixel(0,0);var foreground=pixels.GetPixel(3,3);
            check(background.GetChannel(0)==0&&background.GetChannel(1)==0&&background.GetChannel(2)==255&&background.GetChannel(3)==255,"Solid replacement color is wrong");
            check(Math.Abs(foreground.GetChannel(0)-110)<=1&&Math.Abs(foreground.GetChannel(1)-30)<=1&&Math.Abs(foreground.GetChannel(2)-137)<=1&&foreground.GetChannel(3)==255,"Foreground alpha was not composited over the replacement color");
        }
        byte[] backgroundPng;using(var background=new MagickImage(MagickColors.Lime,16,4))backgroundPng=background.ToByteArray(MagickFormat.Png32);
        var replacement=BackgroundEditingEngine.Compose(png,new(BackgroundFill.Image,ImagePng:backgroundPng));
        using(var image=new MagickImage(replacement))using(var pixels=image.GetPixels())
            check(image.Width==8&&image.Height==8&&pixels.GetPixel(0,0).GetChannel(1)==255&&pixels.GetPixel(0,0).GetChannel(3)==255,"Background image did not cover the unchanged output canvas");
        void Reject(Action action,string message)
        {var rejected=false;try{action();}catch(ArgumentException){rejected=true;}check(rejected,message);}
        Reject(()=>engine.CreateCutout(input,[]),"Empty manual selection was exported as a blank image");
        Reject(()=>engine.CreateCutout(input,[new([new(double.NaN,0),new(1,0),new(0,1)])]),"Non-finite selection point was accepted");
        Reject(()=>engine.CreateCutout(input,[Rectangle(-.1,0,1,1)]),"Out-of-bounds selection point was accepted");
        Reject(()=>BackgroundEditingEngine.Compose(png,new(BackgroundFill.Color,"not-a-color")),"Invalid background color was accepted");
        Reject(()=>BackgroundEditingEngine.Compose(png,new(BackgroundFill.Image)),"Missing background image was accepted");
        var exports=Path.Combine(root,"exports");
        var files=BackgroundEditingEngine.Export([new(input,png),new(input,triangle)],exports,new());
        check(files.Count==2&&files.Distinct().Count()==2&&files.All(File.Exists),"Batch cutouts overwrote each other");
        using(var first=new MagickImage(files[0]))using(var second=new MagickImage(files[1]))
            check(first.Compare(second,ErrorMetric.Absolute)>0,"Batch export reused the first file's selection for every image");
        var before=Directory.GetFileSystemEntries(exports).Order().ToArray();
        var failed=false;try{BackgroundEditingEngine.Export([new(input,png),new(input,[1,2,3])],exports,new());}catch(MagickException){failed=true;}
        check(failed&&Directory.GetFileSystemEntries(exports).Order().SequenceEqual(before),"Failed batch leaked partial exports or staging data");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        var cancelled=false;try{BackgroundEditingEngine.Export([new(input,png)],exports,new(),token:cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
        check(cancelled&&Directory.GetFileSystemEntries(exports).Order().SequenceEqual(before),"Cancelled export published files");
        using var midBatch=new CancellationTokenSource();
        cancelled=false;try{BackgroundEditingEngine.Export([new(input,png),new(input,triangle)],exports,new(),new CancelAfterFirst(midBatch),midBatch.Token);}catch(OperationCanceledException){cancelled=true;}
        check(cancelled&&Directory.GetFileSystemEntries(exports).Order().SequenceEqual(before),"Mid-batch cancellation retained partial exports");
        var invisible=Path.Combine(root,"transparent-source.png");using(var image=new MagickImage(MagickColors.Transparent,8,8))image.Write(invisible,MagickFormat.Png32);
        using(var image=new MagickImage(engine.CreateCutout(invisible,[Rectangle(0,0,1,1)])))using(var pixels=image.GetPixels())
            check(pixels.GetPixel(3,3).GetChannel(3)==0,"Selecting an originally transparent pixel made it opaque");
        var fixture=Path.GetFullPath("Tests/LokumCoreTests/Fixtures/astronaut.png");
        using(var image=new MagickImage(engine.CreateCutout(fixture,[],automatic:true)))using(var pixels=image.GetPixels())
            check(pixels.GetPixel(225,120).GetChannel(3)>220&&pixels.GetPixel(480,30).GetChannel(3)<30,"Automatic selection did not retain the subject and remove the background");
        using(var image=new MagickImage(engine.CreateCutout(fixture,[Rectangle(0,0,1,1)],automatic:true)))using(var pixels=image.GetPixels())
            check(pixels.GetPixel(480,30).GetChannel(3)==255,"Adding a region could not restore pixels removed by automatic selection");
        using(var image=new MagickImage(engine.CreateCutout(fixture,[Rectangle(.3,.1,.6,.4,true)],automatic:true)))using(var pixels=image.GetPixels())
            check(pixels.GetPixel(225,120).GetChannel(3)==0,"Subtracting a region did not refine the automatic foreground mask");
        check(SHA256.HashData(File.ReadAllBytes(input)).SequenceEqual(originalHash),"Editing or exporting modified the source image");
    }
    private sealed class CancelAfterFirst(CancellationTokenSource cancellation):IProgress<OperationProgress>
    { public void Report(OperationProgress progress)=>cancellation.Cancel(); }
}
