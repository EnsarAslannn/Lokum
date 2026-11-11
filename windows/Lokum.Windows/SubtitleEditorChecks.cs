using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;

namespace Lokum.Windows;

internal static class SubtitleEditorChecks
{
    private static IEnumerable<T> Find<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T item)yield return item;
        for(var n=0;n<VisualTreeHelper.GetChildrenCount(root);n++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,n)))yield return child;
    }
    private static async Task Until(Func<bool> ready,string step="Subtitle editor")
    {
        var deadline=DateTime.UtcNow.AddSeconds(120);
        while(!ready()){if(DateTime.UtcNow>deadline)throw new TimeoutException(step+" did not finish processing.");await Task.Delay(50);}
    }
    internal static async Task Run(string output,string source,string toolDirectory,string speechDirectory)
    {
        Directory.CreateDirectory(output);foreach(var name in new[]{"verification.json","error.txt","subtitles-tr-light.png"})File.Delete(Path.Combine(output,name));Theme.Apply("light");L10n.Language="tr";var checks=new List<string>();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
        var tools=new Toolchain(toolDirectory);var info=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(source);
        var owner=new Window{Width=200,Height=120};owner.Show();var model=Environment.GetEnvironmentVariable("LOKUM_TEST_TRANSLATION_MODEL");var editor=new SubtitleEditorWindow(owner,source,info,tools,new SpeechModelSetup(speechDirectory),model is null?null:new TranslationModelSetup(model));
        try
        {
            var startupVideo=Path.Combine(output,"Startup-blue.mp4");
            await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-v","error","-f","lavfi","-i","color=blue:size=640x360:rate=15","-f","lavfi","-i","sine=frequency=440:sample_rate=48000","-t","3","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",startupVideo]);
            var startupInfo=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(startupVideo);
            var startup=new SubtitleEditorWindow(owner,startupVideo,startupInfo,tools,new SpeechModelSetup(speechDirectory));
            try
            {
                startup.Show();startup.UpdateLayout();
                var loading=Find<FrameworkElement>(startup).FirstOrDefault(e=>AutomationProperties.GetAutomationId(e)=="subtitles.previewLoading");
                Check(loading is {IsVisible:true},"Opening the subtitle editor shows a loading state before the first media frame is ready.");
                var initialPlayer=Find<MediaElement>(startup).Single();
                bool BlueFrame(MediaElement current)
                {
                    if(current.ActualWidth<1||current.ActualHeight<1)return false;
                    var frame=new RenderTargetBitmap((int)current.ActualWidth,(int)current.ActualHeight,96,96,PixelFormats.Pbgra32);frame.Render(current);
                    var pixel=new byte[4];frame.CopyPixels(new Int32Rect(frame.PixelWidth/2,frame.PixelHeight/2,1,1),pixel,4,0);return pixel[0]>150&&pixel[1]<100&&pixel[2]<100;
                }
                await Until(()=>BlueFrame(initialPlayer),"Paused subtitle editor first frame");
                Check(BlueFrame(initialPlayer)&&!loading!.IsVisible,"The first colored video frame is visible without pressing Play, and the loading state disappears.");
                await Task.Delay(350);Check(initialPlayer.Position.TotalSeconds<.15,"Opening a subtitle editor does not automatically play the video or its audio.");
                var original=Find<Button>(startup).Single(b=>b.Content as string==L10n.Text("Original"));original.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));original.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(150);Check(BlueFrame(initialPlayer)&&!loading!.IsVisible,"Selecting the already loaded original does not leave a loading indicator waiting for an event that will never recur.");
                var play=Find<Button>(startup).Single(b=>b.Content as string==L10n.Text("Play / pause"));play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>initialPlayer.Position.TotalSeconds>.3,"Subtitle editor playback");original.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(()=>initialPlayer.Position.TotalSeconds<.15&&BlueFrame(initialPlayer),"Restoring the initial subtitle frame");
                Check(!loading!.IsVisible,"Returning from playback restores the first paused frame without obscuring it with a stale loading state.");
                var startupImage=new RenderTargetBitmap((int)startup.ActualWidth,(int)startup.ActualHeight,96,96,PixelFormats.Pbgra32);startupImage.Render(startup);ImageEngine.Save(startupImage,Path.Combine(output,"startup-first-frame.png"),"png");
                var webm=Path.Combine(output,"Startup-blue.webm");await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-v","error","-i",startupVideo,"-c:v","libvpx-vp9","-deadline","realtime","-cpu-used","8","-c:a","libopus",webm]);
                var proxyInfo=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(webm);
                var proxyEditor=new SubtitleEditorWindow(owner,webm,proxyInfo,tools,new SpeechModelSetup(speechDirectory));
                try
                {
                    proxyEditor.Show();proxyEditor.UpdateLayout();var proxyLoading=Find<FrameworkElement>(proxyEditor).First(e=>AutomationProperties.GetAutomationId(e)=="subtitles.previewLoading");var proxyPlayer=Find<MediaElement>(proxyEditor).Single();
                    Check(proxyLoading.IsVisible,"Video formats requiring a local playback copy show loading feedback during preparation.");
                    await Until(()=>BlueFrame(proxyPlayer)&&!proxyLoading.IsVisible,"Converted subtitle source first frame");
                    Check(proxyPlayer.Source?.LocalPath!=webm&&proxyPlayer.Position.TotalSeconds<.15,"A converted video opens on its first frame and stays paused, like a directly supported video.");
                }
                finally{proxyEditor.Close();}
                var invalid=Path.Combine(output,"Invalid-preview.mp4");await File.WriteAllTextAsync(invalid,"not a video");var failedEditor=new SubtitleEditorWindow(owner,invalid,startupInfo,tools,new SpeechModelSetup(speechDirectory));
                try
                {
                    failedEditor.Show();failedEditor.UpdateLayout();var failedLoading=Find<FrameworkElement>(failedEditor).First(e=>AutomationProperties.GetAutomationId(e)=="subtitles.previewLoading");
                    await Until(()=>failedLoading.IsVisible&&!Find<ProgressBar>(failedLoading).Any(p=>p.IsVisible),"Invalid subtitle source feedback");
                    Check(Find<TextBlock>(failedLoading).Any(t=>!string.IsNullOrWhiteSpace(t.Text)),"An unreadable preview ends the loading animation and displays an explanation.");
                }
                finally{failedEditor.Close();}
            }
            finally{startup.Close();}
            editor.Show();editor.UpdateLayout();
            Check(editor.Format=="mp4","A video opens with MP4 selected so Save creates a subtitled video instead of a text sidecar.");
            var sourceLanguage=Find<ComboBox>(editor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.sourceLanguage");
            var targetLanguage=Find<ComboBox>(editor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.targetLanguage");
            var sourceBox=Find<TextBox>(editor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.source");
            var translate=Find<Button>(editor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.translate");
            var tabs=Find<TabControl>(editor).Single();
            var save=Find<Button>(editor).Single(b=>AutomationProperties.GetAutomationId(b)=="subtitles.save");
            var preview=Find<Button>(editor).Single(b=>AutomationProperties.GetAutomationId(b)=="subtitles.preview");
            var generate=Find<Button>(editor).Single(b=>AutomationProperties.GetAutomationId(b)=="subtitles.generate");
            Check(sourceLanguage.SelectedIndex<0&&!generate.IsEnabled&&!targetLanguage.IsEnabled,"Creating subtitles requires an explicit source language before choosing a target.");
            sourceLanguage.SelectedItem="English";
            Check(targetLanguage.Items.Cast<string>().SequenceEqual(new[]{"Turkish","English"})&&sourceLanguage.Items.Cast<string>().Contains("German"),"Turkish and English are the only subtitle targets; the original language can still be different.");
            Check(preview.Content as string=="Sonucu göster","The result preview button is translated into Turkish.");
            Check(!save.IsEnabled&&!preview.IsEnabled,"Empty subtitles cannot be saved or previewed.");
            sourceBox.Text="1\n00:00:00,250 --> 00:00:03,000\nPlease close the door.\n";
            Check(!save.IsEnabled&&targetLanguage.IsEnabled,"A valid source transcript enables target selection but cannot be saved as an ungenerated translation.");
            targetLanguage.SelectedItem=model is null?"English":"Turkish";translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>translate.IsEnabled);
            tabs.SelectedIndex=1;editor.UpdateLayout();var textbox=Find<TextBox>(editor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.editor");
            Check(save.IsEnabled&&(model is null?editor.Transcript.Text.Contains("door"):editor.Transcript.Text.Contains("kapı")),"The selected target subtitles are generated from the reviewed transcript.");
            if(model is not null)
            {
                tabs.SelectedIndex=0;sourceBox.Text=sourceBox.Text.Replace("door","window");Check(!save.IsEnabled,"A source correction cannot reuse the previous translated result.");
                translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>translate.IsEnabled);
                Check(editor.Transcript.Text.Contains("pencere")&&!editor.Transcript.Text.Contains("kapı"),"Regeneration translates the edited source transcript instead of the audio or old subtitle draft.");
            }
            textbox.Text="1\n00:00:03,000 --> 00:00:02,000\nWrong\n";Check(!save.IsEnabled&&!preview.IsEnabled,"Invalid target timing blocks saving and preview.");
            tabs.SelectedIndex=0;generate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(()=>generate.IsEnabled);Check(!save.IsEnabled&&sourceBox.Text.Contains("country",StringComparison.OrdinalIgnoreCase),"Speech recognition creates only the source-language transcript.");
            targetLanguage.SelectedItem="English";translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>translate.IsEnabled);editor.UpdateLayout();textbox=Find<TextBox>(editor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.editor");
            preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>preview.IsEnabled);
            var player=Find<MediaElement>(editor).Single();
            await Until(()=>player.NaturalVideoWidth>0,"Subtitled MP4 playback");
            Check(player.NaturalVideoWidth==640&&player.HasAudio,"The rendered subtitle preview opens in the native media player.");
            Check(player.Source is not null&&player.Source.LocalPath!=source&&File.Exists(player.Source.LocalPath),"Show result plays an actual subtitled MP4 before saving.");
            Check(!player.IsMuted&&player.Volume>0,"The subtitle result player is audible rather than muted.");
            var saved=(await TranscriptionEngine.ExportAsync(editor.Transcript,source,editor.Format,Path.Combine(output,"saved-default"),tools.Executable("ffmpeg")))[0];
            var savedInfo=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(saved);
            Check(Path.GetExtension(saved)==".mp4"&&savedInfo.HasAudio&&savedInfo.Video is not null,"Saving with the initial format produces an MP4 containing both video and audio.");
            Check(editor.Transcript.Text.Contains("country",StringComparison.OrdinalIgnoreCase),"Video preview preserves editable recognized text.");
            var image=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(editor);ImageEngine.Save(image,Path.Combine(output,"subtitles-tr-light.png"),"png");
            var formats=Find<ComboBox>(editor).Single(c=>c.Items.Cast<object>().Any(i=>i as string=="MP4"));formats.SelectedItem="SRT";
            var sidecar=(await TranscriptionEngine.ExportAsync(editor.Transcript,source,editor.Format,Path.Combine(output,"saved-sidecar"),tools.Executable("ffmpeg")))[0];
            Check(Path.GetExtension(sidecar)==".srt"&&SubtitleCodec.Parse(await File.ReadAllTextAsync(sidecar)).Length>0,"SRT remains an explicit option for a separate timed subtitle file.");formats.SelectedItem="MP4";
            textbox.Text="1\n00:00:00,000 --> 00:00:02,000\nYeni metin.\n";
            Check(player.Source?.LocalPath==source&&editor.Transcript.Text=="Yeni metin.\n","Editing invalidates the rendered preview and restores the original media.");
            tabs.SelectedIndex=0;sourceBox.Text=sourceBox.Text.Replace("country","world");Check(!save.IsEnabled&&!preview.IsEnabled,"Editing the reviewed source invalidates all previously generated target subtitles.");
            if(model is not null)
            {
                var namesVideo=Path.Combine(output,"Subtitle-name-fixture.mp4");
                await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-v","error","-f","lavfi","-i","color=black:size=640x360:rate=15","-f","lavfi","-i","anullsrc=r=48000:cl=stereo","-t","12","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",namesVideo]);
                var namesInfo=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(namesVideo);
                var namesEditor=new SubtitleEditorWindow(owner,namesVideo,namesInfo,tools,new SpeechModelSetup(speechDirectory),new TranslationModelSetup(model));
                const string originalNames="1\n00:00:00,000 --> 00:00:02,400\nAbi Yaren Duman'ın doğum gününü kutlayıp\n\n2\n00:00:02,400 --> 00:00:04,040\nkokulu ve öpücükler yollar mısın?\n\n3\n00:00:04,040 --> 00:00:06,480\nYaren Duman'cım doğum günün kutlu olsun kardeş.\n\n4\n00:00:07,920 --> 00:00:10,710\nRabbim hayırlı günler versin. Baba.\n\n5\n00:00:10,710 --> 00:00:12,000\nCanım benim seni seviyoruz.\n";
                try
                {
                    namesEditor.Show();namesEditor.UpdateLayout();
                    Find<ComboBox>(namesEditor).Single(c=>AutomationProperties.GetAutomationId(c)=="subtitles.sourceLanguage").SelectedItem="Turkish";
                    var originalBox=Find<TextBox>(namesEditor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.source");originalBox.Text=originalNames;
                    Find<ComboBox>(namesEditor).Single(c=>AutomationProperties.GetAutomationId(c)=="subtitles.targetLanguage").SelectedItem="English";
                    var action=Find<Button>(namesEditor).Single(b=>AutomationProperties.GetAutomationId(b)=="subtitles.translate");action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>action.IsEnabled);
                    var english=namesEditor.Transcript.Text;
                    Check(english.Split("Yaren Duman").Length-1==2&&!english.Contains("smok",StringComparison.OrdinalIgnoreCase),"The real editor preserves a full Turkish name in both the request and affectionate birthday greeting.");
                    Check((english.Contains("Dad",StringComparison.OrdinalIgnoreCase)||english.Contains("Father",StringComparison.OrdinalIgnoreCase))&&english.Contains("we love you",StringComparison.OrdinalIgnoreCase),"Short addresses and plural subjects remain present in English subtitles.");
                    Check(originalBox.Text==originalNames&&namesEditor.Transcript.Cues[^1].End==12000,"Translation preserves the complete reviewed transcript and final timestamp.");
                    File.WriteAllText(Path.Combine(output,"names-english.srt"),namesEditor.Transcript.Srt);
                }
                finally{namesEditor.Close();}
            }
            var opus=Path.Combine(output,"speech.opus");
            await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-v","error","-i",source,"-vn","-c:a","libopus",opus]);
            var audioInfo=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(opus);
            var audioEditor=new SubtitleEditorWindow(owner,opus,audioInfo,tools,new SpeechModelSetup(speechDirectory));
            try
            {
                audioEditor.Show();audioEditor.UpdateLayout();var audio=Find<MediaElement>(audioEditor).Single();
                Check(audioEditor.Format=="srt","Audio-only media defaults to SRT rather than an unsupported MP4 video.");
                await Until(()=>audio.Source is {} url&&url.LocalPath!=opus&&File.Exists(url.LocalPath),"Compressed audio preview preparation");
                var decoded=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(audio.Source.LocalPath);
                Check(decoded.HasAudio&&decoded.Streams.Any(s=>s.Codec=="pcm_s16le")&&Math.Abs(decoded.Duration-audioInfo.Duration)<.1,"Compressed speech is decoded into a complete PCM playback copy without truncation.");
                if(Environment.GetEnvironmentVariable("LOKUM_TEST_AUDIO_DEVICE")=="1")
                {
                    await Until(()=>audio.HasAudio,"Native playback with a physical audio device");
                    Check(audio.HasAudio,"The prepared speech preview opens on the machine's audio device.");
                }
                Check(!Find<ComboBox>(audioEditor).Any(c=>c.Items.Cast<object>().Any(i=>i as string=="MP4")),"Audio subtitles offer text formats without an unsupported MP4 export.");
            }
            finally{audioEditor.Close();}
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks=checks.Count,details=checks},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{editor.Close();owner.Close();}
    }
}
