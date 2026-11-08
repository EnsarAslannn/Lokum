using System.IO.Compression;
using System.Security.Cryptography;

namespace Lokum.Core;

public sealed class TranslationModelSetup(string directory)
{
    public string DirectoryPath=>Path.GetFullPath(directory);
    public string Executable=>Path.Combine(AppContext.BaseDirectory,"Tools","Translate","translate.exe");
    public bool Ready=>new[]{"model/model.bin","model/shared_vocabulary.txt","sentencepiece.model"}.All(n=>ToolInstallation.UsableFile(Path.Combine(DirectoryPath,n)));
    public async Task InstallAsync(IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();if(Ready)return;
        if(!File.Exists(Executable))throw new FileNotFoundException("The translation engine is missing. Extract the complete Lokum package again.");
        var parent=Path.GetDirectoryName(DirectoryPath)!;Directory.CreateDirectory(parent);var stage=Path.Combine(parent,".translation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try
        {
            var archive=Path.Combine(stage,"model.zip");
            await VerifiedDownload.FetchAsync(new("https://data.argosopentech.com/argospm/v2/translate-fairseq_m2m_100_418M.argosmodel"),archive,500L*1024*1024,p=>progress?.Report(new(p,"Downloading the translation model (459 MB)…")),token);
            await using(var stream=File.OpenRead(archive))
                if(Convert.ToHexString(await SHA256.HashDataAsync(stream,token)).ToLowerInvariant()!="8a63b8e9228f985d081dd166e336acde5f9083f1f4d7b39e14f7f2d57e5405b4")throw new InvalidDataException("Upstream download checksum mismatch. Nothing was installed.");
            var content=Path.Combine(stage,"content");Directory.CreateDirectory(content);
            using(var zip=ZipFile.OpenRead(archive))
            foreach(var relative in new[]{"model/model.bin","model/shared_vocabulary.txt","sentencepiece.model"})
            {
                token.ThrowIfCancellationRequested();var entry=zip.Entries.Single(e=>e.FullName=="translate-fairseq_m2m_100_418M/"+relative&&e.Length>0&&e.Length<800L*1024*1024);
                var output=Path.Combine(content,relative);Directory.CreateDirectory(Path.GetDirectoryName(output)!);entry.ExtractToFile(output);
            }
            token.ThrowIfCancellationRequested();ToolInstallation.Publish(content,DirectoryPath,d=>File.Exists(Path.Combine(d,"model/model.bin"))&&File.Exists(Path.Combine(d,"sentencepiece.model")));
        }
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
}
