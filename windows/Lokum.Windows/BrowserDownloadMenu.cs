using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
namespace Lokum.Windows;

internal sealed class BrowserDownloadMenu:Window
{
    internal const string WindowTitle="Lokum media download";
    internal BrowserDownloadMenu(MediaToolSource? source,Action download)
    {
        Title=WindowTitle;Width=300;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
        Background=System.Windows.Media.Brushes.Transparent;ShowInTaskbar=false;Topmost=true;Theme.Attach(this);
        var page=new StackPanel();page.Children.Add(L10n.Label(new TextBlock{Text=source?.Kind switch{"image"=>"Image","video"=>"Video","audio"=>"Audio",_=>"Web media"},FontSize=12,Foreground=Theme.Brush("Muted"),Margin=new(3,0,3,10)}));
        var button=MainWindow.Button("Download Media",()=>{Close();download();});button.Background=Theme.Primary;button.Foreground=Theme.PrimaryInk;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Padding=new(16,12,16,12);AutomationProperties.SetAutomationId(button,"web.download.media");page.Children.Add(button);
        page.Children.Add(L10n.Label(new TextBlock{Text=source is null?"Paste a media link to download.":"Saves to your configured Downloads folder.",FontSize=11,Foreground=Theme.Brush("Muted"),TextWrapping=TextWrapping.Wrap,Margin=new(3,10,3,0)}));
        Content=new Border{Background=Theme.Brush("Surface"),BorderBrush=Theme.Brush("Line"),BorderThickness=new(1),CornerRadius=new(16),Padding=new(16),Child=page};
        var closing=false;Closing+=(_,_)=>closing=true;Deactivated+=(_,_)=>{if(!closing&&IsVisible)Close();};PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};Loaded+=(_,_)=>button.Focus();
    }
}
