using Lokum.Core;

namespace Lokum.Windows;

public sealed partial class MainWindow
{
    private async Task RemoveBackgroundAsync(string[] selected,bool addBackground=false)
    {
        var preview=new BackgroundRemovalWindow(this,selected,addBackground);
        if(preview.ShowDialog()!=true)return;
        var prepared=preview.PreparedImages;var options=preview.FillOptions;
        await RunAsync((destination,progress,token)=>Task.Run(()=>
        {
            return BackgroundEditingEngine.Export(prepared,destination,options,progress,token);
        },token),"Edit Background");
    }
}
