using Lokum.Core;

namespace Lokum.Windows;

public sealed partial class MainWindow
{
    private async Task RemoveBackgroundAsync(string[] selected)
    {
        var preview=new BackgroundRemovalWindow(this,selected[0],selected.Length);
        if(preview.ShowDialog()!=true)return;
        await RunAsync((destination,progress,token)=>Task.Run(()=>
        {
            using var engine=new BackgroundRemovalEngine();
            return engine.Export(selected,destination,progress,token);
        },token),"Remove Background");
    }
}
