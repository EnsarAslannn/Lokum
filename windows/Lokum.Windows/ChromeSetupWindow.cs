using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Lokum.Windows;

internal sealed class ChromeSetupWindow:Window
{
    internal ChromeSetupWindow(Window owner)
    {
        Owner=owner;Title="Chrome connection · Lokum";Width=660;Height=510;MinWidth=580;MinHeight=420;WindowStartupLocation=WindowStartupLocation.CenterOwner;Theme.Attach(this);
        var page=new StackPanel{Margin=new(28)};Content=new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        void Label(string text,double size=14){page.Children.Add(L10n.Label(new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,14)}));}
        Label("Chrome media tools",27);Label("Ctrl + left-click an image or video on the web to open its Lokum tools. Your local file shortcuts stay the same.");
        Label("1. Prepare the connection on this PC.");var status=L10n.Label(new TextBlock{Text="The extension only sends the media you Ctrl-click to Lokum on this computer. It does not use your clipboard.",TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,18)});
        page.Children.Add(MainWindow.Button("Prepare Chrome connection",()=>
        {
            try{ChromeBridgeSetup.Register();L10n.SetText(status,"Chrome connection prepared. Add the extension once, then refresh your web page.");}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException){L10n.SetText(status,error.Message);}
        }));
        Label("2. In Chrome extensions, turn on Developer mode and choose Load unpacked.");
        page.Children.Add(MainWindow.Button("Open Chrome extensions",()=>Process.Start(new ProcessStartInfo("chrome.exe","chrome://extensions"){UseShellExecute=true})));
        Label("3. Select this folder. Refresh the page you want to use.");
        var path=new TextBox{Text=ChromeBridgeSetup.ExtensionDirectory,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,10)};page.Children.Add(path);
        page.Children.Add(MainWindow.Button("Copy extension folder",()=>{Clipboard.SetText(path.Text);L10n.SetText(status,"Copied extension folder.");}));page.Children.Add(status);
        var close=MainWindow.Button("Close",Close);close.HorizontalAlignment=HorizontalAlignment.Right;page.Children.Add(close);
    }
}
