using System.Windows;
using Lokum.Core;
namespace Lokum.Windows;

public sealed partial class MainWindow
{
    private ContextMediaController? mediaContext;
    private string BesideSourceDirectory()
    {
        var imported=Path.GetFullPath(Path.Combine(library.DirectoryPath,"Imports","Web"))+Path.DirectorySeparatorChar;
        return files.All(path=>Path.GetFullPath(path).StartsWith(imported,StringComparison.OrdinalIgnoreCase))?preferences.Value.DownloadFolder:Path.GetDirectoryName(files[0])!;
    }
    private async Task OpenMediaToolAsync(MediaToolSource source,ToolChoice choice)
    {
        if(busy||!DesktopGeometry.IsInteractive(this))return;
        var tool=choice.Id;if(MediaToolSource.TryTool(tool,out _,out var selected))tool=selected;
        if(!FeatureCatalog.WheelTools(source.CatalogPaths).Contains(tool))return;
        ShowMain();
        if(source.Web is not null)
        {
            if(source.Kind!="image"&&!tools.Ready){MessageBox.Show(this,L10n.Text("Choose Set up media tools once to enable web downloads."),L10n.Text("Media setup"));return;}
            var directory=Path.Combine(library.DirectoryPath,"Imports","Web",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            SetBusy(true);using var cancel=new CancellationTokenSource();var toast=new ToastWindow(this,cancel.Cancel);toast.Show();
            try
            {
                var kind=source.Web.Kind=="instagramPhotos"?"photos":source.Kind;
                var paths=await new WebDownload(tools).DownloadAsync(source.Web.Url,kind,directory,new Progress<OperationProgress>(toast.Update),cancel.Token);
                var imported=MediaToolSource.Local(paths)??throw new InvalidDataException("The downloaded file is not a supported image, video or audio file.");
                if(!FeatureCatalog.WheelTools(imported.Paths).Contains(tool))throw new InvalidDataException("This tool is not available for the downloaded media format.");
                source=imported;
            }
            catch(OperationCanceledException){Directory.Delete(directory,true);L10n.SetText(status,"Cancelled. Originals kept.");return;}
            catch(Exception error){Directory.Delete(directory,true);MessageBox.Show(this,L10n.Text(error.Message),L10n.Text("Couldn’t open this tool"));return;}
            finally{toast.Close();SetBusy(false);}
        }
        if(source.Paths.Any(path=>!File.Exists(path)))return;
        files.Clear();Add(source.Paths);ShowLibrary(false);
        await EditAsync(new(tool,FeatureCatalog.ToolNames[tool],ToolSymbol(tool)));
    }
}
