using Lokum.Core;

internal static class SubtitleWorkflowChecks
{
    internal static async Task Integration(Action<bool,string> check,string model)
    {
        if(Environment.GetEnvironmentVariable("LOKUM_VERIFY_INSTALL_TRANSLATION") is {} directory)
        {
            var setup=new TranslationModelSetup(directory);await setup.InstallAsync("fr","en");
            check(setup.ReadyFor("fr","en"),"A fresh verified language-pair download extracts and publishes a usable model.");
        }
        var source=SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nHello, how are you?\n\n2\n00:00:06,000 --> 00:00:09,000\nPlease close the door.\n",10);
        var executable=Path.Combine(AppContext.BaseDirectory,"Tools","Translate","translate.exe");var engine=new SubtitleTranslationEngine(executable,model);
        foreach(var (from,target,original,word) in new[]{("en","tr",source,"kapı"),("tr","en",SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nLütfen kapıyı kapatın.\n\n2\n00:00:06,000 --> 00:00:09,000\nLütfen pencereyi kapatın.\n",10),"door"),("de","en",SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nBitte schließen Sie die Tür.\n\n2\n00:00:06,000 --> 00:00:09,000\nBitte schließen Sie das Fenster.\n",10),"door"),("fr","tr",SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nVeuillez fermer la porte.\n\n2\n00:00:06,000 --> 00:00:09,000\nVeuillez fermer la fenêtre.\n",10),"kapı")})
        {
            var translated=await engine.TranslateAsync(original,10,from,target);
            check(translated.Text.Contains(word,StringComparison.OrdinalIgnoreCase),"Real local translation from "+from+" must produce the selected "+target+" language: "+translated.Text);
            check(translated.Cues[0].Start==1000&&translated.Cues[^1].End==9000&&!translated.Cues.Any(c=>c.Start<6000&&c.End>4000),"Translation must preserve timing boundaries and the silent gap.");
        }
        var corrected=SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nPlease close the window.\n",10);
        check((await engine.TranslateAsync(corrected,10,"en","tr")).Text.Contains("pencere"),"Translation uses the corrected source transcript, rather than repeating audio recognition.");
        var birthday=SubtitleTranscript.FromSrt("1\n00:00:01,000 --> 00:00:04,000\nBaba canım benim seni seviyoruz.\n",10);
        check((await engine.TranslateAsync(birthday,10,"tr","en")).Text.Contains("we love you",StringComparison.OrdinalIgnoreCase),"The birthday message preserves the source's plural subject instead of inventing I love you.");
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
        check(TranslationModelSetup.RequiredPairs("fr","tr").SequenceEqual(new[]{"fr-en","en-tr"})&&TranslationModelSetup.RequiredPairs("tr","tr").Count==0,"Translation chooses the source-specific English model and Turkish model only when necessary.");
        workflow.SetSource(english,"en");workflow.ChooseTarget("tr");var revision=workflow.Revision;
        check(workflow.CanTranslate&&!workflow.CanSave,"Choosing a different subtitle language requires translation.");
        check(workflow.AcceptTranslation(turkish,revision,"tr")&&workflow.Subtitles!.At(2)=="Lütfen kapıyı kapatın.","Translated subtitles reach preview and export.");
        check(workflow.Source!.At(2)=="Please close the door.","Translation preserves the reviewed source transcript.");
        workflow.SetSource(english.Replace("door","window"),"en");
        check(!workflow.CanSave&&!workflow.AcceptTranslation(turkish,revision,"tr"),"Editing the source invalidates the previous translation and rejects stale work.");
        var unsupported=false;try{workflow.ChooseTarget("de");}catch(ArgumentException){unsupported=true;}
        check(unsupported,"Subtitles can only target Turkish or English while the source language remains independent.");
        workflow.ChooseTarget("en");var latest=workflow.Revision;
        check(!workflow.AcceptTranslation(turkish,latest,"tr"),"A completed translation cannot be used for another target language.");
        workflow.ChooseTarget("en");check(workflow.UseSourceLanguage()&&workflow.Subtitles!.Text.Contains("window"),"Same-language subtitles use the corrected transcript without a translation model.");
        workflow.InvalidateSource();check(!workflow.CanSave&&!workflow.CanTranslate,"Changing the video's source language requires a new transcript.");
    }
}
