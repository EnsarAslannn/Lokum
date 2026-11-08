using Lokum.Core;

internal static class TranscriptionChecks
{
    public static async Task Integration(Action<bool,string> check,string root,string whisper,string model,string sample,string ffmpeg,string ffprobe)
    {
        Directory.CreateDirectory(root);var source=Path.Combine(root,"Konuşma 'Türkçe' örneği.mp4");
        await ProcessRunner.RunAsync(ffmpeg,["-y","-v","error","-f","lavfi","-i","color=black:size=640x360:rate=15","-i",sample,"-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",source]);
        var original=System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(source));
        var engine=new TranscriptionEngine(whisper,model,ffmpeg,ffprobe);
        var document=await engine.TranscribeAsync(source,"en");
        check(document.Text.Contains("country",StringComparison.OrdinalIgnoreCase),"Real Whisper must recognize the known speech fixture.");
        var destination=Path.Combine(root,"Saved 'çalışma'");
        var srt=(await TranscriptionEngine.ExportAsync(document,source,"srt",destination,ffmpeg))[0];
        check(SubtitleCodec.Parse(await File.ReadAllTextAsync(srt)).Length>0,"Exported SRT must contain timed recognized speech.");
        var txt=(await TranscriptionEngine.ExportAsync(document,source,"txt",destination,ffmpeg))[0];
        check(await File.ReadAllTextAsync(txt)==document.Text,"Text export must preserve the reviewed transcription.");
        var mp4=(await TranscriptionEngine.ExportAsync(document,source,"mp4",destination,ffmpeg))[0];
        var info=await new MediaEngine(ffmpeg,ffprobe).InspectAsync(mp4);
        check(info.HasAudio&&info.Video?.Width==640,"Burned subtitles must preserve video and audio.");
        var frame=Path.Combine(root,"frame.gray");
        await ProcessRunner.RunAsync(ffmpeg,["-y","-v","error","-ss","1","-i",mp4,"-frames:v","1","-pix_fmt","gray","-f","rawvideo",frame]);
        check((await File.ReadAllBytesAsync(frame)).Count(b=>b>100)>50,"Burned subtitle text must be visible on the black video frame.");
        var after=System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(source));
        check(original.SequenceEqual(after),"Subtitle generation and export must preserve the original media.");
        var again=(await TranscriptionEngine.ExportAsync(document,source,"srt",destination,ffmpeg))[0];
        check(again!=srt&&File.Exists(srt),"Saving again must keep the previous subtitle file.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();var stopped=false;
        try{await engine.TranscribeAsync(source,"en",token:cancelled.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"Cancelled transcription must not start an inference.");
        check(!Directory.EnumerateDirectories(destination,".Lokum-*").Any(),"Subtitle exports must clean staging directories.");
    }
    public static void Documents(Action<bool,string> check)
    {
        var document=SubtitleTranscript.FromWhisperJson("""{"transcription":[{"offsets":{"from":1000,"to":2400},"text":" Merhaba dünya. "},{"offsets":{"from":3000,"to":4500},"text":"İkinci cümle."}]}""",4);
        check(document.Srt=="1\n00:00:01,000 --> 00:00:02,400\nMerhaba dünya.\n\n2\n00:00:03,000 --> 00:00:04,000\nİkinci cümle.\n","Whisper offsets must produce UTF-8 SRT with end clamped to the media duration.");
        check(document.Text=="Merhaba dünya.\n\nİkinci cümle.\n","Plain transcript must omit timestamps.");
        check(document.At(1.5)=="Merhaba dünya."&&document.At(2.8)==""&&document.At(4)=="","Preview must display only the active subtitle.");
        var edited=SubtitleTranscript.FromSrt("1\n00:00:00,250 --> 00:00:01,500\nDüzeltilmiş metin.\n",4);
        check(edited.At(.5)=="Düzeltilmiş metin.","Edited text and timing must reach the preview.");
        foreach(var invalid in new[]{"1\n00:00:03,000 --> 00:00:02,000\nWrong\n","1\n00:00:03,000 --> 00:00:05,000\nOutside\n",""})
        {
            var rejected=false;try{SubtitleTranscript.FromSrt(invalid,4);}catch(ArgumentException){rejected=true;}catch(InvalidDataException){rejected=true;}
            check(rejected,"Invalid edits must be rejected before export.");
        }
        var silent=false;try{SubtitleTranscript.FromWhisperJson("""{"transcription":[]}""",4);}catch(InvalidDataException){silent=true;}
        check(silent,"Silence must not produce an empty successful subtitle export.");
    }
    public static void Catalog(Action<bool,string> check)
    {
        check(FeatureCatalog.WheelTools(["speech.mp4"]).Contains("generateSubtitles"),"Video wheel must offer automatic subtitles.");
        check(FeatureCatalog.WheelTools(["speech.wav"]).Contains("generateSubtitles"),"Audio wheel must offer automatic subtitles.");
        check(!FeatureCatalog.WheelTools(["picture.png"]).Contains("generateSubtitles"),"Images cannot be transcribed.");
        check(!FeatureCatalog.WheelTools(["a.mp4","b.mp4"]).Contains("generateSubtitles"),"The subtitle editor accepts one media file.");
        var directory=Path.Combine(Path.GetTempPath(),"Lokum-Folder-"+Guid.NewGuid().ToString("N")+".mp4");Directory.CreateDirectory(directory);
        try{check(!FeatureCatalog.WheelTools([directory]).Contains("generateSubtitles"),"A folder with a video extension cannot be transcribed.");}finally{Directory.Delete(directory);}
    }
}
