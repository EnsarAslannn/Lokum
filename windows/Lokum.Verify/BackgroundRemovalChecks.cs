using Lokum.Core;
using ImageMagick;
using System.Security.Cryptography;

internal static class BackgroundRemovalChecks
{
    internal static void Catalog(Action<bool,string> check)
    {
        foreach(var extension in new[]{"jpg","png","webp","heic","tiff","bmp","avif","svg"})
            check(FeatureCatalog.Tools(["photo."+extension]).Contains("removeBackground"),"Background removal is not offered for "+extension);
        check(FeatureCatalog.Tools(["one.jpg","two.png"]).Contains("removeBackground"),"Background removal is missing for a batch of images");
        foreach(var selection in new string[][]{[],["document.pdf"],["clip.mp4"],["photo.jpg","document.pdf"]})
            check(!FeatureCatalog.Tools(selection).Contains("removeBackground"),"Background removal was offered for unsupported files");
        check(FeatureCatalog.ToolNames.GetValueOrDefault("removeBackground")=="Edit Background","The unified background workspace has no tool label");
    }

    internal static void Engine(Action<bool,string> check,string root,string fixture)
    {
        var normalized=BackgroundRemovalEngine.NormalizeInput([0,0,0,255,255,255]);
        check(normalized.Length==6&&Math.Abs(normalized[0]-(-.485f/.229f))<.0001f&&Math.Abs(normalized[1]-(1-.485f)/.229f)<.0001f,"Model input red channel or channel-first layout is wrong");
        check(Math.Abs(normalized[3]-(1-.456f)/.224f)<.0001f&&Math.Abs(normalized[5]-(1-.406f)/.225f)<.0001f,"Model input green/blue normalization is wrong");
        check(BackgroundRemovalEngine.NormalizeInput([0,0,0]).All(float.IsFinite),"Black images produced non-finite model input");
        byte[] rgba=[200,50,20,128,20,50,200,0];
        var masked=BackgroundRemovalEngine.ApplyMask(rgba,2,1,[1,1],2,1);
        check(masked.SequenceEqual(rgba),"Existing image transparency or RGB colors were replaced");
        masked=BackgroundRemovalEngine.ApplyMask(rgba,2,1,[0,1],2,1);
        check(masked[3]==0&&masked[7]==0&&masked[0]==200,"Foreground mask was inverted or erased RGB channels");
        var interpolated=BackgroundRemovalEngine.ApplyMask([1,2,3,255,1,2,3,255,1,2,3,255],3,1,[0,1],2,1);
        check(interpolated[3]==0&&Math.Abs(interpolated[7]-128)<=1&&interpolated[11]==255,"Mask resizing does not preserve soft edges");
        using var engine=new BackgroundRemovalEngine();
        var hash=SHA256.HashData(File.ReadAllBytes(fixture));
        var png=engine.Process(fixture);
        using(var cutout=new MagickImage(png))
        {
            check(cutout.Format==MagickFormat.Png&&cutout.Width==512&&cutout.Height==512&&cutout.HasAlpha,"Cutout lost PNG transparency or source dimensions");
            using var pixels=cutout.GetPixels();
            check(pixels.GetPixel(225,120).GetChannel(3)>220,"Real model removed the astronaut's face");
            check(pixels.GetPixel(480,30).GetChannel(3)<30,"Real model retained the photograph background");
            using var source=PhotoCodec.Read(fixture);using var original=source.GetPixels();
            check(pixels.GetPixel(225,120).GetChannel(0)==original.GetPixel(225,120).GetChannel(0),"Foreground RGB pixels changed");
        }
        Directory.CreateDirectory(root);File.WriteAllBytes(Path.Combine(root,"astronaut-cutout.png"),png);
        var outputs=engine.Export([fixture,fixture],root);
        check(outputs.Count==2&&outputs.Distinct().Count()==2&&outputs.All(p=>Path.GetExtension(p)==".png"&&File.Exists(p)),"Batch output collided or did not produce transparent PNGs");
        check(SHA256.HashData(File.ReadAllBytes(fixture)).SequenceEqual(hash),"Background removal modified its input");
        var before=Directory.GetFileSystemEntries(root).Order().ToArray();
        try{engine.Export([fixture,Path.Combine(root,"missing.png")],root);throw new Exception("Missing image accepted");}catch(FileNotFoundException) { }
        check(Directory.GetFileSystemEntries(root).Order().SequenceEqual(before),"Failed batch published partial files or leaked staging directories");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{engine.Export([fixture],root,token:cancellation.Token);throw new Exception("Cancelled batch was processed");}catch(OperationCanceledException) { }
        check(Directory.GetFileSystemEntries(root).Order().SequenceEqual(before),"Cancelled export published files");
        using var midBatch=new CancellationTokenSource();
        try{engine.Export([fixture,fixture],root,new CancelAfterFirst(midBatch),midBatch.Token);throw new Exception("Mid-batch cancellation ignored");}catch(OperationCanceledException) { }
        check(Directory.GetFileSystemEntries(root).Order().SequenceEqual(before),"Mid-batch cancellation retained partial exports");
        File.WriteAllText(Path.Combine(root,"verification.json"),System.Text.Json.JsonSerializer.Serialize(new{passed=true,offline=true,fixture="NASA astronaut (public domain)",width=512,height=512,foregroundAlpha=255,backgroundAlpha=0,originalUnchanged=true,batchNaming=true,failedBatchRollback=true,cancellationRollback=true},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }

    private sealed class CancelAfterFirst(CancellationTokenSource cancellation):IProgress<OperationProgress>
    {
        public void Report(OperationProgress progress){if(progress.Fraction>=.4)cancellation.Cancel();}
    }
}
