using System.Globalization;
using System.Text.Json;

namespace Lokum.Core;

public sealed class SubtitleTranscript
{
    public IReadOnlyList<SubtitleCodec.Cue> Cues{get;}
    private SubtitleTranscript(IEnumerable<SubtitleCodec.Cue> cues,double duration)
    {
        if(!double.IsFinite(duration)||duration<=0)throw new ArgumentException("Choose media with a valid duration.");
        var values=cues.ToArray();long previous=-1;
        foreach(var cue in values)
        {
            if(cue.Start<0||cue.End<=cue.Start||cue.End>duration*1000+.5||cue.Start<previous||string.IsNullOrWhiteSpace(cue.Text))
                throw new InvalidDataException("Each subtitle needs text and a valid start and end within the media duration.");
            previous=cue.Start;
        }
        if(values.Length==0)throw new InvalidDataException("No speech was detected. Try choosing the spoken language.");
        Cues=Array.AsReadOnly(values);
    }
    private static string Stamp(long n)=>string.Format(CultureInfo.InvariantCulture,"{0:00}:{1:00}:{2:00},{3:000}",n/3600000,n/60000%60,n/1000%60,n%1000);
    public string Srt=>string.Join("\n\n",Cues.Select((c,i)=>$"{i+1}\n{Stamp(c.Start)} --> {Stamp(c.End)}\n{c.Text}"))+"\n";
    public string Text=>string.Join("\n\n",Cues.Select(c=>c.Text))+"\n";
    public string At(double seconds)=>Cues.FirstOrDefault(c=>c.Start<=seconds*1000&&c.End>seconds*1000)?.Text??"";
    public static SubtitleTranscript FromWhisperJson(string json,double duration)
    {
        using var parsed=JsonDocument.Parse(json);var cues=new List<SubtitleCodec.Cue>();
        foreach(var item in parsed.RootElement.GetProperty("transcription").EnumerateArray())
        {
            var offsets=item.GetProperty("offsets");var start=offsets.GetProperty("from").GetInt64();var end=Math.Min(offsets.GetProperty("to").GetInt64(),(long)(duration*1000));
            var text=(item.GetProperty("text").GetString()??"").Trim();
            if(text.Length>0&&start<end)cues.Add(new(start,end,text));
        }
        return new(cues,duration);
    }
    public static SubtitleTranscript FromSrt(string text,double duration)=>new(SubtitleCodec.Parse(text),duration);
    public static SubtitleTranscript FromCues(IEnumerable<SubtitleCodec.Cue> cues,double duration)=>new(cues,duration);
}
