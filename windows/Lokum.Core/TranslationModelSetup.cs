using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lokum.Core;

public sealed class TranslationModelSetup(string directory)
{
    private sealed record Asset(string Url,long Size,string Sha256,string? Path,Dictionary<string,string>? Entries);
    private sealed record Model(string Id,string[] Required,Asset[] Assets);
    private static readonly Model[] Models=LoadModels();
    private static Model[] LoadModels()
    {
        using var stream=typeof(TranslationModelSetup).Assembly.GetManifestResourceStream("Lokum.SubtitleTranslationModels.json")!;
        return JsonSerializer.Deserialize<Model[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
    }
    public string DirectoryPath=>Path.GetFullPath(directory);
    public string Executable=>Path.Combine(AppContext.BaseDirectory,"Tools","Translate","translate.exe");
    public static IReadOnlyList<string> RequiredPairs(string source,string target)
    {
        SubtitleWorkflow.ValidateLanguage(source);SubtitleWorkflow.ValidateTargetLanguage(target);
        if(source==target)return [];
        var pairs=new List<string>();if(source!="en")pairs.Add(source+"-en");if(target=="tr")pairs.Add("en-tr");return pairs;
    }
    private static bool Complete(string path,Model model)=>model.Required.All(n=>ToolInstallation.UsableFile(System.IO.Path.Combine(path,n)));
    public bool ReadyFor(string source,string target)=>RequiredPairs(source,target).All(id=>Complete(System.IO.Path.Combine(DirectoryPath,id),Models.Single(m=>m.Id==id)));
    public async Task InstallAsync(string source,string target,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();var pairs=RequiredPairs(source,target);if(ReadyFor(source,target))return;
        if(!File.Exists(Executable))throw new FileNotFoundException("The translation engine is missing. Extract the complete Lokum package again.");
        Directory.CreateDirectory(DirectoryPath);
        foreach(var id in pairs)
        {
            var model=Models.Single(m=>m.Id==id);var destination=System.IO.Path.Combine(DirectoryPath,id);if(Complete(destination,model))continue;
            var stage=System.IO.Path.Combine(DirectoryPath,".translation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
            try
            {
                var content=System.IO.Path.Combine(stage,"content");Directory.CreateDirectory(content);
                foreach(var asset in model.Assets)
                {
                    var download=System.IO.Path.Combine(stage,"download");
                    await VerifiedDownload.FetchAsync(new(asset.Url),download,asset.Size,p=>progress?.Report(new(p,"Downloading the language-specific translation model…")),token);
                    await using(var stream=File.OpenRead(download))
                        if(stream.Length!=asset.Size||Convert.ToHexString(await SHA256.HashDataAsync(stream,token)).ToLowerInvariant()!=asset.Sha256)throw new InvalidDataException("Upstream download checksum mismatch. Nothing was installed.");
                    if(asset.Entries is {} entries)
                    {
                        using(var zip=ZipFile.OpenRead(download))
                        {
                            foreach(var (entryName,relative) in entries)
                            {
                                token.ThrowIfCancellationRequested();var entry=zip.Entries.Single(e=>e.FullName==entryName&&e.Length>0&&e.Length<800L*1024*1024);
                                var output=System.IO.Path.Combine(content,relative);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);entry.ExtractToFile(output);
                            }
                        }
                        File.Delete(download);
                    }
                    else
                    {
                        var output=System.IO.Path.Combine(content,asset.Path!);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);File.Move(download,output);
                    }
                }
                token.ThrowIfCancellationRequested();ToolInstallation.Publish(content,destination,d=>Complete(d,model));
            }
            finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
        }
    }
}
