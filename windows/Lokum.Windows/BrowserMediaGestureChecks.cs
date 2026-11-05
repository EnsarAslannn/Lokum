using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Lokum.Core;
namespace Lokum.Windows;

// Real browser regression, used only by the explicit native test command. A
// disposable Edge profile displays generated fixtures, never a user's tabs.
internal static class BrowserMediaGestureChecks
{
    private delegate bool Visitor(nint window,nint argument);
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool EnumWindows(Visitor visit,nint argument);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(nint window,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]private static extern bool ShowWindow(nint window,int command);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll")]private static extern bool PostMessage(nint window,uint message,nint w,nint l);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint FindWindow(string? type,string title);
    internal static void Run(Window owner,string root,string image,Action<bool,string> check)
    {
        var edge=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe");
        if(!File.Exists(edge))throw new Exception("Edge is required for the browser context regression.");
        var tools=new Toolchain(Environment.GetEnvironmentVariable("LOKUM_CONTEXT_TEST_TOOLS")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Lokum","Tools","v1"));
        var video=Path.Combine(root,"Browser video.mp4");ProcessRunner.RunAsync(tools.Executable("ffmpeg"),["-hide_banner","-loglevel","error","-f","lavfi","-i","color=c=blue:s=160x120:r=10","-t","2","-c:v","libx264","-pix_fmt","yuv420p","-an",video]).GetAwaiter().GetResult();
        var identity="Lokum Browser Context "+Guid.NewGuid().ToString("N")[..8];var html=Path.Combine(root,"browser-fixture.html");
        File.WriteAllText(html,$"<!doctype html><title>{identity}</title><body style='padding:60px;display:flex;gap:80px'><img alt='Lokum Context Image Fixture' width='240' height='200' src='{new Uri(image).AbsoluteUri}'><video aria-label='Lokum Context Video Fixture' width='360' height='240' controls src='{new Uri(video).AbsoluteUri}'></video></body>");
        var start=new ProcessStartInfo(edge){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Normal};foreach(var argument in new[]{"--guest","--user-data-dir="+Path.Combine(root,"edge-profile"),"--no-first-run","--no-default-browser-check","--new-window",new Uri(html).AbsoluteUri})start.ArgumentList.Add(argument);
        ClipboardSnapshot? original=null;using var browser=Process.Start(start)!;nint handle=0;
        var end=DateTime.UtcNow.AddSeconds(20);
        while(handle==0&&DateTime.UtcNow<end){EnumWindows((window,_)=>{var title=new System.Text.StringBuilder(512);GetWindowText(window,title,title.Capacity);if(title.ToString().Contains(identity,StringComparison.Ordinal))handle=window;return true;},0);DesktopInput.Pump(100);}
        if(handle==0)throw new Exception("The isolated Edge fixture did not open.");
        var settings=new PreferencesStore(Path.Combine(root,"Browser gestures"));MediaToolSource? received=null;ToolChoice? chosen=null;string? notice=null;
        using var controller=new ContextMediaController(owner,settings,(source,choice)=>{received=source;chosen=choice;},message=>notice=message);
        try
        {
            foreach(var fixture in new[]{(Label:"Lokum Context Image Fixture",Path:image,Kind:"image",Tool:"editImage"),(Label:"Lokum Context Video Fixture",Path:video,Kind:"video",Tool:"muteVideo")})
            {
                ShowWindow(handle,9);SetForegroundWindow(handle);DesktopInput.Pump(300);
                var focusLimit=DateTime.UtcNow.AddSeconds(45);while(GetForegroundWindow()!=handle&&DateTime.UtcNow<focusLimit)DesktopInput.Pump(100);
                if(GetForegroundWindow()!=handle)throw new Exception("The browser fixture is not foreground; no input will be sent to another app.");
                received=null;chosen=null;original??=ClipboardSnapshot.Capture();Clipboard.SetText("Lokum original clipboard fixture");
                var bounds=Task.Run(()=>
                {
                    var limit=DateTime.UtcNow.AddSeconds(15);
                    while(DateTime.UtcNow<limit){var item=AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,fixture.Label));if(item is not null&&!item.Current.BoundingRectangle.IsEmpty)return item.Current.BoundingRectangle;Thread.Sleep(100);}
                    throw new Exception("Browser fixture has no accessible media bounds: "+fixture.Kind);
                });
                while(!bounds.IsCompleted)DesktopInput.Pump(30);var sourceBounds=bounds.GetAwaiter().GetResult();
                var click=Task.Run(()=>
                {
                    SetForegroundWindow(handle);Thread.Sleep(250);if(GetForegroundWindow()!=handle)throw new Exception("Browser focus changed; context test cancelled before sending input.");DesktopInput.MovePointer(new(sourceBounds.Left+sourceBounds.Width/2,sourceBounds.Top+sourceBounds.Height/2));DesktopInput.Hold(Key.LeftCtrl,true);DesktopInput.HoldMouse(true,secondary:true);Thread.Sleep(180);DesktopInput.HoldMouse(false,secondary:true);DesktopInput.Hold(Key.LeftCtrl,false);
                    nint menu=0;for(var n=0;n<80&&menu==0;n++){Thread.Sleep(100);menu=FindWindow(null,BrowserDownloadMenu.WindowTitle);}
                    if(menu==0||!GetWindowRect(menu,out var rect)){owner.Dispatcher.Invoke(()=>DesktopCapture.Save(Path.Combine(root,"browser-context-timeout.png")));throw new Exception("Browser Ctrl + right-click did not open the "+fixture.Kind+" download menu; "+notice);}
                    DesktopInput.MovePointer(new((rect.Left+rect.Right)/2d,rect.Top+(rect.Bottom-rect.Top)*.5));Thread.Sleep(100);DesktopInput.HoldMouse(true);DesktopInput.HoldMouse(false);
                });
                var limit=DateTime.UtcNow.AddSeconds(12);while(!click.IsCompleted&&DateTime.UtcNow<limit)DesktopInput.Pump(30);if(!click.IsCompleted)throw new Exception("Browser context regression timed out");click.GetAwaiter().GetResult();DesktopInput.Pump(200);
                if(received?.Kind!=fixture.Kind||received.Paths.SingleOrDefault()!=fixture.Path||chosen?.Id!="download:media")DesktopCapture.Save(Path.Combine(root,"browser-context-failed.png"));
                check(received?.Kind==fixture.Kind&&received.Paths.SingleOrDefault()==fixture.Path&&chosen?.Id=="download:media","Edge Ctrl + right-click resolves the clicked "+fixture.Kind+" and offers Download Media; observed="+received?.Kind+"/"+string.Join(",",received?.Paths??[])+"/"+chosen?.Id);
                check(received!.Choices.Any(choice=>choice.Id==fixture.Tool),"The resolved "+fixture.Kind+" can be used with its normal local editing tools after saving");
                check(Clipboard.GetText()=="Lokum original clipboard fixture","Browser "+fixture.Kind+" address extraction restores the original clipboard");
            }
        }
        finally{if(original is not null&&Clipboard.ContainsText()&&Clipboard.GetText()=="Lokum original clipboard fixture")original.Restore();DesktopInput.Hold(Key.LeftCtrl,false);DesktopInput.HoldMouse(false,secondary:true);PostMessage(handle,0x10,0,0);}
    }
}
