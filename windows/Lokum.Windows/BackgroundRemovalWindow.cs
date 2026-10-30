using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;

namespace Lokum.Windows;

internal sealed class BackgroundRemovalWindow:Window
{
    private readonly string input;
    private readonly Image result=new(){Stretch=Stretch.Uniform};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new(0,10,0,0)};
    private readonly ProgressBar progress=new(){Height=4,IsIndeterminate=true,Margin=new(0,8,0,0)};
    private readonly Button save,retry;
    private readonly CancellationTokenSource lifetime=new();
    private bool working,closed;
    internal bool PreviewReady=>result.Source is BitmapSource&&save.IsEnabled;

    internal BackgroundRemovalWindow(Window owner,string input,int inputCount=1)
    {
        this.input=input;Owner=owner;Title="Remove Background · Lokum";
        Width=900;Height=650;MinWidth=700;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Theme.Attach(this);
        var root=new Grid{Margin=new(24)};Content=root;
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel();header.Children.Add(Label("Remove Background",28));
        header.Children.Add(new TextBlock{Text=Path.GetFileName(input),Foreground=Theme.Brush("Muted"),Margin=new(0,6,0,0)});
        header.Children.Add(Label("Runs on your PC. No image uploads.",12));root.Children.Add(header);
        if(inputCount>1)header.Children.Add(Label("Preview shows the first image. Saving processes all selected images.",12));
        var previews=new Grid{Margin=new(0,18,0,0)};previews.ColumnDefinitions.Add(new());previews.ColumnDefinitions.Add(new());Grid.SetRow(previews,1);root.Children.Add(previews);
        var original=new Image{Source=ImageEngine.Load(input),Stretch=Stretch.Uniform};
        var left=Preview("Original",original);left.Margin=new(0,0,8,0);previews.Children.Add(left);
        var right=Preview("Transparent PNG",result);right.Margin=new(8,0,0,0);Grid.SetColumn(right,1);previews.Children.Add(right);
        var footer=new StackPanel{Margin=new(0,14,0,0)};Grid.SetRow(footer,2);root.Children.Add(footer);
        footer.Children.Add(Label("Inspect fine edges before saving. Complex backgrounds may need further editing.",12));footer.Children.Add(status);footer.Children.Add(progress);
        var buttons=new DockPanel{LastChildFill=false,Margin=new(0,10,0,0)};footer.Children.Add(buttons);
        save=MainWindow.Primary("Save transparent PNG  ↗",()=>{if(PreviewReady)DialogResult=true;});save.IsEnabled=false;DockPanel.SetDock(save,Dock.Right);buttons.Children.Add(save);
        var cancel=MainWindow.Button("Cancel",()=>Close());DockPanel.SetDock(cancel,Dock.Left);buttons.Children.Add(cancel);
        retry=MainWindow.Button("Try again",async()=>await GeneratePreview());retry.Visibility=Visibility.Collapsed;DockPanel.SetDock(retry,Dock.Left);buttons.Children.Add(retry);
        Loaded+=async(_,_)=>await GeneratePreview();
        Closed+=(_,_)=>{closed=true;lifetime.Cancel();if(!working)lifetime.Dispose();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}};
    }

    private async Task GeneratePreview()
    {
        if(working||closed)return;working=true;save.IsEnabled=false;retry.Visibility=Visibility.Collapsed;progress.Visibility=Visibility.Visible;
        L10n.SetText(status,"Removing background…");var token=lifetime.Token;
        try
        {
            var png=await Task.Run(()=>{using var engine=new BackgroundRemovalEngine();return engine.Process(input,token);},token);
            if(closed)return;
            result.Source=ImageEngine.FromPng(png);save.IsEnabled=true;L10n.SetText(status,"Ready. Save a new transparent PNG; your original stays safe.");
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested) { }
        catch(Exception error)
        {
            if(!closed){status.Text=L10n.Text("Couldn’t remove the background.")+"\n"+error.Message;retry.Visibility=Visibility.Visible;}
        }
        finally{working=false;if(closed)lifetime.Dispose();else progress.Visibility=Visibility.Collapsed;}
    }

    private static TextBlock Label(string text,double size)=>L10n.Label(new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new(0,4,0,0)});
    private static Grid Preview(string title,Image image)
    {
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        grid.Children.Add(Label(title,14));
        var drawing=new DrawingGroup();drawing.Children.Add(new GeometryDrawing(Brushes.White,null,new RectangleGeometry(new Rect(0,0,16,16))));
        var gray=new SolidColorBrush(Color.FromRgb(222,218,224));
        drawing.Children.Add(new GeometryDrawing(gray,null,new RectangleGeometry(new Rect(0,0,8,8))));drawing.Children.Add(new GeometryDrawing(gray,null,new RectangleGeometry(new Rect(8,8,8,8))));
        var checker=new DrawingBrush(drawing){TileMode=TileMode.Tile,Viewport=new Rect(0,0,16,16),ViewportUnits=BrushMappingMode.Absolute,Stretch=Stretch.None};checker.Freeze();
        var border=new Border{Background=checker,BorderBrush=Theme.Brush("Line"),BorderThickness=new(1),Child=image,Margin=new(0,8,0,0),ClipToBounds=true};Grid.SetRow(border,1);grid.Children.Add(border);
        return grid;
    }
}
