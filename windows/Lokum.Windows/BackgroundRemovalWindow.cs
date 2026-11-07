using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lokum.Core;
using Microsoft.Win32;

namespace Lokum.Windows;

internal sealed class BackgroundRemovalWindow:Window
{
    private sealed class Document(string path)
    {
        internal readonly string Path=path;
        internal ForegroundRegion[] Regions=[];
        internal bool Automatic;
        internal byte[]? Cutout;
    }
    private readonly Document[] documents;
    private readonly ForegroundSelectionSurface selection=new();
    private readonly Image result=new(){Stretch=Stretch.Uniform};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new(4,4,4,0)};
    private readonly TextBlock imageName=new(){TextWrapping=TextWrapping.Wrap,Margin=new(4)};
    private readonly TextBlock empty=L10n.Label(new TextBlock{Text="Select an area on the left to create your cutout.",TextWrapping=TextWrapping.Wrap,MaxWidth=230,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new(20),Padding=new(12)});
    private readonly ProgressBar progress=new(){Height=4,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new(4,4,4,0)};
    private readonly Button save;
    private readonly StackPanel backgroundOptions=new();
    private readonly ComboBox shape=new(){ItemsSource=new[]{"Freehand selection","Rectangle selection"},SelectedIndex=0};
    private readonly ComboBox background=new(){ItemsSource=new[]{"Transparent","Solid color","Background image"},SelectedIndex=0};
    private readonly ComboBox files=new(){MinWidth=240,MaxWidth=470};
    private readonly TextBox color=new(){Text="#FFFFFF",MaxLength=7};
    private readonly StackPanel colorOptions=new(),imageOptions=new();
    private readonly CancellationTokenSource lifetime=new();
    private readonly System.Windows.Threading.DispatcherTimer debounce=new(){Interval=TimeSpan.FromMilliseconds(180)};
    private CancellationTokenSource? pending;
    private byte[]? backgroundPng;
    private long revision;
    private long renderedRevision=-1;
    private long imageRequest;
    private int current;
    private bool loading,closed,working,loadingBackground;
    private Document Current=>documents[current];
    internal bool PreviewReady=>!working&&renderedRevision==revision&&result.Source is BitmapSource&&Current.Cutout is not null;
    internal bool EditorReady=>selection.SourceBitmap is not null&&!working;
    internal PreparedCutout[] PreparedImages=>documents.Select(d=>new PreparedCutout(d.Path,d.Cutout??throw new InvalidOperationException("Finish selecting every image before saving."))).ToArray();
    internal BackgroundFillOptions FillOptions=>new((BackgroundFill)background.SelectedIndex,color.Text,backgroundPng);

    internal BackgroundRemovalWindow(Window owner,string input):this(owner,[input]) { }
    internal BackgroundRemovalWindow(Window owner,IReadOnlyList<string> inputs,bool addBackground=false)
    {
        if(inputs.Count==0)throw new ArgumentException("Choose one or more images.");
        documents=inputs.Select(p=>new Document(p)).ToArray();Owner=owner;Title="Background editor · Lokum";
        empty.SetResourceReference(TextBlock.BackgroundProperty,"Surface");AutomationProperties.SetAutomationId(result,"BackgroundResult");
        Width=1240;Height=800;MinWidth=1000;MinHeight=620;WindowStartupLocation=WindowStartupLocation.CenterOwner;Theme.Attach(this);
        var root=new Grid{Margin=new(24)};Content=root;
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel();header.Children.Add(Label("Edit Background",27,true));
        header.Children.Add(Label("Keep what you select. Save it transparent, or add a new background.",13));
        files.ItemsSource=inputs.Select(Path.GetFileName).ToArray();files.SelectedIndex=0;
        AutomationProperties.SetAutomationId(files,"BackgroundInputFiles");L10n.Bind(files,AutomationProperties.NameProperty,"Image to edit");
        if(inputs.Count>1){header.Children.Add(files);header.Children.Add(Label("Select each image separately. The background setting is shared.",12));}
        root.Children.Add(header);
        var body=new Grid{Margin=new(0,14,0,0)};body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new(280)});body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});Grid.SetRow(body,1);root.Children.Add(body);
        var tools=new WrapPanel{Margin=new(0,0,12,8)};Grid.SetColumnSpan(tools,2);body.Children.Add(tools);
        var sourcePreview=Preview("1 · Select what to keep",new Viewbox{Child=selection,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly});sourcePreview.Margin=new(0,0,10,0);Grid.SetRow(sourcePreview,1);body.Children.Add(sourcePreview);
        var previewContent=new Grid();previewContent.Children.Add(result);previewContent.Children.Add(empty);
        var resultPreview=Preview("Result",previewContent);resultPreview.Margin=new(0,0,14,0);Grid.SetColumn(resultPreview,1);Grid.SetRow(resultPreview,1);body.Children.Add(resultPreview);
        var controls=new StackPanel();var scroll=new ScrollViewer{Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(scroll,2);Grid.SetRowSpan(scroll,2);body.Children.Add(scroll);
        shape.MinWidth=160;tools.Children.Add(shape);AutomationProperties.SetAutomationId(shape,"ForegroundSelectionTool");L10n.Bind(shape,AutomationProperties.NameProperty,"Selection tools");
        selection.ToolTip="Draw around the area to keep. Everything outside your selection becomes transparent.";
        var subtract=L10n.Content(new CheckBox{Content=Label("Subtract from selection",12),Margin=new(4,8,10,8)});tools.Children.Add(subtract);
        subtract.Checked+=(_,_)=>selection.Subtract=true;subtract.Unchecked+=(_,_)=>selection.Subtract=false;
        tools.Children.Add(MainWindow.Button("Select all",selection.SelectAll));
        tools.Children.Add(MainWindow.Button("Undo selection",UndoSelection));
        tools.Children.Add(MainWindow.Button("Clear selection",()=>{Current.Automatic=false;selection.Clear();}));
        var automatic=MainWindow.Button("Select automatically",()=>{Current.Automatic=true;InvalidateCutout();});automatic.ToolTip="Automatic selection stays on your PC. Draw to add or subtract areas afterward.";AutomationProperties.SetAutomationId(automatic,"AutomaticForeground");tools.Children.Add(automatic);
        controls.Children.Add(Label("2 · Choose a background",16,true));controls.Children.Add(backgroundOptions);
        backgroundOptions.Children.Add(background);AutomationProperties.SetAutomationId(background,"BackgroundFillType");L10n.Bind(background,AutomationProperties.NameProperty,"Background type");
        colorOptions.Children.Add(Label("Background color",12));colorOptions.Children.Add(color);AutomationProperties.SetAutomationId(color,"BackgroundColor");L10n.Bind(color,AutomationProperties.NameProperty,"Background color");color.ToolTip="#RRGGBB";
        colorOptions.Children.Add(new ColorPalette(color));
        var palette=new WrapPanel();foreach(var value in new[]{"#FFFFFF","#000000","#F7DEE5","#E1EBDD","#B63F68","#365B82"})
        {
            var swatch=MainWindow.Button(value,()=>color.Text=value);swatch.Width=76;swatch.Padding=new(6,8,6,8);swatch.FontSize=10;
            swatch.Background=ImageEngine.ColorBrush(value);swatch.Foreground=value is "#000000" or "#B63F68" or "#365B82"?Brushes.White:Brushes.Black;
            AutomationProperties.SetName(swatch,value);palette.Children.Add(swatch);
        }
        colorOptions.Children.Add(palette);backgroundOptions.Children.Add(colorOptions);
        var choose=MainWindow.Button("Choose background image…",async()=>
        {
            var picker=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.webp;*.heic;*.avif;*.bmp;*.tif;*.tiff;*.gif;*.svg"};
            if(picker.ShowDialog(this)==true)await UseBackgroundImageAsync(picker.FileName);
        });imageOptions.Children.Add(choose);imageOptions.Children.Add(imageName);imageOptions.Children.Add(Label("The background image fills the original canvas and is centered.",12));backgroundOptions.Children.Add(imageOptions);
        controls.Children.Add(Label("Undo: Ctrl+Z · Select all: Ctrl+A · Save: Ctrl+S",11));
        var footer=new StackPanel{Margin=new(0,10,0,0)};Grid.SetRow(footer,2);root.Children.Add(footer);footer.Children.Add(status);footer.Children.Add(progress);
        var buttons=new DockPanel{LastChildFill=false,Margin=new(0,8,0,0)};footer.Children.Add(buttons);
        save=MainWindow.Primary("Save new PNG  ↗",()=>{if(PreviewReady&&documents.All(d=>d.Cutout is not null))DialogResult=true;});save.IsEnabled=false;AutomationProperties.SetAutomationId(save,"SaveBackgroundEdit");DockPanel.SetDock(save,Dock.Right);buttons.Children.Add(save);
        var cancel=MainWindow.Button("Cancel",()=>Close());DockPanel.SetDock(cancel,Dock.Left);buttons.Children.Add(cancel);
        buttons.Children.Add(Label("Original stays safe",12));
        shape.SelectionChanged+=(_,_)=>selection.Shape=(ForegroundSelectionShape)shape.SelectedIndex;
        selection.SelectionChanged+=()=>{if(loading)return;Current.Regions=selection.Regions;InvalidateCutout();};
        background.SelectionChanged+=(_,_)=>{UpdateOptions();QueuePreview();};color.TextChanged+=(_,_)=>{if(!loading&&background.SelectedIndex==1)QueuePreview();};
        files.SelectionChanged+=(_,_)=>{if(!loading&&files.SelectedIndex>=0)LoadDocument(files.SelectedIndex);};
        debounce.Tick+=async(_,_)=>{debounce.Stop();await GeneratePreview();};
        Closed+=(_,_)=>{closed=true;debounce.Stop();pending?.Cancel();lifetime.Cancel();lifetime.Dispose();};
        PreviewKeyDown+=(_,e)=>
        {
            if(e.Key==Key.Escape){if(selection.IsDrawing)selection.CancelSelection();else Close();e.Handled=true;return;}
            if(ClipboardImport.IsEditing(Keyboard.FocusedElement as DependencyObject))return;
            if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Z){UndoSelection();e.Handled=true;}
            if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.A){selection.SelectAll();e.Handled=true;}
            if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.S&&save.IsEnabled){DialogResult=true;e.Handled=true;}
        };
        if(addBackground)background.SelectedIndex=1;UpdateOptions();LoadDocument(0);
    }

    private void LoadDocument(int index)
    {
        pending?.Cancel();debounce.Stop();revision++;working=false;current=index;loading=true;
        try{selection.Load(ImageEngine.Load(Current.Path),Current.Regions);}
        finally{loading=false;}
        result.Source=null;empty.Visibility=Visibility.Visible;files.ToolTip=Current.Path;UpdateOptions();
        if(Current.Cutout is not null||Current.Automatic||Current.Regions.Any(r=>!r.Subtract))QueuePreview();else SetIdle();
    }
    private void InvalidateCutout(){Current.Cutout=null;result.Source=null;empty.Visibility=Visibility.Visible;UpdateOptions();QueuePreview();}
    private void UndoSelection(){if(selection.Regions.Length>0)selection.Undo();else if(Current.Automatic){Current.Automatic=false;InvalidateCutout();}}
    private void QueuePreview()
    {
        if(loading||closed)return;revision++;pending?.Cancel();save.IsEnabled=false;debounce.Stop();
        if(loadingBackground){working=true;return;}
        if(Current.Cutout is null&&!Current.Automatic&&!Current.Regions.Any(r=>!r.Subtract)){working=false;SetIdle();return;}
        working=true;progress.Visibility=Visibility.Visible;L10n.SetText(status,"Preparing your selection…");debounce.Start();
    }
    private void SetIdle()
    {
        save.IsEnabled=false;progress.Visibility=Visibility.Collapsed;
        L10n.SetText(status,"Select an area to keep or use automatic removal.");
    }
    private async Task GeneratePreview()
    {
        var document=Current;var ticket=revision;var options=FillOptions;var regions=document.Regions;
        var automatic=document.Automatic;var cutout=document.Cutout;
        using var request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);pending=request;
        try
        {
            var prepared=await Task.Run(()=>
            {
                var png=cutout;if(png is null){using var engine=new BackgroundEditingEngine();png=engine.CreateCutout(document.Path,regions,automatic,request.Token);}
                return png;
            },request.Token);
            if(closed||ticket!=revision)return;
            document.Cutout=prepared;UpdateOptions();
            var composed=await Task.Run(()=>BackgroundEditingEngine.Compose(prepared,options,request.Token),request.Token);
            if(closed||ticket!=revision)return;
            result.Source=ImageEngine.FromPng(composed);renderedRevision=ticket;empty.Visibility=Visibility.Collapsed;
            backgroundOptions.IsEnabled=true;save.IsEnabled=documents.All(d=>d.Cutout is not null);
            L10n.SetText(status,save.IsEnabled?"Ready. Save a new PNG; your original stays safe.":"Select the remaining images before saving the batch.");
        }
        catch(OperationCanceledException)when(request.IsCancellationRequested) { }
        catch(Exception error){if(!closed&&ticket==revision){status.Text=L10n.Text(error.Message);save.IsEnabled=false;}}
        finally
        {
            if(ReferenceEquals(pending,request))pending=null;
            if(!closed&&ticket==revision){working=false;progress.Visibility=Visibility.Collapsed;UpdateOptions();}
        }
    }
    internal async Task UseBackgroundImageAsync(string path)
    {
        var request=++imageRequest;loadingBackground=true;revision++;pending?.Cancel();debounce.Stop();
        working=true;save.IsEnabled=false;backgroundPng=null;imageName.Text="";
        progress.Visibility=Visibility.Visible;L10n.SetText(status,"Loading background image…");
        try
        {
            var png=await Task.Run(()=>PhotoCodec.Png(path),lifetime.Token);if(closed||request!=imageRequest)return;
            loadingBackground=false;working=false;
            backgroundPng=png;imageName.Text=Path.GetFileName(path);imageName.ToolTip=path;background.SelectedIndex=2;QueuePreview();
        }
        catch(OperationCanceledException)when(closed) { }
        catch(Exception error){if(!closed&&request==imageRequest){loadingBackground=false;working=false;progress.Visibility=Visibility.Collapsed;status.Text=L10n.Text(error.Message);save.IsEnabled=false;}}
    }
    private void UpdateOptions()
    {
        backgroundOptions.IsEnabled=Current.Cutout is not null;
        colorOptions.Visibility=background.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;
        imageOptions.Visibility=background.SelectedIndex==2?Visibility.Visible:Visibility.Collapsed;
    }
    private static TextBlock Label(string text,double size,bool bold=false)=>L10n.Label(new TextBlock{Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new(4,4,4,4)});
    private static Grid Preview(string title,UIElement content)
    {
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});grid.Children.Add(Label(title,15,true));
        var pattern=new DrawingGroup();pattern.Children.Add(new GeometryDrawing(Brushes.White,null,new RectangleGeometry(new Rect(0,0,16,16))));var gray=new SolidColorBrush(Color.FromRgb(222,218,224));
        pattern.Children.Add(new GeometryDrawing(gray,null,new RectangleGeometry(new Rect(0,0,8,8))));pattern.Children.Add(new GeometryDrawing(gray,null,new RectangleGeometry(new Rect(8,8,8,8))));
        var checker=new DrawingBrush(pattern){TileMode=TileMode.Tile,Viewport=new Rect(0,0,16,16),ViewportUnits=BrushMappingMode.Absolute,Stretch=Stretch.None};checker.Freeze();
        var border=new Border{Background=checker,BorderBrush=Theme.Brush("Line"),BorderThickness=new(1),Child=content,Margin=new(0,6,0,0),ClipToBounds=true};Grid.SetRow(border,1);grid.Children.Add(border);return grid;
    }
}
