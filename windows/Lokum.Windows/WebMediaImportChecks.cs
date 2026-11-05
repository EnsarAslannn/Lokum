using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Lokum.Core;
namespace Lokum.Windows;

internal static class WebMediaImportChecks
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
        var library=new LibraryStore(Path.Combine(output,"state"));var downloads=Path.Combine(output,"downloads");Directory.CreateDirectory(downloads);
        new PreferencesStore(library.DirectoryPath).Save(new(){Enabled=false,AutomaticUpdateChecks=false,KeepInTray=false,Feedback=false,Animations=false,DownloadFolder=downloads});
        var owner=new MainWindow(library:library,checkForUpdates:false,offerToolSetup:false);owner.Show();
        try
        {
            foreach(var fixture in new[]{(Url:"https://www.w3.org/Icons/w3c_home.png",Kind:"image",Tool:"editImage"),(Url:Environment.GetEnvironmentVariable("LOKUM_WEB_VIDEO_FIXTURE")??"https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4",Kind:"video",Tool:"muteVideo")})
            {
                Task? opening=null;var source=MediaToolSource.Address(fixture.Url,fixture.Kind)!;var open=typeof(MainWindow).GetMethod("OpenMediaToolAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
                _ = owner.Dispatcher.BeginInvoke(()=>opening=(Task)open.Invoke(owner,[source,new ToolChoice($"web:{fixture.Kind}:{fixture.Tool}","Tools","✦")])!);
                ToolEditor? editor=null;var limit=DateTime.UtcNow.AddSeconds(90);
                while(editor is null&&DateTime.UtcNow<limit){await Task.Delay(50);editor=Application.Current.Windows.OfType<ToolEditor>().FirstOrDefault();}
                Check(editor is not null,"The real downloaded "+fixture.Kind+" opens the selected tool before any edit is exported");
                var imported=owner.SelectedFiles.Single();var hash=SHA256.HashData(File.ReadAllBytes(imported));
                Check(File.Exists(imported)&&imported.StartsWith(Path.Combine(library.DirectoryPath,"Imports","Web"),StringComparison.OrdinalIgnoreCase),"The "+fixture.Kind+" source is kept in Lokum imports for repeated editing");
                if(fixture.Kind=="image")
                {
                    var preview=typeof(ToolEditor).GetField("photoPreviewValid",BindingFlags.Instance|BindingFlags.NonPublic)!;var ready=DateTime.UtcNow.AddSeconds(15);
                    while(!(bool)preview.GetValue(editor)!&&DateTime.UtcNow<ready)await Task.Delay(30);
                    Check((bool)preview.GetValue(editor)!,"The downloaded image has a rendered photo preview before Save");
                    typeof(ToolEditor).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,null);
                    if(opening is not null)await opening;
                    Check(library.Entries.Count==1&&library.Entries[0].Paths.All(path=>Path.GetDirectoryName(path)==downloads),"A saved web-image edit appears in the configured Downloads folder instead of the internal import cache");
                }
                else{editor!.Close();if(opening is not null)await opening;}
                Check(SHA256.HashData(File.ReadAllBytes(imported)).SequenceEqual(hash),"Editing/cancelling preserves the downloaded original "+fixture.Kind);
            }
            File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{passed=true,checks},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{foreach(var editor in Application.Current.Windows.OfType<ToolEditor>().ToArray())editor.Close();owner.Close();}
    }
}
