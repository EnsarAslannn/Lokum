using Lokum.Core;

internal static class SubtitleWorkflowChecks
{
    internal static async Task Integration(Action<bool,string> check,string model)
    {
        var source=SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nHello, how are you?\n\n2\n00:00:06,000 --> 00:00:09,000\nPlease close the door.\n",10);
        var executable=Path.Combine(AppContext.BaseDirectory,"Tools","Translate","translate.exe");var engine=new SubtitleTranslationEngine(executable,model);
        foreach(var target in new[]{"tr","de","fr"})
        {
            var translated=await engine.TranslateAsync(source,10,"en",target);
            check(target switch{"tr"=>translated.Text.Contains("kapı"),"de"=>translated.Text.Contains("Tür"),_=>translated.Text.Contains("porte")},"Real local translation must produce the selected "+target+" language.");
            check(translated.Cues[0].Start==1000&&translated.Cues[^1].End==9000&&!translated.Cues.Any(c=>c.Start<6000&&c.End>4000),"Translation must preserve timing boundaries and the silent gap.");
        }
        var corrected=SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nPlease close the window.\n",10);
        check((await engine.TranslateAsync(corrected,10,"en","tr")).Text.Contains("pencere"),"Translation uses the corrected source transcript, rather than repeating audio recognition.");
        check(source.Text.Contains("door")&&!source.Text.Contains("kapı"),"Translation leaves the original source transcript unchanged.");
        var bypass=await new SubtitleTranslationEngine("missing.exe","missing-model").TranslateAsync(source,10,"en","en");
        check(bypass.Srt==source.Srt,"Same-language subtitles need no extra model or executable.");
        using var cancel=new CancellationTokenSource();cancel.Cancel();var stopped=false;try{await engine.TranslateAsync(source,10,"en","tr",token:cancel.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"Translation cancellation stops before creating a partial result.");
    }
    internal static void Run(Action<bool,string> check)
    {
        const string english="1\n00:00:01,000 --> 00:00:03,000\nPlease close the door.\n";
        const string turkish="1\n00:00:01,000 --> 00:00:03,000\nLütfen kapıyı kapatın.\n";
        var workflow=new SubtitleWorkflow(4);
        check(!workflow.CanTranslate&&!workflow.CanSave,"A subtitle language cannot replace the source-language transcript stage.");
        var rejected=false;try{workflow.SetSource(english,"auto");}catch(ArgumentException){rejected=true;}
        check(rejected,"The source language must be explicitly selected.");
        workflow.SetSource(english,"en");workflow.ChooseTarget("tr");var revision=workflow.Revision;
        check(workflow.CanTranslate&&!workflow.CanSave,"Choosing a different subtitle language requires translation.");
        check(workflow.AcceptTranslation(turkish,revision,"tr")&&workflow.Subtitles!.At(2)=="Lütfen kapıyı kapatın.","Translated subtitles reach preview and export.");
        check(workflow.Source!.At(2)=="Please close the door.","Translation preserves the reviewed source transcript.");
        workflow.SetSource(english.Replace("door","window"),"en");
        check(!workflow.CanSave&&!workflow.AcceptTranslation(turkish,revision,"tr"),"Editing the source invalidates the previous translation and rejects stale work.");
        workflow.ChooseTarget("de");var latest=workflow.Revision;
        check(!workflow.AcceptTranslation(turkish,latest,"tr"),"A completed translation cannot be used for another target language.");
        workflow.ChooseTarget("en");check(workflow.UseSourceLanguage()&&workflow.Subtitles!.Text.Contains("window"),"Same-language subtitles use the corrected transcript without a translation model.");
        workflow.InvalidateSource();check(!workflow.CanSave&&!workflow.CanTranslate,"Changing the video's source language requires a new transcript.");
    }
}
