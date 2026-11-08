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
    private static async Task Until(Func<bool> ready)
    {
        var deadline=DateTime.UtcNow.AddSeconds(120);
        while(!ready()){if(DateTime.UtcNow>deadline)throw new TimeoutException("Subtitle editor did not finish processing.");await Task.Delay(50);}
    }
    internal static async Task Run(string output,string source,string toolDirectory,string speechDirectory)
    {
        Directory.CreateDirectory(output);foreach(var name in new[]{"verification.json","error.txt","subtitles-tr-light.png"})File.Delete(Path.Combine(output,name));Theme.Apply("light");L10n.Language="tr";var checks=new List<string>();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
        var tools=new Toolchain(toolDirectory);var info=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(source);
        var owner=new Window{Width=200,Height=120};owner.Show();var editor=new SubtitleEditorWindow(owner,source,info,tools,new SpeechModelSetup(speechDirectory));
        try
        {
            editor.Show();editor.UpdateLayout();
            var textbox=Find<TextBox>(editor).Single(t=>AutomationProperties.GetAutomationId(t)=="subtitles.editor");
            var save=Find<Button>(editor).Single(b=>AutomationProperties.GetAutomationId(b)=="subtitles.save");
            var preview=Find<Button>(editor).Single(b=>AutomationProperties.GetAutomationId(b)=="subtitles.preview");
            Check(Find<TextBlock>(editor).Any(t=>t.Text=="Otomatik algıla"),"The selected speech language is visible and translated.");
            Check(preview.Content as string=="Sonucu göster","The result preview button is translated into Turkish.");
            Check(!save.IsEnabled&&!preview.IsEnabled,"Empty subtitles cannot be saved or previewed.");
            textbox.Text="1\n00:00:00,250 --> 00:00:03,000\nDeneme altyazısı.\n";
            Check(save.IsEnabled&&editor.Transcript.At(1)=="Deneme altyazısı.","Text and timestamp edits reach the save document.");
            textbox.Text="1\n00:00:03,000 --> 00:00:02,000\nWrong\n";
            Check(!save.IsEnabled&&!preview.IsEnabled,"Invalid timestamps block saving and preview.");
            var generate=Find<Button>(editor).Single(b=>AutomationProperties.GetAutomationId(b)=="subtitles.generate");generate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(()=>generate.IsEnabled);Check(save.IsEnabled&&textbox.Text.Contains("country",StringComparison.OrdinalIgnoreCase),"Generate button runs real local speech recognition.");
            var formats=Find<ComboBox>(editor).Single(c=>c.Items.Cast<object>().Any(i=>i as string=="MP4"));formats.SelectedItem="MP4";
            preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Until(()=>preview.IsEnabled);
            var player=Find<MediaElement>(editor).Single();
            await Until(()=>player.NaturalVideoWidth>0);
            Check(player.NaturalVideoWidth==640&&player.HasAudio,"The rendered subtitle preview opens in the native media player.");
            Check(player.Source is not null&&player.Source.LocalPath!=source&&File.Exists(player.Source.LocalPath),"Show result plays an actual subtitled MP4 before saving.");
            Check(editor.Transcript.Text.Contains("country",StringComparison.OrdinalIgnoreCase),"Video preview preserves editable recognized text.");
            var image=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(editor);ImageEngine.Save(image,Path.Combine(output,"subtitles-tr-light.png"),"png");
            textbox.Text="1\n00:00:00,000 --> 00:00:02,000\nYeni metin.\n";
            Check(player.Source?.LocalPath==source&&editor.Transcript.Text=="Yeni metin.\n","Editing invalidates the rendered preview and restores the original media.");
            var opus=Path.Combine(output,"speech.opus");
            await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-v","error","-i",source,"-vn","-c:a","libopus",opus]);
            var audioInfo=await new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe")).InspectAsync(opus);
            var audioEditor=new SubtitleEditorWindow(owner,opus,audioInfo,tools,new SpeechModelSetup(speechDirectory));
            try
            {
                audioEditor.Show();audioEditor.UpdateLayout();var audio=Find<MediaElement>(audioEditor).Single();
                await Until(()=>audio.HasAudio);
                Check(audio.HasAudio,"Compressed speech audio opens in the subtitle preview, using a local playback copy when needed.");
                Check(!Find<ComboBox>(audioEditor).Any(c=>c.Items.Cast<object>().Any(i=>i as string=="MP4")),"Audio subtitles offer text formats without an unsupported MP4 export.");
            }
            finally{audioEditor.Close();}
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks=checks.Count,details=checks},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{editor.Close();owner.Close();}
    }
}
