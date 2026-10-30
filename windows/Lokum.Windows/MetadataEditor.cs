using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Lokum.Core;
namespace Lokum.Windows;
public sealed class MetadataEditor:Window
{
    private readonly CheckBox removeAll=new(){Content="Remove all metadata",Margin=new(4,14,4,14)};
    private readonly Dictionary<string,TextBox> fields=[];private readonly Dictionary<string,string> original=[];
    public bool RemoveAll=>removeAll.IsChecked==true;public Dictionary<string,string> Updates=>fields.Where(p=>p.Value.Text!=original.GetValueOrDefault(p.Key,"")).ToDictionary(p=>p.Key,p=>p.Value.Text);
    public MetadataEditor(Window owner,IReadOnlyDictionary<string,string> metadata,bool media=false)
    {
        Owner=owner;Theme.Attach(this);Title="Metadata · Lokum";Width=820;Height=740;MinWidth=640;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new(26)};Content=root;var header=new StackPanel();header.Children.Add(L10n.Label(new TextBlock{Text="Metadata",FontSize=28,FontWeight=FontWeights.Bold}));header.Children.Add(L10n.Label(new TextBlock{Text="Inspect saved information, edit fields or remove personal tags. Your original file stays unchanged.",Margin=new(0,8,0,0),TextWrapping=TextWrapping.Wrap}));header.Children.Add(removeAll);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var footer=new DockPanel{LastChildFill=false,Margin=new(0,16,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var save=MainWindow.Button("Save new copy ↗",()=>DialogResult=true);save.Background=Theme.Primary;save.Foreground=Theme.PrimaryInk;DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);var cancel=MainWindow.Button("Cancel",()=>DialogResult=false);DockPanel.SetDock(cancel,Dock.Left);footer.Children.Add(cancel);var stack=new StackPanel();root.Children.Add(new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var source=metadata.GetValueOrDefault("SourceFile","")??"";var prefix=FeatureCatalog.Extension(source)=="pdf"?"PDF:":FeatureCatalog.Extension(source)=="mp3"?"ID3:":FeatureCatalog.IsVideo(source)||FeatureCatalog.IsAudio(source)?"QuickTime:":"";
        IEnumerable<string> editable=MetadataService.EditableTags.Where(k=>prefix.Length>0?k.StartsWith(prefix,StringComparison.Ordinal):k.StartsWith("EXIF:",StringComparison.Ordinal)||k.StartsWith("XMP:",StringComparison.Ordinal));
        if(media)editable=MediaMetadata.Editable.Concat(metadata.Keys.Where(k=>k.StartsWith("stream:",StringComparison.Ordinal)||k.StartsWith("chapter:",StringComparison.Ordinal)).Where(k=>k.EndsWith(":title",StringComparison.Ordinal)||k.EndsWith(":language",StringComparison.Ordinal)||k.EndsWith(":handler_name",StringComparison.Ordinal)||k.EndsWith(":comment",StringComparison.Ordinal))).Distinct();
        // Screenshots often have no optional EXIF/XMP tags. Show the facts we actually read first.
        var summary=new StackPanel{Margin=new(14,10,14,10)};
        summary.Children.Add(Heading("File information"));
        summary.Children.Add(new TextBlock{Text=Path.GetFileName(source),TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold,Margin=new(4)});
        var facts=new List<string>();
        var format=metadata.GetValueOrDefault("File:FileType",FeatureCatalog.Extension(source).ToUpperInvariant());
        if(!string.IsNullOrWhiteSpace(format))facts.Add(format);
        string? Tag(string name)=>metadata.FirstOrDefault(p=>p.Key.EndsWith(":"+name,StringComparison.Ordinal)).Value;
        var width=Tag("ImageWidth");var height=Tag("ImageHeight");
        if(!string.IsNullOrEmpty(width)&&!string.IsNullOrEmpty(height))facts.Add(width+" × "+height+" px");
        if(Tag("FileSize") is {Length:>0} size)facts.Add(size);
        summary.Children.Add(new TextBlock{Text=string.Join("  ·  ",facts),TextWrapping=TextWrapping.Wrap,Margin=new(4)});
        var card=new Border{Child=summary,CornerRadius=new(12),BorderThickness=new(1),Margin=new(4,0,4,8)};
        card.SetResourceReference(Border.BackgroundProperty,"Surface");card.SetResourceReference(Border.BorderBrushProperty,"Line");stack.Children.Add(card);
        var saved=new StackPanel();var optional=new StackPanel();var savedCount=0;
        foreach(var key in editable)
        {
            var value=metadata.GetValueOrDefault(key,"");original[key]=value;
            var caption=media?MediaMetadata.Label(key):Caption(key);
            var panel=string.IsNullOrEmpty(value)?optional:saved;if(!string.IsNullOrEmpty(value))savedCount++;
            panel.Children.Add(L10n.Label(new TextBlock{Text=caption,Margin=new(4,10,4,0),ToolTip=key}));
            var field=new TextBox{Text=value,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxHeight=150,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            L10n.Bind(field,AutomationProperties.NameProperty,caption);AutomationProperties.SetAutomationId(field,key);field.ToolTip=key;
            fields[key]=field;panel.Children.Add(field);
        }
        stack.Children.Add(Heading("Saved metadata fields"));
        if(savedCount==0)stack.Children.Add(L10n.Label(new TextBlock{Text="No editable tags are saved in this file. You can add a title, author or description below.",Margin=new(4,6,4,10),TextWrapping=TextWrapping.Wrap}));
        stack.Children.Add(saved);
        if(optional.Children.Count>0)stack.Children.Add(new Expander{Header="Add metadata fields",Content=optional,Margin=new(4,14,4,8)});
        var details=new StackPanel();foreach(var pair in metadata.Where(p=>p.Key!="SourceFile"))details.Children.Add(new TextBlock{Text=(media?MediaMetadata.Label(pair.Key):pair.Key)+"  ·  "+pair.Value,TextWrapping=TextWrapping.Wrap,Margin=new(4,5,4,5)});stack.Children.Add(new Expander{Header="All metadata",Content=details,Margin=new(4,12,4,8)});removeAll.Checked+=(_,_)=>{foreach(var field in fields.Values)field.IsEnabled=false;};removeAll.Unchecked+=(_,_)=>{foreach(var field in fields.Values)field.IsEnabled=true;};PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}};
    }
    private static TextBlock Heading(string text)=>L10n.Label(new TextBlock{Text=text,FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new(4,8,4,4)});
    private static string Caption(string key)=>key[(key.IndexOf(':')+1)..] switch
    {
        "Artist"=>"Author","ImageDescription"=>"Description","Make"=>"Camera manufacturer","Model"=>"Camera model",
        "DateTimeOriginal"=>"Date taken","CreateDate"=>"Created","ModifyDate"=>"Modified","UserComment"=>"Comment",var name=>name
    };
}
