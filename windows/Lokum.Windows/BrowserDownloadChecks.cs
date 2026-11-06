using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;
namespace Lokum.Windows;

internal static class BrowserDownloadChecks
{
    internal static async Task Run(string output)
    {
        output=Path.GetFullPath(output);Directory.CreateDirectory(output);var checks=new List<string>();void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
        ContextClickGestureChecks.Run(Check);
        await ContextClickGestureChecks.DelayedExplorerHint(Check);
        await ContextClickGestureChecks.ExplorerRowBounds(Check);
        ContextClickGestureChecks.OwnedPopup(Check);
        var library=new LibraryStore(Path.Combine(output,"state"));var folder=Path.Combine(output,"downloads");Directory.CreateDirectory(folder);
        new PreferencesStore(library.DirectoryPath).Save(new(){Enabled=false,AutomaticUpdateChecks=false,KeepInTray=false,Feedback=false,Animations=false,DownloadFolder=folder,OutputMode="ask"});
        var owner=new MainWindow(library:library,checkForUpdates:false,offerToolSetup:false);owner.Show();
        try
        {
            var original=Path.Combine(output,"Source.png");var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)200,16).ToArray(),8);ImageEngine.Save(bitmap,original,"png");
            var source=Environment.GetEnvironmentVariable("LOKUM_BROWSER_DOWNLOAD_WEB")=="1"?MediaToolSource.Address("https://www.w3.org/Icons/w3c_home.png","image")!:MediaToolSource.Local([original])!;
            var open=typeof(MainWindow).GetMethod("OpenMediaToolAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
            await (Task)open.Invoke(owner,[source,new ToolChoice("download:media","Download Media","↓")])!;
            Check(library.Entries.Count==1&&library.Entries[0].Paths.Length==1,"Download Media saves an image and records it as a download");
            var image=library.Entries[0].Paths[0];
            Check(File.Exists(image)&&Path.GetDirectoryName(image)==folder,"The shortcut uses the configured Downloads folder even when normal exports ask for a folder");
            Check(owner.SelectedFiles.SequenceEqual(new[]{image})&&FeatureCatalog.WheelTools(owner.SelectedFiles).Contains("editImage"),"The downloaded image becomes a normal local selection with image tools");
            Check(!Application.Current.Windows.OfType<ToolEditor>().Any(),"Downloading does not open an editor or require choosing an editing action");
            if(Environment.GetEnvironmentVariable("LOKUM_BROWSER_DOWNLOAD_VIDEO") is {} url)
            {
                await (Task)open.Invoke(owner,[MediaToolSource.Address(url,"video")!,new ToolChoice("download:media","Download Media","↓")])!;
                Check(library.Entries.Count==2&&owner.SelectedFiles.All(File.Exists),"A real public video downloads directly into the local Library");
                Check(FeatureCatalog.WheelTools(owner.SelectedFiles).Contains("muteVideo")&&!Application.Current.Windows.OfType<ToolEditor>().Any(),"The downloaded video offers local video tools without automatically editing it");
            }
            var language=L10n.Language;L10n.Language="tr";
            try
            {
                var invoked=0;var menu=new BrowserDownloadMenu(source,()=>invoked++);menu.Show();menu.UpdateLayout();
                Button? Find(DependencyObject node){if(node is Button b)return b;for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)if(Find(VisualTreeHelper.GetChild(node,i)) is {} child)return child;return null;}
                try
                {
                    var button=Find(menu);Check(button is not null&&AutomationProperties.GetAutomationId(button)=="web.download.media"&&button.Content?.ToString()=="Medyayı indir","The native web menu presents a localized Download Media action");
                    var capture=new RenderTargetBitmap((int)menu.ActualWidth,(int)menu.ActualHeight,96,96,PixelFormats.Pbgra32);capture.Render(menu);ImageEngine.Save(capture,Path.Combine(output,"download-menu.png"),"png");
                    button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(invoked==1&&!menu.IsVisible,"Choosing Download Media closes the popup and invokes the download exactly once");
                }
                finally{menu.Close();}
            }
            finally{L10n.Language=language;}
            owner.LinkField.Text="https://previous.example/unrelated";
            typeof(MainWindow).GetMethod("OpenUnresolvedDownload",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            Check(owner.DownloadWindow!=owner&&owner.LinkField.Text=="","An unexposed media address opens a fresh download dialog instead of reusing an unrelated previous link");
            Check(!Application.Current.Windows.OfType<ToolEditor>().Any(),"The fallback asks for a media link without guessing an editing action");owner.DownloadWindow.Close();
        }
        finally{foreach(var toast in Application.Current.Windows.OfType<ToastWindow>().ToArray())toast.Close();owner.Close();}
        File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks},new JsonSerializerOptions{WriteIndented=true}));
    }
}
