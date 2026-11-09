using System.Text.Json;

namespace Lokum.Core;

public sealed class SubtitleTranslationEngine(string executable,string model)
{
    public async Task<SubtitleTranscript> TranslateAsync(SubtitleTranscript source,double duration,string sourceLanguage,string targetLanguage,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();SubtitleWorkflow.ValidateLanguage(sourceLanguage);SubtitleWorkflow.ValidateTargetLanguage(targetLanguage);
        if(sourceLanguage==targetLanguage)return source;
        var directory=Path.Combine(Path.GetTempPath(),"Lokum-Translation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var input=Path.Combine(directory,"request.json");var output=Path.Combine(directory,"response.json");
            await File.WriteAllTextAsync(input,JsonSerializer.Serialize(new{sourceLanguage,targetLanguage,cues=source.Cues.Select(c=>new{start=c.Start,end=c.End,text=c.Text})}),token);
            progress?.Report(new(null,"Translating the reviewed transcript on your computer…"));
            await ProcessRunner.RunAsync(executable,["--model",model,"--request",input,"--response",output],token,line=>
            {
                try{using var value=JsonDocument.Parse(line);if(value.RootElement.TryGetProperty("fraction",out var fraction))progress?.Report(new(fraction.GetDouble(),"Translating the reviewed transcript on your computer…"));}catch(JsonException){}
            });
            token.ThrowIfCancellationRequested();using var result=JsonDocument.Parse(await File.ReadAllTextAsync(output,token));
            var root=result.RootElement;if(root.GetProperty("sourceLanguage").GetString()!=sourceLanguage||root.GetProperty("targetLanguage").GetString()!=targetLanguage)throw new InvalidDataException("The translation language does not match your selection.");
            var cues=root.GetProperty("cues").EnumerateArray().Select(c=>new SubtitleCodec.Cue(c.GetProperty("start").GetInt64(),c.GetProperty("end").GetInt64(),c.GetProperty("text").GetString()??"")).ToArray();
            return SubtitleTranscript.FromCues(cues,duration);
        }
        finally{Directory.Delete(directory,true);}
    }
}
