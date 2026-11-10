using System.Globalization;
using System.Text;

namespace Lokum.Core;

public sealed class TranscriptionEngine(string whisper,string model,string ffmpeg,string ffprobe)
{
    public static IReadOnlyDictionary<string,string> Languages{get;}=new Dictionary<string,string>{{"auto","Detect automatically"},{"tr","Turkish"},{"en","English"},{"de","German"},{"fr","French"},{"es","Spanish"},{"it","Italian"},{"ar","Arabic"},{"ru","Russian"},{"ja","Japanese"},{"ko","Korean"},{"zh","Chinese"}};
    public async Task<SubtitleTranscript> TranscribeAsync(string input,string language,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        if(!Languages.ContainsKey(language))throw new ArgumentException("Choose a supported spoken language.");
        var info=await new MediaEngine(ffmpeg,ffprobe).InspectAsync(input,token);
        if(!info.HasAudio)throw new ArgumentException("This media has no audio track to transcribe.");
        if(!File.Exists(whisper)||!File.Exists(model))throw new FileNotFoundException("Set up the speech model first.");
        var directory=Path.Combine(Path.GetTempPath(),"Lokum-Subtitles-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var audio=Path.Combine(directory,"speech.wav");var prefix=Path.Combine(directory,"transcript");
            progress?.Report(new(null,"Preparing audio…"));
            await ProcessRunner.RunAsync(ffmpeg,["-nostdin","-v","error","-i",Path.GetFullPath(input),"-map","0:a:0","-vn","-ar","16000","-ac","1","-c:a","pcm_s16le",audio],token);
            progress?.Report(new(null,"Transcribing speech on your computer…"));
            await ProcessRunner.RunAsync(whisper,["-m",Path.GetFullPath(model),"-f",audio,"-l",language,"-oj","-of",prefix,"-ml","42","-sow","-t",Math.Clamp(Environment.ProcessorCount/2,1,8).ToString(CultureInfo.InvariantCulture),"-ng","-np"],token);
            token.ThrowIfCancellationRequested();
            var result=SubtitleTranscript.FromWhisperJson(await File.ReadAllTextAsync(prefix+".json",Encoding.UTF8,token),info.Duration);
            progress?.Report(new(1,"Subtitles are ready. Review the text and timing."));return result;
        }
        finally{Directory.Delete(directory,true);}
    }
    public static async Task<IReadOnlyList<string>> ExportAsync(SubtitleTranscript transcript,string input,string format,string destination,string ffmpeg,CancellationToken token=default)
    {
        if(format is not ("srt" or "txt" or "vtt" or "mp4"))throw new ArgumentException("Choose SRT, TXT, VTT or MP4.");
        if(format=="mp4"&&!FeatureCatalog.IsVideo(input))throw new ArgumentException("Choose a video to embed subtitles.");
        using var tx=new ExportTransaction(destination);
        var output=Path.Combine(tx.StagingDirectory,Path.GetFileNameWithoutExtension(input)+"-subtitles."+format);
        if(format!="mp4")await File.WriteAllTextAsync(output,format=="srt"?transcript.Srt:format=="txt"?transcript.Text:SubtitleCodec.Convert(transcript.Srt,"vtt"),new UTF8Encoding(false),token);
        else
        {
            var subtitle=Path.Combine(tx.StagingDirectory,"captions.srt");await File.WriteAllTextAsync(subtitle,transcript.Srt,new UTF8Encoding(false),token);
            await ProcessRunner.RunAsync(ffmpeg,["-nostdin","-v","error","-i",Path.GetFullPath(input),"-map","0:v:0","-map","0:a?","-vf","subtitles=filename=captions.srt","-c:v","libx264","-preset","medium","-crf","20","-c:a","aac","-movflags","+faststart",Path.GetFullPath(output)],token,workingDirectory:tx.StagingDirectory);
        }
        token.ThrowIfCancellationRequested();return tx.Publish([output],token);
    }
}
