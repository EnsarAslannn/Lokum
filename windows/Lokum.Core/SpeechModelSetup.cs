using System.Security.Cryptography;

namespace Lokum.Core;

public sealed class SpeechModelSetup(string directory)
{
    public string DirectoryPath=>Path.GetFullPath(directory);
    public string Executable=>Path.Combine(AppContext.BaseDirectory,"Tools","Speech","whisper-cli.exe");
    public string Model=>Path.Combine(DirectoryPath,"ggml-base.bin");
    public bool Ready=>ToolInstallation.UsableFile(Executable)&&File.Exists(Model)&&new FileInfo(Model).Length==147951465;
    public async Task InstallAsync(IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();if(Ready)return;
        if(!File.Exists(Executable))throw new FileNotFoundException("The speech engine is missing. Extract the complete Lokum package again.");
        var parent=Path.GetDirectoryName(DirectoryPath)!;Directory.CreateDirectory(parent);var stage=Path.Combine(parent,".speech-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try
        {
            await VerifiedDownload.FetchAsync(new("https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base.bin"),Path.Combine(stage,"ggml-base.bin"),160*1024*1024,p=>progress?.Report(new(p,"Downloading the speech model (148 MB)…")),token);
            await Verify(Path.Combine(stage,"ggml-base.bin"),"60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe",token);
            token.ThrowIfCancellationRequested();ToolInstallation.Publish(stage,DirectoryPath,d=>File.Exists(Path.Combine(d,"ggml-base.bin"))&&new FileInfo(Path.Combine(d,"ggml-base.bin")).Length==147951465);
        }
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
    private static async Task Verify(string file,string expected,CancellationToken token)
    {
        await using var stream=File.OpenRead(file);var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token)).ToLowerInvariant();
        if(hash!=expected)throw new InvalidDataException("Upstream download checksum mismatch. Nothing was installed.");
    }
}
