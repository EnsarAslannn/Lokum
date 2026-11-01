using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lokum.Core;

namespace Lokum.Windows;

internal static class BackgroundEditorChecks
{
    internal static void Initial(BackgroundRemovalWindow editor)
    {
        Check(!Find<Button>(editor,"SaveBackgroundEdit").IsEnabled,"The editor must wait for a kept area or an explicit automatic selection before exporting.");
        Check(!Find<ComboBox>(editor,"BackgroundFillType").IsEnabled,"Replacement backgrounds must follow foreground selection.");
    }

    internal static void Prepare(BackgroundRemovalWindow editor,string scene,string input,string output)
    {
        Initial(editor);
        var canvas=Find<ForegroundSelectionSurface>(editor,"ForegroundSelectionCanvas");
        var tool=Find<ComboBox>(editor,"ForegroundSelectionTool");
        var fill=Find<ComboBox>(editor,"BackgroundFillType");
        var save=Find<Button>(editor,"SaveBackgroundEdit");
        var color=Find<TextBox>(editor,"BackgroundColor");
        var hash=SHA256.HashData(File.ReadAllBytes(input));
        tool.SelectedIndex=1;Draw(canvas,new(100,80),new(300,250));
        Check(Wait(()=>editor.PreviewReady),"Manual selection did not produce a preview.");
        Check(canvas.Regions.Length==1&&Math.Abs(canvas.Regions[0].Points[0].X-100d/640)<.0001,"Fitted preview coordinates were not stored relative to the original image.");
        Check(Alpha(editor.PreparedImages[0].Png,150,120)==255&&Alpha(editor.PreparedImages[0].Png,10,10)==0,"The editor kept pixels outside the selected region.");
        Check(save.IsEnabled&&fill.IsEnabled,"A ready cutout did not enable export and replacement backgrounds.");
        if(scene=="editor.background-cutout")
        {
            canvas.BeginSelection(new(20,20));canvas.ContinueSelection(new(40,40));canvas.CancelSelection();
            Check(canvas.Regions.Length==1,"Cancelling a gesture changed the saved selection.");
            fill.SelectedIndex=1;color.Text="#oops";
            Check(Wait(()=>editor.EditorReady)&&!save.IsEnabled&&!editor.PreviewReady,"Invalid color must disable saving a stale preview.");
            color.Text="#235FA8";Check(Wait(()=>editor.PreviewReady),"Correcting the color did not recover the preview.");
            Check(Pixel(Result(editor),10,10).SequenceEqual(new byte[]{168,95,35,255}),"Solid background preview used the wrong color.");
            fill.SelectedIndex=0;Check(Wait(()=>editor.PreviewReady),"Returning to transparency lost the cutout.");
            FindButton(editor,"Clear selection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!save.IsEnabled&&!editor.PreviewReady,"Clearing selections retained an exportable stale result.");
            tool.SelectedIndex=0;canvas.BeginSelection(new(100,80));canvas.ContinueSelection(new(300,80));canvas.ContinueSelection(new(100,280));canvas.FinishSelection();
            Check(Wait(()=>editor.PreviewReady),"Freehand selection did not render.");
            Check(Alpha(editor.PreparedImages[0].Png,140,120)==255&&Alpha(editor.PreparedImages[0].Png,280,250)==0,"Freehand cutout kept the polygon's bounding box instead of its actual area.");
            var subtract=Descendants<CheckBox>(editor).Single();subtract.IsChecked=true;tool.SelectedIndex=1;
            Draw(canvas,new(120,100),new(160,140));Check(Wait(()=>editor.PreviewReady)&&Alpha(editor.PreparedImages[0].Png,140,120)==0,"Subtracting a selected region did not remove it.");
            FindButton(editor,"Undo selection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Wait(()=>editor.PreviewReady)&&Alpha(editor.PreparedImages[0].Png,140,120)==255,"Undo did not restore the removed area.");
            subtract.IsChecked=false;
        }
        if(scene=="editor.background-color")
        {
            fill.SelectedIndex=1;color.Text="#235FA8";Check(Wait(()=>editor.PreviewReady),"Solid background preview did not finish.");
            Check(Pixel(Result(editor),10,10).SequenceEqual(new byte[]{168,95,35,255}),"Solid background did not fill transparent pixels.");
        }
        if(scene=="editor.background-image")
        {
            var replacement=Path.Combine(output,"Replacement.png");
            var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[]{95,170,24,255,95,170,24,255,95,170,24,255,95,170,24,255},8);bitmap.Freeze();
            File.WriteAllBytes(replacement,ImageEngine.PngBytes(bitmap));
            var load=editor.UseBackgroundImageAsync(replacement);Check(Wait(()=>load.IsCompleted),"Background image did not load.");load.GetAwaiter().GetResult();
            Check(Wait(()=>editor.PreviewReady)&&Pixel(Result(editor),10,10).SequenceEqual(new byte[]{95,170,24,255}),"Replacement image did not cover the output canvas.");
            var failedLoad=editor.UseBackgroundImageAsync(Path.Combine(output,"missing-background.png"));
            Check(!save.IsEnabled&&!editor.PreviewReady,"Loading a replacement left the old result exportable.");
            Check(Wait(()=>failedLoad.IsCompleted),"Invalid replacement image did not finish reporting its error.");failedLoad.GetAwaiter().GetResult();
            Check(!save.IsEnabled&&!editor.PreviewReady,"A failed replacement image retained a stale ready result.");
            var retry=editor.UseBackgroundImageAsync(replacement);Check(Wait(()=>retry.IsCompleted),"Replacement retry did not finish.");retry.GetAwaiter().GetResult();
            Check(Wait(()=>editor.PreviewReady)&&save.IsEnabled,"A valid replacement could not recover after a loading error.");
        }
        var exported=BackgroundEditingEngine.Export(editor.PreparedImages,output,editor.FillOptions).Single();
        Check(Bytes(ImageEngine.Load(exported)).SequenceEqual(Bytes(Result(editor))),"Saved PNG differs from the actual background preview.");
        Check(SHA256.HashData(File.ReadAllBytes(input)).SequenceEqual(hash),"The background editor changed the source file.");
    }

    internal static void Batch(BackgroundRemovalWindow editor)
    {
        Initial(editor);var canvas=Find<ForegroundSelectionSurface>(editor,"ForegroundSelectionCanvas");
        var files=Find<ComboBox>(editor,"BackgroundInputFiles");var save=Find<Button>(editor,"SaveBackgroundEdit");
        Find<ComboBox>(editor,"ForegroundSelectionTool").SelectedIndex=1;
        Draw(canvas,new(100,80),new(300,250));Check(Wait(()=>editor.PreviewReady)&&!save.IsEnabled,"A partly edited batch was allowed to export unselected files.");
        files.SelectedIndex=1;FindButton(editor,"Select all").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Wait(()=>editor.PreviewReady)&&save.IsEnabled,"Selecting the second image did not complete the batch.");
        var prepared=editor.PreparedImages;
        Check(prepared.Length==2&&Alpha(prepared[0].Png,10,10)==0&&Alpha(prepared[1].Png,10,10)==255,"The first image's selection was reused for the batch.");
        var fill=Find<ComboBox>(editor,"BackgroundFillType");fill.SelectedIndex=2;
        FindButton(editor,"Clear selection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        FindButton(editor,"Select all").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Wait(()=>editor.EditorReady)&&fill.IsEnabled&&!save.IsEnabled,"A missing replacement image trapped a fresh cutout behind disabled background controls.");
        fill.SelectedIndex=0;Check(Wait(()=>editor.PreviewReady)&&save.IsEnabled,"Returning to transparent could not recover the batch.");
        files.SelectedIndex=0;Check(Wait(()=>editor.PreviewReady)&&canvas.Regions.Length==1,"Navigating back lost the saved foreground selection.");
    }

    private static void Draw(ForegroundSelectionSurface canvas,Point first,Point last){canvas.BeginSelection(first);canvas.ContinueSelection(last);canvas.FinishSelection();}
    private static Image Image(DependencyObject editor)=>Find<Image>(editor,"BackgroundResult");
    private static BitmapSource Result(DependencyObject editor)=>(BitmapSource)Image(editor).Source;
    private static byte[] Pixel(BitmapSource bitmap,int x,int y){var bytes=new byte[4];new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0).CopyPixels(new Int32Rect(x,y,1,1),bytes,4,0);return bytes;}
    private static byte Alpha(byte[] png,int x,int y)=>Pixel(ImageEngine.FromPng(png),x,y)[3];
    private static byte[] Bytes(BitmapSource image){var bytes=new byte[image.PixelWidth*image.PixelHeight*4];new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0).CopyPixels(bytes,image.PixelWidth*4,0);return bytes;}
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static Button FindButton(DependencyObject editor,string caption)=>Descendants<Button>(editor).Single(b=>Equals(b.Content,L10n.Text(caption)));
    private static T Find<T>(DependencyObject root,string id)where T:DependencyObject=>Descendants<T>(root).Single(x=>AutomationProperties.GetAutomationId(x)==id);
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private static bool Wait(Func<bool> condition)
    {
        var clock=Stopwatch.StartNew();while(clock.Elapsed<TimeSpan.FromSeconds(15))
        {
            if(condition())return true;var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
            timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
        }
        return condition();
    }
}
