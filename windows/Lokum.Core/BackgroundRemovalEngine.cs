using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using System.Security.Cryptography;

namespace Lokum.Core;

/// Offline U²-Net inference. The model is embedded, pinned and never fetched at runtime.
/// One instance belongs to one preview/export; callers run CPU work off the UI thread.
public sealed class BackgroundRemovalEngine : IDisposable
{
    private const int Size=320;
    private InferenceSession? session;
    private bool disposed;

    public byte[] Process(string input,CancellationToken token=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);token.ThrowIfCancellationRequested();
        using var image=PhotoCodec.Read(input);
        image.Alpha(AlphaAction.On);
        var width=checked((int)image.Width);var height=checked((int)image.Height);
        using var pixels=image.GetPixels();
        var rgba=pixels.ToByteArray(PixelMapping.RGBA)!;
        // Hidden RGB in transparent pixels must not affect foreground detection.
        using var small=(MagickImage)image.Clone();
        small.BackgroundColor=MagickColors.White;small.Alpha(AlphaAction.Remove);
        small.FilterType=FilterType.Lanczos;small.Resize(new MagickGeometry(Size,Size){IgnoreAspectRatio=true});
        using var smallPixels=small.GetPixels();
        var normalized=NormalizeInput(smallPixels.ToByteArray(PixelMapping.RGB)!);
        token.ThrowIfCancellationRequested();session??=CreateSession();token.ThrowIfCancellationRequested();
        using var value=OrtValue.CreateTensorValueFromMemory(normalized,[1,3,Size,Size]);
        using var options=new RunOptions();
        using var cancelled=token.Register(()=>options.Terminate=true);
        float[] mask;
        try
        {
            using var results=session.Run(options,new Dictionary<string,OrtValue>{{session.InputNames[0],value}},[session.OutputNames[0]]);
            mask=results[0].GetTensorDataAsSpan<float>().ToArray();
        }
        catch(OnnxRuntimeException) when(token.IsCancellationRequested){throw new OperationCanceledException(token);}
        token.ThrowIfCancellationRequested();
        if(mask.Length!=Size*Size||mask.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("The background removal model returned an invalid mask.");
        var minimum=mask.Min();var range=mask.Max()-minimum;
        for(var n=0;n<mask.Length;n++)mask[n]=Math.Clamp(range>1e-6f?(mask[n]-minimum)/range:mask[n],0,1);
        var cutout=ApplyMask(rgba,width,height,mask,Size,Size,token);
        using var output=new MagickImage();
        output.ReadPixels(cutout,new PixelReadSettings((uint)width,(uint)height,StorageType.Char,PixelMapping.RGBA));
        output.ColorSpace=ColorSpace.sRGB;output.Strip();
        token.ThrowIfCancellationRequested();return output.ToByteArray(MagickFormat.Png32);
    }

    public IReadOnlyList<string> Export(IReadOnlyList<string> inputs,string destination,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);token.ThrowIfCancellationRequested();
        if(inputs.Count==0||inputs.Any(p=>!FeatureCatalog.IsImage(p)))throw new ArgumentException("Choose one or more images to remove their backgrounds.");
        using var transaction=new ExportTransaction(destination);var outputs=new List<string>();
        for(var n=0;n<inputs.Count;n++)
        {
            token.ThrowIfCancellationRequested();progress?.Report(new(n/(double)inputs.Count,"Removing background…"));
            var png=Process(inputs[n],token);
            var output=SafePaths.Unique(transaction.StagingDirectory,Path.GetFileNameWithoutExtension(inputs[n])+" Cutout.png");
            File.WriteAllBytes(output,png);outputs.Add(output);
            progress?.Report(new((n+1)/(double)inputs.Count*.98,"Preparing transparent PNG…"));
        }
        return transaction.Publish(outputs,token);
    }

    internal static float[] NormalizeInput(byte[] rgb)
    {
        if(rgb.Length==0||rgb.Length%3!=0)throw new ArgumentException("RGB pixels are required.");
        var count=rgb.Length/3;var result=new float[rgb.Length];var maximum=Math.Max(1,(int)rgb.Max());
        float[] mean=[.485f,.456f,.406f],deviation=[.229f,.224f,.225f];
        for(var n=0;n<count;n++)for(var c=0;c<3;c++)result[c*count+n]=(rgb[n*3+c]/(float)maximum-mean[c])/deviation[c];
        return result;
    }

    internal static byte[] ApplyMask(byte[] rgba,int width,int height,float[] mask,int maskWidth,int maskHeight,CancellationToken token=default)
    {
        if(width<1||height<1||maskWidth<1||maskHeight<1||rgba.LongLength!=(long)width*height*4||mask.LongLength!=(long)maskWidth*maskHeight)
            throw new ArgumentException("Image and mask dimensions must match their pixels.");
        var output=(byte[])rgba.Clone();
        for(var y=0;y<height;y++)
        {
            token.ThrowIfCancellationRequested();
            var sy=Math.Clamp((y+.5)*maskHeight/height-.5,0,maskHeight-1);var y0=(int)sy;var y1=Math.Min(y0+1,maskHeight-1);var fy=sy-y0;
            for(var x=0;x<width;x++)
            {
                var sx=Math.Clamp((x+.5)*maskWidth/width-.5,0,maskWidth-1);var x0=(int)sx;var x1=Math.Min(x0+1,maskWidth-1);var fx=sx-x0;
                var top=mask[y0*maskWidth+x0]*(1-fx)+mask[y0*maskWidth+x1]*fx;
                var bottom=mask[y1*maskWidth+x0]*(1-fx)+mask[y1*maskWidth+x1]*fx;
                var alpha=Math.Clamp(top*(1-fy)+bottom*fy,0,1);var offset=(y*width+x)*4+3;
                output[offset]=(byte)Math.Round(rgba[offset]*alpha);
            }
        }
        return output;
    }

    private static InferenceSession CreateSession()
    {
        using var resource=typeof(BackgroundRemovalEngine).Assembly.GetManifestResourceStream("Lokum.u2netp.onnx")??throw new InvalidDataException("The background removal model is missing. Reinstall Lokum.");
        using var bytes=new MemoryStream();resource.CopyTo(bytes);var model=bytes.ToArray();
        if(Convert.ToHexString(SHA256.HashData(model))!="309C8469258DDA742793DCE0EBEA8E6DD393174F89934733ECC8B14C76F4DDD8")throw new InvalidDataException("The background removal model checksum does not match. Reinstall Lokum.");
        using var options=new SessionOptions{IntraOpNumThreads=Math.Clamp(Environment.ProcessorCount/2,1,4),InterOpNumThreads=1,GraphOptimizationLevel=GraphOptimizationLevel.ORT_ENABLE_ALL};
        return new InferenceSession(model,options);
    }

    public void Dispose(){if(disposed)return;disposed=true;session?.Dispose();session=null;}
}
