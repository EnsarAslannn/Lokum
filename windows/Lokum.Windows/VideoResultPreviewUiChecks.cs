using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;

namespace Lokum.Windows;

internal static class VideoResultPreviewUiChecks
{
    private static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T item)yield return item;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    internal static async Task Run(Window owner,string source,string output,Toolchain tools,Action<bool,string> check)
    {
        var engine=new MediaEngine(tools.Executable("ffmpeg"),tools.Executable("ffprobe"));var info=await engine.InspectAsync(source);
        var inputs=new Dictionary<string,string>{{"mp4",source}};
        foreach(var extension in new[]{"mov","mkv"})
        {
            var variant=Path.Combine(output,"preview-source."+extension);
            await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-i",source,"-map","0","-c","copy",variant]);inputs[extension]=variant;
        }
        var frame=Path.Combine(output,"preview-source.png");await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-i",source,"-frames:v","1",frame]);var bitmap=ImageEngine.Load(frame);
        foreach(var entry in new[]{(Tool:"compress",Format:"mp4"),(Tool:"changeVideoSpeed",Format:"mp4"),(Tool:"cropVideo",Format:"mp4"),(Tool:"redactVideo",Format:"mp4"),(Tool:"changeVideoSpeed",Format:"mov"),(Tool:"cropVideo",Format:"mkv")})
        {
            var tool=entry.Tool;var mediaInput=inputs[entry.Format];var sourceHash=SHA256.HashData(File.ReadAllBytes(mediaInput));
            var editor=new ToolEditor(owner,tool,FeatureCatalog.ToolNames[tool],mediaInput,info,tool is "cropVideo" or "redactVideo"?bitmap:null){Width=800,Height=600};editor.ConfigureMediaRuntime(tools);
            try
            {
                editor.Show();editor.UpdateLayout();
                var show=Find<Button>(editor).SingleOrDefault(b=>AutomationProperties.GetAutomationId(b)=="media.preview.processed");
                check(show is not null,$"{tool}: a result-preview action is available before saving");
                check(Equals(show!.Content,L10n.Text("Show Result")),tool+": the result action has the same localized label");
                var rect=show.TransformToAncestor(editor).TransformBounds(new Rect(show.RenderSize));
                check(show.IsVisible&&rect.Top>=0&&rect.Bottom<=editor.ActualHeight&&rect.Right<=editor.ActualWidth,tool+": the result action fits the compact window");
                DependencyObject? parent=show;while(parent is not null&&parent is not ScrollViewer)parent=VisualTreeHelper.GetParent(parent);
                check(parent is null,tool+": result preview stays visible outside scrolling editor options");
                Configure(false);
                await Preview();var first=editor.ProcessedPreviewPath!;var firstInfo=await engine.InspectAsync(first);
                check(editor.ProcessedPlayback,tool+": result action switches the player to the processed video");
                if(tool=="changeVideoSpeed")check(Math.Abs(firstInfo.Duration-3)<.2,"Speed preview renders the selected 2x speed");
                if(tool=="cropVideo")check(firstInfo.Video is {Width:80,Height:48},"Crop preview shows the actual cropped dimensions");
                if(tool=="compress")check(new FileInfo(first).Length<=info.Bytes*.8,"Compression preview plays the actual size-limited export without a second compression");
                var saved=(await engine.ToolAsync(tool,[mediaInput],output,editor.MediaOptions))[0];
                var previewPixels=await Frame(first,tool+"-"+entry.Format+"-preview");var savedPixels=await Frame(saved,tool+"-"+entry.Format+"-saved");
                check(previewPixels.SequenceEqual(savedPixels),tool+": preview pixels match the saved result exactly");
                Capture(tool+"-"+entry.Format+"-result");
                Configure(true);
                check(!editor.ProcessedPlayback&&editor.ProcessedPreviewPath is null,tool+": changing settings invalidates the previous result without automatic rendering");
                await Preview();check(editor.ProcessedPreviewPath!=first,tool+": the result action refreshes the changed settings");
                var refreshed=await engine.InspectAsync(editor.ProcessedPreviewPath!);
                if(tool=="changeVideoSpeed")check(Math.Abs(refreshed.Duration-2)<.2,"Refreshed speed preview uses 3x instead of the previous 2x");
                if(tool=="cropVideo")check(refreshed.Video is {Width:64,Height:48},"Refreshed crop preview uses the newly selected area");
                var original=Find<Button>(editor).Single(b=>AutomationProperties.GetAutomationId(b)=="media.preview.original");original.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(!editor.ProcessedPlayback&&Find<Canvas>(editor).All(c=>c.IsEnabled),tool+": Original returns to the editable source");
                check(sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(mediaInput))),tool+": preview and export preserve the source file");

                void Configure(bool changed)
                {
                    if(tool=="compress")editor.FieldForTest("Reduction (%; 0 = auto)").Text=changed?"30":"20";
                    if(tool=="changeVideoSpeed")editor.FieldForTest("Speed").Text=changed?"3":"2";
                    if(tool=="cropVideo"){editor.FieldForTest("X").Text="16";editor.FieldForTest("Y").Text="8";editor.FieldForTest("Width").Text=changed?"64":"80";editor.FieldForTest("Height").Text="48";}
                    if(tool=="redactVideo"){editor.FieldForTest("Width").Text="96";editor.FieldForTest("Height").Text="64";Find<ComboBox>(editor).Single(c=>c.Items.Contains("blur")).SelectedItem=changed?"pixelate":"blur";}
                }
                async Task Preview()
                {
                    show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var deadline=DateTime.UtcNow.AddSeconds(90);
                    while(editor.ProcessedPreviewPath is null){if(DateTime.UtcNow>deadline)throw new Exception(tool+": processed preview did not finish");await Task.Delay(50);}
                }
                async Task<byte[]> Frame(string path,string name)
                {
                    var target=Path.Combine(output,name+".png");await ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-y","-ss","0.7","-i",path,"-frames:v","1",target]);
                    var image=new FormatConvertedBitmap(ImageEngine.Load(target),PixelFormats.Bgra32,null,0);var data=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(data,image.PixelWidth*4,0);return data;
                }
                void Capture(string name)
                {
                    editor.UpdateLayout();var capture=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);capture.Render(editor);ImageEngine.Save(capture,Path.Combine(output,name+".png"),"png");
                }
            }
            finally{editor.Close();}
        }
    }
}
