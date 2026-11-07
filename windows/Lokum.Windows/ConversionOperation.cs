using Lokum.Core;
namespace Lokum.Windows;

internal sealed record ConversionRequest(string[] Inputs,string Format,ImageOptions Image,MediaOptions Media,DocumentOptions Document,bool PreserveMetadata);

internal sealed class ConversionOperation(Toolchain tools,OfficeRuntime office,MetadataService metadata)
{
    internal async Task<IReadOnlyList<string>> RunAsync(ConversionRequest request,string destination,IProgress<OperationProgress>? progress,CancellationToken token)
    {
        if(request.Inputs.Length==0||!FeatureCatalog.Formats(request.Inputs).Contains(request.Format))throw new ArgumentException("Choose a supported output format.");
        if(request.Inputs.All(FeatureCatalog.IsImage))
        {
            using var tx=new ExportTransaction(destination);
            var images=await MainWindow.OnStaAsync(()=>ImageEngine.Apply("convert",request.Inputs,tx.StagingDirectory,request.Image with{EncoderPath=tools.Ready?tools.Executable("ffmpeg"):null},request.Format,token,progress));
            if(request.PreserveMetadata&&request.Format is not ("pdf" or "bmp" or "gif"))
            {
                await metadata.InstallAsync(progress,token);
                for(var n=0;n<images.Count;n++)await metadata.CopyImageTagsAsync(request.Inputs[n],images[n],token);
            }
            return tx.Publish(images,token);
        }
        if(request.Inputs.All(p=>FeatureCatalog.IsVideo(p)||FeatureCatalog.IsAudio(p)))
            return await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).ConvertAsync(request.Inputs,request.Format,destination,request.Media,progress,token);
        var documents=new DocumentConversion(office,tools);
        if(request.Inputs.All(FeatureCatalog.IsArchive))return await Task.Run(()=>documents.ConvertAsync(request.Inputs,request.Format,destination,request.Document,progress,token),token);
        return await documents.ConvertAsync(request.Inputs,request.Format,destination,request.Document,progress,token);
    }
}
