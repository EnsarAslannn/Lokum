using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Lokum.Windows;

internal static class PhotoPreviewThreadChecks
{
    internal static async Task Run(Window owner,string input,Action<bool,string> check)
    {
        var mutable=new RenderTargetBitmap(16,16,96,96,PixelFormats.Pbgra32);
        void Paint(Brush brush){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(brush,null,new Rect(0,0,16,16));mutable.Render(visual);}
        Paint(Brushes.Blue);if(mutable.IsFrozen)throw new Exception("The mutable preview fixture was unexpectedly frozen");
        var editor=new ToolEditor(owner,"redactImage","Redact Image",input,preview:mutable);editor.Show();editor.UpdateLayout();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            var timer=(System.Windows.Threading.DispatcherTimer)typeof(ToolEditor).GetField("photoPreview",flags)!.GetValue(editor)!;timer.Stop();
            ((List<Rect>)typeof(ToolEditor).GetField("photoRegions",flags)!.GetValue(editor)!).Add(new Rect(4,4,8,8));
            Paint(Brushes.Red);
            await (Task)typeof(ToolEditor).GetMethod("PreviewPhoto",flags)!.Invoke(editor,null)!;
            var canvas=(Canvas)typeof(ToolEditor).GetField("drawing",flags)!.GetValue(editor)!;
            var preview=(BitmapSource)((Image)canvas.Children[0]).Source;
            check(preview.IsFrozen,"Redaction previews accept a mutable UI bitmap and complete safely on the worker thread");
            var pixel=new byte[4];new FormatConvertedBitmap(preview,PixelFormats.Bgra32,null,0).CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
            check(pixel[0]==255&&pixel[2]==0&&!mutable.IsFrozen,"The editor owns a stable bitmap snapshot without freezing or changing the caller's image");
        }
        finally{editor.Close();}
    }
}
