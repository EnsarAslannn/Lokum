using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;
namespace Lokum.Windows;

internal static class ContextMediaChecks
{
    internal static void Native(string output,bool browserOnly=false)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();
        using var trace=new System.Diagnostics.TextWriterTraceListener(Path.Combine(output,"context-trace.log"));System.Diagnostics.Trace.Listeners.Add(trace);System.Diagnostics.Trace.AutoFlush=true;
        var source=Path.Combine(output,"Native source.png");var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)200,64*64*4).ToArray(),64*4);ImageEngine.Save(bitmap,source,"png");
        var library=new LibraryStore(Path.Combine(output,"state"));new PreferencesStore(library.DirectoryPath).Save(new(){Enabled=false,AutomaticUpdateChecks=false,KeepInTray=false,Feedback=false,Animations=false});
        var owner=new MainWindow(library:library,checkForUpdates:false,offerToolSetup:false);owner.Show();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);File.WriteAllText(Path.Combine(output,"progress.txt"),message);}
        try{if(browserOnly)BrowserMediaGestureChecks.Run(owner,output,source,Check);else GlobalGestureChecks.Run(owner,output,source,Check);}
        finally{owner.Close();}
        File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks},new JsonSerializerOptions{WriteIndented=true}));
    }
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);File.WriteAllText(Path.Combine(output,"progress.txt"),message);}
        var wheel=new GlobalDropWheel("tools",1,(_,_)=>{},()=>null);
        try
        {
            var choices=typeof(GlobalDropWheel).GetMethod("Choices",BindingFlags.Instance|BindingFlags.NonPublic)!;
            var image=(IReadOnlyList<ToolChoice>)choices.Invoke(wheel,[Array.Empty<string>(),new WebMediaPayload("https://cdn.example/photo.png","image")])!;
            Check(image.Any(c=>c.Id.EndsWith(":editImage",StringComparison.Ordinal))&&image.Any(c=>c.Id.EndsWith(":removeBackground",StringComparison.Ordinal))&&!image.Any(c=>c.Id.StartsWith("download:",StringComparison.Ordinal)),"A web image offers image editing/background tools instead of a download-only wheel");
            var video=(IReadOnlyList<ToolChoice>)choices.Invoke(wheel,[Array.Empty<string>(),new WebMediaPayload("https://cdn.example/clip.mp4","media")])!;
            Check(video.Any(c=>c.Id.EndsWith(":trimVideo",StringComparison.Ordinal))&&video.Any(c=>c.Id.EndsWith(":muteVideo",StringComparison.Ordinal))&&!video.Any(c=>c.Id.EndsWith(":editImage",StringComparison.Ordinal)),"A web video offers cutting/muting tools and excludes image tools");
        }
        finally{wheel.Close();}
        var imagePath=Path.Combine(output,"Screenshot test.png");var videoPath=Path.Combine(output,"Video test.mp4");
        var bitmap=BitmapSource.Create(3,2,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)200,24).ToArray(),12);ImageEngine.Save(bitmap,imagePath,"png");File.WriteAllBytes(videoPath,[0,1,2]);
        var local=MediaToolSource.Local([imagePath])!;
        Check(local.Choices.Any(c=>c.Id=="editImage")&&!local.Choices.Any(c=>c.Id.StartsWith("web:",StringComparison.Ordinal)),"Local image tools operate directly on the real file without a web download");
        Check(MediaToolSource.Local([videoPath])!.Choices.Any(c=>c.Id=="muteVideo"),"Local video tools include muting");
        Check(MediaToolSource.Local([Path.Combine(output,"Missing.png")]) is null,"A missing file does not open an unrelated wheel");
        Check(MediaToolSource.Address(new Uri(imagePath).AbsoluteUri,"image")!.Paths.Single()==Path.GetFullPath(imagePath),"An image opened in a browser from disk resolves to its real local path");
        var html=new DataObject();html.SetData(DataFormats.Html,"<a href='https://www.youtube.com/watch?v=mXS5W7_kdzk'><img src='https://i.ytimg.com/vi/mXS5W7_kdzk/hqdefault.jpg'></a>");
        Check(MediaToolSource.FromDrop(html) is {Kind:"image",Web.Url:"https://i.ytimg.com/vi/mXS5W7_kdzk/hqdefault.jpg"},"An image inside a video hyperlink edits the actual image instead of downloading the linked video");
        html.SetData(DataFormats.Html,"<video src='https://cdn.example/stream?token=fixture'></video>");
        Check(MediaToolSource.FromDrop(html) is {Kind:"video",Web.Url:"https://cdn.example/stream?token=fixture"},"A video without a filename extension retains its video tools and URL parameters");
        html.SetData(DataFormats.FileDrop,new[]{imagePath});
        Check(MediaToolSource.FromDrop(html) is {Web:null},"A real file-drop payload takes precedence over a misleading HTML link");
        Check(MediaToolSource.TryTool("web:image:removeBackground",out var kind,out var tool)&&kind=="image"&&tool=="removeBackground"&&!MediaToolSource.TryTool("web:image:muteVideo",out _,out _),"Only tools valid for the selected media kind can be dispatched");
        Check(MediaToolSource.Address("blob:https://example.com/session","video") is null&&MediaToolSource.Address("javascript:alert(1)","image") is null,"Unexposed browser blobs and script links are not treated as downloadable media");
        Check(BrowserContextMedia.KnownVideoAddress("www.youtube.com/watch?v=aqz-KE-bpKQ") is {Kind:"video",Web.Url:"https://www.youtube.com/watch?v=aqz-KE-bpKQ"},"A public YouTube video page can resolve a browser-internal video address");
        Check(BrowserContextMedia.KnownVideoAddress("https://example.com/page") is null&&BrowserContextMedia.KnownVideoAddress("https://www.instagram.com/p/PhotoOnly/") is null,"An unrelated page or photo post is never guessed to be the clicked video");
        foreach(var name in new[]{"Copy image address","Resim adresini kopyala","Resim Bağlantısını Kopyala"})Check(BrowserContextMedia.CommandKind(name)=="image","Image context command: "+name);
        foreach(var name in new[]{"Copy video address","Copy video URL","Video bağlantısını kopyala"})Check(BrowserContextMedia.CommandKind(name)=="video","Video context command: "+name);
        Check(BrowserContextMedia.CommandKind("Copy link address") is null&&BrowserContextMedia.CommandKind("Copy image") is null,"Ordinary hyperlinks and image-bitmap copy commands cannot replace the actual media address");
        var clipboard=new DataObject();clipboard.SetData(DataFormats.UnicodeText,"Original text");clipboard.SetData(DataFormats.Html,"<b>Original text</b>");clipboard.SetData(DataFormats.FileDrop,new[]{imagePath});var stream=new MemoryStream(new byte[]{1,2,3});stream.Position=1;clipboard.SetData("Fixture stream",stream);
        var snapshot=ClipboardSnapshot.Capture(clipboard);stream.Position=0;stream.WriteByte(9);stream.Position=1;
        var restored=(IDataObject)typeof(ClipboardSnapshot).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(snapshot)!;
        Check((string)restored.GetData(DataFormats.UnicodeText)=="Original text"&&(string)restored.GetData(DataFormats.Html)=="<b>Original text</b>"&&((string[])restored.GetData(DataFormats.FileDrop)).Single()==imagePath,"The clipboard snapshot preserves text, HTML and file formats");
        Check(((MemoryStream)restored.GetData("Fixture stream")).ToArray()[0]==1&&stream.Position==1,"Clipboard streams are copied independently without changing the source position");
        var library=new LibraryStore(Path.Combine(output,"state"));new PreferencesStore(library.DirectoryPath).Save(new(){Enabled=false,AutomaticUpdateChecks=false,KeepInTray=false,Feedback=false,Animations=false});
        var owner=new MainWindow(library:library,checkForUpdates:false,offerToolSetup:false);owner.Show();
        try
        {
            var open=typeof(MainWindow).GetMethod("OpenMediaToolAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;Task? editing=null;
            _ = owner.Dispatcher.BeginInvoke(()=>editing=(Task)open.Invoke(owner,[local,new ToolChoice("editImage","Edit Image","✦")])!);
            var end=DateTime.UtcNow.AddSeconds(15);ToolEditor? editor=null;
            while(editor is null&&DateTime.UtcNow<end){await Task.Delay(30);editor=Application.Current.Windows.OfType<ToolEditor>().FirstOrDefault();}
            Check(editor is not null&&owner.SelectedFiles.Single()==imagePath,"Choosing an image tool opens the real image editor with the clicked file");
            editor!.Close();if(editing is not null)await editing;
            Check(File.Exists(imagePath),"Cancelling a context-launched editor keeps the original file");
        }
        finally{owner.Close();}
        if(Environment.GetEnvironmentVariable("LOKUM_CONTEXT_CLIPBOARD_TESTS")=="1")
        {
        var menuOwner=new Window{Width=360,Height=200,Content=new TextBlock{Text="Lokum context menu fixture"}};menuOwner.Show();
        var savedClipboard=await ClipboardSnapshot.CaptureAsync(CancellationToken.None);var marker="Lokum menu clipboard fixture "+Guid.NewGuid().ToString("N");Clipboard.SetText(marker);
        try
        {
            foreach(var fixture in new[]{(Label:"Copy image address",Address:"https://cdn.example/image.png",Kind:"image"),(Label:"Video bağlantısını kopyala",Address:"https://cdn.example/video.mp4",Kind:"video")})
            {
                var menu=new ContextMenu{PlacementTarget=menuOwner.Content as UIElement};var link=new MenuItem{Header="Copy link address"};link.Click+=(_,_)=>Clipboard.SetText("https://wrong.example/page");menu.Items.Add(link);
                var media=new MenuItem{Header=fixture.Label};media.Click+=(_,_)=>Clipboard.SetText(fixture.Address);menu.Items.Add(media);menu.IsOpen=true;
                try
                {
                    await Task.Delay(150);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));var result=await BrowserContextMedia.ReadAsync(Environment.ProcessId,timeout.Token);
                    Check(result?.Kind==fixture.Kind&&result.Web?.Url==fixture.Address,"Native "+fixture.Kind+" context menu invokes the media-address command rather than the ordinary hyperlink");
                    Check(Clipboard.GetText()==marker,"Native "+fixture.Kind+" address capture restores the pre-existing clipboard after copying");
                }
                finally{menu.IsOpen=false;}
            }
        }
        finally{if(Clipboard.ContainsText()&&Clipboard.GetText()==marker)savedClipboard.Restore();menuOwner.Close();}
        }
        File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks},new JsonSerializerOptions{WriteIndented=true}));
    }
}
