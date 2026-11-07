using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lokum.Windows;

internal static class ImageWorkspaceUiChecks
{
    internal static void Menu(MainWindow owner)
    {
        var tile=Descendants<Button>(owner).Single(b=>b.Content is DependencyObject content&&Descendants<TextBlock>(content).Any(t=>t.Text==L10n.Text("Fine-tune")));
        Click(tile);
        ContextMenu? menu=null;
        try
        {
            Check(Wait(()=>
            {
                menu=PresentationSource.CurrentSources.OfType<System.Windows.Interop.HwndSource>().Where(s=>s.RootVisual is not null).SelectMany(s=>Descendants<ContextMenu>(s.RootVisual)).FirstOrDefault(m=>m.IsOpen&&ReferenceEquals(m.PlacementTarget,tile));
                return menu is not null;
            }),"The image tools menu did not open from its actual tile.");
            var names=menu!.Items.OfType<MenuItem>().Select(i=>i.Header as string).ToArray();
            Check(names.Contains(L10n.Text("Edit Background"))&&names.Contains(L10n.Text("Edit Image"))&&names.Contains(L10n.Text("Redact Image"))&&names.Contains(L10n.Text("Read QR Code")),"The image tools menu does not show the localized consolidated names used by the wheel.");
            Check(names.Length==7,"The image menu retained duplicate crop/background entries.");
        }
        finally{if(menu is not null)menu.IsOpen=false;}
    }
    internal static void Verify(ToolEditor editor,BitmapSource source,string input,string output,bool cropped)
    {
        var region=new Rect(100d/640,80d/400,200d/640,170d/400);
        var settings=new ImageOptions{Width=640,Height=400,NormalizedCrop=region};
        var hash=SHA256.HashData(File.ReadAllBytes(input));
        var engineResult=ImageEngine.TransformPhoto(source,settings);
        Check(engineResult.PixelWidth==200&&engineResult.PixelHeight==170,"Image editing ignored its crop or used the resized preview's coordinates for export.");
        var expected=new CroppedBitmap(source,new(100,80,200,170));
        Check(Pixels(engineResult).SequenceEqual(Pixels(expected)),"Cropped pixels differ from the requested source area.");
        var resized=ImageEngine.TransformPhoto(source,settings with{Width=320,Height=200});
        Check(resized.PixelWidth==100&&resized.PixelHeight==85,"Normalized crop did not scale with image resizing.");
        var rotated=ImageEngine.TransformPhoto(source,new(){Rotation=90,NormalizedCrop=new(.25,.25,.5,.5)});
        Check(rotated.PixelWidth==200&&rotated.PixelHeight==320,"Crop was applied in the wrong coordinate space after rotation.");
        var rejected=false;try{ImageEngine.TransformPhoto(source,settings with{NormalizedCrop=new(-.1,0,.5,.5)});}catch(ArgumentException){rejected=true;}
        Check(rejected,"An out-of-bounds photo crop was accepted.");

        var crop=Find<Button>(editor,"PhotoCropStart");var apply=Find<Button>(editor,"PhotoCropPreview");
        var remove=Find<Button>(editor,"PhotoCropRemove");var image=Descendants<Canvas>(editor).Single(c=>c.Children.Count>0&&c.Children[0] is Image);
        BitmapSource Result()=>(BitmapSource)((Image)image.Children[0]).Source;
        Check(Wait(()=>editor.UIAuditPreviewReady),"Initial image editor preview did not finish.");
        Click(crop);Check(Wait(()=>editor.UIAuditPreviewReady&&apply.IsEnabled),"Crop selection did not become available inside the image editor.");
        Check(!(bool)typeof(ToolEditor).GetMethod("BeginCropGesture",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,[new Point(100,80)])!,"A fresh crop starts with a full-canvas move gesture, preventing a new selection.");
        Check(!Descendants<Button>(editor).Single(b=>Equals(b.Content,L10n.Text("Save new copy  ↗"))).IsEnabled,"Saving must wait until the selected crop has been previewed.");
        typeof(ToolEditor).GetMethod("SetCrop",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,[new Rect(100,80,200,170)]);
        Click(apply);
        Check(Wait(()=>editor.UIAuditPreviewReady&&Result().PixelWidth==200&&Result().PixelHeight==170),"Preview crop did not display the cropped image before saving.");
        Read(editor);Check(editor.ImageOptions.NormalizedCrop==region,"The photo editor did not forward its selected crop to export.");
        var file=ImageEngine.Apply("editImage",[input],output,editor.ImageOptions,"png").Single();
        Check(Pixels(ImageEngine.Load(file)).SequenceEqual(Pixels(Result())),"The saved image differs from the cropped preview.");
        Click(crop);Check(Wait(()=>editor.UIAuditPreviewReady&&Result().PixelWidth==640),"Re-editing a crop did not restore the full selection canvas.");
        Check(image.Children.OfType<System.Windows.Shapes.Rectangle>().Any(r=>r.Width==200&&r.Height==170),"Re-editing lost the previous crop selection.");
        Click(apply);Check(Wait(()=>editor.UIAuditPreviewReady&&Result().PixelWidth==200),"Re-applying a crop cut the already cropped preview a second time.");
        if(!cropped)
        {
            editor.FieldForTest("Brightness").Text="0.1";Check(Wait(()=>editor.UIAuditPreviewReady),"Photo adjustment after cropping did not finish.");
            Click(remove);Check(Wait(()=>editor.UIAuditPreviewReady&&Result().PixelWidth==640),"Removing the crop did not restore the complete image.");
            Read(editor);Check(editor.ImageOptions.NormalizedCrop is null&&editor.ImageOptions.Adjustments.Brightness==.1,"Removing a crop erased other image adjustments.");
        }
        Check(SHA256.HashData(File.ReadAllBytes(input)).SequenceEqual(hash),"The combined image editor changed its original file.");
    }
    internal static void Palette(BackgroundRemovalWindow editor)
    {
        var hue=Find<Slider>(editor,"BackgroundHue");
        hue.Value=120;
        var surface=Descendants<FrameworkElement>(editor).Single(e=>AutomationProperties.GetAutomationId(e)=="BackgroundColorSpectrum");
        surface.GetType().GetMethod("ChooseNormalized",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(surface,[.5,.8]);
        var color=Find<TextBox>(editor,"BackgroundColor");
        Check(color.Text=="#66CC66","Selecting the color spectrum did not update the actual background color.");
        Check(Wait(()=>editor.PreviewReady),"The palette selection did not refresh the background preview.");
        var bitmap=(BitmapSource)Find<Image>(editor,"BackgroundResult").Source;
        var pixel=new byte[4];new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0).CopyPixels(new Int32Rect(10,10,1,1),pixel,4,0);
        Check(pixel.SequenceEqual(new byte[]{102,204,102,255}),"The visible palette color does not match the composed image.");
        color.Text="#235FA8";Check(Wait(()=>editor.PreviewReady),"Typing a hex color could not update the picker and preview together.");
    }
    private static void Read(ToolEditor editor)=>typeof(ToolEditor).GetMethod("ReadSettings",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(editor,[false]);
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    private static byte[] Pixels(BitmapSource image){var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var data=new byte[image.PixelWidth*image.PixelHeight*4];converted.CopyPixels(data,image.PixelWidth*4,0);return data;}
    private static T Find<T>(DependencyObject root,string id)where T:DependencyObject=>Descendants<T>(root).Single(e=>AutomationProperties.GetAutomationId(e)==id);
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var item=VisualTreeHelper.GetChild(root,i);if(item is T match)yield return match;foreach(var nested in Descendants<T>(item))yield return nested;}}
    private static bool Wait(Func<bool> condition){var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(15)){if(condition())return true;var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}return condition();}
}
