using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Lokum.Windows;

public sealed partial class MainWindow
{
    internal IReadOnlyList<ToolChoice> UIAuditToolChoices => Choices(true);
}

public sealed partial class ToolEditor
{
    internal bool UIAuditPreviewReady
    {
        get
        {
            if (bitmap is null || drawing.Children.Count == 0 || drawing.Children[0] is not Image { Source: BitmapSource source }) return bitmap is null;
            if(tool=="editImage")return photoPreviewValid&&photoPreview is {IsEnabled:false}&&previewGate.CurrentCount==1;
            if (tool != "frameImage") return true;
            return photoPreview is { IsEnabled: false } && previewGate.CurrentCount == 1 &&
                source.PixelWidth == (int)Value("Canvas width", bitmap.PixelWidth + 160) &&
                source.PixelHeight == (int)Value("Canvas height", bitmap.PixelHeight + 160);
        }
    }
}
