using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;
namespace Lokum.Windows;

internal static class ConversionWorkspaceUiChecks
{
    private static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item)yield return item;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private static async Task Until(Func<bool> ready,string context)
    {
        var deadline=DateTime.UtcNow.AddSeconds(120);
        while(!ready()){if(DateTime.UtcNow>deadline)throw new Exception("Preview timeout: "+context);await Task.Delay(50);}
    }
    internal static async Task Run(string output,string toolDirectory)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();var seen=new List<string>();
        void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);File.WriteAllText(Path.Combine(output,"progress.json"),JsonSerializer.Serialize(new{checks=checks.Count,last=message,seen}));}
        L10n.Language=Environment.GetEnvironmentVariable("LOKUM_VIDEO_UI_LANGUAGE")??"en";Theme.Apply("light");
        var prefs=new PreferencesStore(Path.Combine(output,"state"));prefs.Save(new(){Language=L10n.Language,Enabled=false,KeepInTray=false,AutomaticUpdateChecks=false,Feedback=false,Animations=false});
        foreach(var target in FeatureCatalog.Conversions.Values.SelectMany(v=>v).Distinct())prefs.SetProfile(target,new(PreserveMetadata:false));
        var png=Path.Combine(output,"source.png");var pixels=BitmapSource.Create(160,96,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat(new byte[]{40,90,180,255},160*96).SelectMany(b=>b).ToArray(),160*4);pixels.Freeze();ImageEngine.Save(pixels,png,"png");
        var library=new LibraryStore(Path.Combine(output,"state"));library.SetAppearance("light");var main=new MainWindow([png],library,checkForUpdates:false,offerToolSetup:false);main.Show();main.UpdateLayout();
        var tools=new Toolchain(toolDirectory);var officeRoot=Environment.GetEnvironmentVariable("LOKUM_VERIFY_OFFICE_TOOLS")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lokum","Tools");var office=new OfficeRuntime(officeRoot);var metadata=new MetadataService(officeRoot);
        var sources=new Dictionary<string,string>{{"png",png}};
        try
        {
            Check(main.UIAuditToolChoices.Count(c=>c.Id=="convert")==1,"The tools wheel offers one Convert entry for compatible files");
            var jpg=Path.Combine(output,"source.jpg");ImageEngine.Save(pixels,jpg,"jpg");sources["jpg"]=jpg;
            var media=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));var video=Path.Combine(output,"source.mp4");await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-f","lavfi","-i","testsrc2=size=160x96:rate=12:duration=2","-f","lavfi","-i","sine=duration=2","-c:v","libx264","-c:a","aac","-shortest",video]);sources["mp4"]=video;
            sources["mov"]=(await media.ConvertAsync([video],"mov",output,new()))[0];sources["mp3"]=(await media.ConvertAsync([video],"mp3",output,new()))[0];
            var text=Path.Combine(output,"source.txt");File.WriteAllText(text,"Lokum preview text\nA converted document before saving.");sources["txt"]=text;
            var srt=Path.Combine(output,"source.srt");File.WriteAllText(srt,"1\n00:00:00,000 --> 00:00:01,000\nLokum subtitle\n");sources["srt"]=srt;sources["vtt"]=Path.Combine(output,"source.vtt");File.WriteAllText(sources["vtt"],SubtitleCodec.Convert(File.ReadAllText(srt),"vtt"));
            sources["zip"]=DocumentEngine.Zip([text],output);sources["tar"]=ArchiveEngine.Convert(sources["zip"],"tar",output)[0];
            var archiveViewer=new FilePreviewWindow(main,[sources["zip"]],tools,office);
            try{archiveViewer.Show();await Until(()=>archiveViewer.PreviewStatus!=L10n.Text("Preparing preview…"),"archive content");Check(archiveViewer.PreviewStatus=="TextBox","Archive results show their file list instead of requiring an external application");}finally{archiveViewer.Close();}
            var pairs=new List<(string Source,string Target)>();
            pairs.AddRange(new[]{"jpg","webp","heic","avif","tiff","bmp","pdf"}.Select(f=>("png",f)));pairs.Add(("jpg","png"));
            pairs.AddRange(new[]{"mov","mkv","webm","avi","wmv","gif","mp3"}.Select(f=>("mp4",f)));pairs.Add(("mov","mp4"));
            pairs.AddRange(new[]{"m4a","wav","flac","ogg","opus","aiff","wma"}.Select(f=>("mp3",f)));
            pairs.AddRange(new[]{("srt","vtt"),("vtt","srt"),("srt","txt"),("zip","tar"),("zip","gz"),("tar","zip")});
            if(office.Executable is not null)
            {
                var documents=new DocumentConversion(office,tools);sources["docx"]=(await documents.ConvertAsync([text],"docx",output,new(),null,CancellationToken.None))[0];sources["pdf"]=(await documents.ConvertAsync([text],"pdf",output,new(),null,CancellationToken.None))[0];
                sources["csv"]=Path.Combine(output,"source.csv");File.WriteAllText(sources["csv"],"Fruit,Count\nPear,3\n");sources["tsv"]=Path.Combine(output,"source.tsv");File.WriteAllText(sources["tsv"],"Fruit\tCount\nPear\t3\n");
                pairs.AddRange(new[]{("txt","docx"),("docx","pages"),("pdf","md"),("txt","html"),("txt","epub"),("csv","xlsx"),("csv","ods"),("csv","tsv"),("tsv","csv"),("pdf","pptx")});
            }
            foreach(var pair in pairs)
            {
                var filter=Environment.GetEnvironmentVariable("LOKUM_CONVERSION_FORMATS");if(!string.IsNullOrWhiteSpace(filter)&&!filter.Split(',').Contains(pair.Target))continue;
                var input=sources[pair.Source];var hash=SHA256.HashData(File.ReadAllBytes(input));var info=FeatureCatalog.IsVideo(input)||FeatureCatalog.IsAudio(input)?await media.InspectAsync(input):null;
                var inputs=new[]{input};
                if(pair.Target=="jpg"){var second=Path.Combine(output,"second.png");var resized=new TransformedBitmap(pixels,new ScaleTransform(.5,.5));resized.Freeze();ImageEngine.Save(resized,second,"png");inputs=[input,second];}
                var editor=new ToolEditor(main,"convert","Convert",input,info,FeatureCatalog.IsImage(input)?ImageEngine.Load(input):null,inputs:inputs,outputFormats:FeatureCatalog.Formats(inputs)){Width=900,Height=650};
                editor.ConfigureProfiles(prefs,pair.Target);editor.ConfigureMediaRuntime(tools);
                editor.ConfigureConversionRuntime(tools,office,metadata);
                FilePreviewWindow? viewer=null;
                try
                {
                    editor.Show();editor.UpdateLayout();var button=Find<Button>(editor).SingleOrDefault(b=>AutomationProperties.GetAutomationId(b)=="conversion.preview");
                    Check(button is {IsVisible:true},pair.Target+": conversion offers Show Result before saving");_ = editor.Dispatcher.BeginInvoke(()=>button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                    await Until(()=>{viewer=editor.OwnedWindows.OfType<FilePreviewWindow>().SingleOrDefault();return viewer is not null;},pair.Target+" generation");
                    await Until(()=>viewer!.PreviewStatus is "Image" or "MediaElement" or "TextBox"||viewer.PreviewStatus=="DockPanel"&&Find<Image>(viewer).Any(image=>image.Source is not null),pair.Target+" display: "+viewer!.PreviewStatus);
                    Check(FeatureCatalog.Extension(viewer!.CurrentPath)==pair.Target,pair.Target+": preview uses a real file in the chosen target format");
                    Check(!Path.GetDirectoryName(viewer.CurrentPath)!.Equals(output,StringComparison.OrdinalIgnoreCase),pair.Target+": preview stays in temporary storage");
                    Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))),pair.Target+": source remains unchanged");
                    Check(library.Entries.Count==0,pair.Target+": preview is not recorded as a saved Library export");
                    if(inputs.Length==2){var first=viewer.CurrentPath;viewer.Navigate(1);await Until(()=>viewer.PreviewStatus=="Image","second batch image");Check(viewer.CurrentPath!=first,"A batch preview includes the second converted file");}
                    seen.Add(pair.Target);viewer.Close();viewer=null;await Until(()=>button!.IsEnabled,pair.Target+" close");
                    Check(Find<TextBlock>(editor).Any(t=>AutomationProperties.GetAutomationId(t)=="conversion.result.summary"&&t.Text.Contains(pair.Target.ToUpperInvariant())),pair.Target+": format and size remain visible before Save");
                    if(pair.Target=="jpg")
                    {
                        Find<ComboBox>(editor).Single(c=>c.Items.Contains("jpg")&&c.Items.Contains("webp")).SelectedItem="webp";
                        Check(!Find<TextBlock>(editor).Any(t=>AutomationProperties.GetAutomationId(t)=="conversion.result.summary"&&t.IsVisible),"Changing the target format clears the previous result summary");
                        _ = editor.Dispatcher.BeginInvoke(()=>button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                        await Until(()=>{viewer=editor.OwnedWindows.OfType<FilePreviewWindow>().SingleOrDefault();return viewer?.PreviewStatus=="Image";},"changed WEBP format");
                        var bytes=File.ReadAllBytes(viewer!.CurrentPath);Check(FeatureCatalog.Extension(viewer.CurrentPath)=="webp","Show Result refreshes the chosen target format");viewer.Close();viewer=null;await Until(()=>button!.IsEnabled,"changed result close");
                        var accepted=await editor.ExportConversionAsync(Path.Combine(output,"accepted"),null,CancellationToken.None);
                        Check(accepted.Count==2&&bytes.SequenceEqual(File.ReadAllBytes(accepted[0])),"Save uses the same conversion settings and bytes as the refreshed batch preview");
                    }
                }
                finally{viewer?.Close();editor.Close();}
            }
            if(office.Executable is not null&&string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOKUM_CONVERSION_FORMATS")))Check(seen.ToHashSet().SetEquals(FeatureCatalog.Conversions.Values.SelectMany(v=>v)),"Every supported target format has a working result preview");
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks,formats=seen.Distinct().Order().ToArray(),officeAvailable=office.Executable is not null,scope="Isolated WPF controls and real conversions; no global input or user Library writes"},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{main.Close();}
    }
}
