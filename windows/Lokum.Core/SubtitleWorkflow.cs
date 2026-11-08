namespace Lokum.Core;

public sealed class SubtitleWorkflow(double duration)
{
    public double Duration=>duration;
    public SubtitleTranscript? Source{get;private set;}
    public SubtitleTranscript? Subtitles{get;private set;}
    public string? SourceLanguage{get;private set;}
    public string? TargetLanguage{get;private set;}
    public long Revision{get;private set;}
    public bool CanTranslate=>Source is not null&&SourceLanguage is not null&&TargetLanguage is not null;
    public bool CanSave=>Subtitles is not null&&CanTranslate;
    public static void ValidateLanguage(string? language)
    {
        if(language is null or "auto"||!TranscriptionEngine.Languages.ContainsKey(language))throw new ArgumentException("Choose the video's source language and the subtitle language.");
    }
    public void SetSource(string srt,string language)
    {
        InvalidateSource();ValidateLanguage(language);Source=SubtitleTranscript.FromSrt(srt,duration);SourceLanguage=language;
    }
    public void ChooseTarget(string language){ValidateLanguage(language);TargetLanguage=language;Subtitles=null;Revision++;}
    public bool AcceptTranslation(string srt,long revision,string target)
    {
        if(!CanTranslate||Revision!=revision||TargetLanguage!=target)return false;
        Subtitles=SubtitleTranscript.FromSrt(srt,duration);return true;
    }
    public void UpdateSubtitles(string srt){if(!CanTranslate)throw new InvalidOperationException("Create and review the source transcript first.");Subtitles=null;Subtitles=SubtitleTranscript.FromSrt(srt,duration);}
    public bool UseSourceLanguage(){if(!CanTranslate||SourceLanguage!=TargetLanguage)return false;Subtitles=Source;return true;}
    public void InvalidateSource(){Source=null;SourceLanguage=null;Subtitles=null;Revision++;}
}
