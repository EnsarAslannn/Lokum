using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Lokum.Core;
namespace Lokum.Windows;

internal sealed class ContextMediaController:IDisposable
{
    [StructLayout(LayoutKind.Sequential)]internal struct Point{public int X,Y;}
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")]private static extern nint GetAncestor(nint handle,uint flags);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint handle,out uint process);
    [DllImport("user32.dll")]private static extern bool PostMessage(nint handle,uint message,nint w,nint l);
    private readonly Window owner;private readonly PreferencesStore preferences;private readonly Action<MediaToolSource,ToolChoice> chosen;private readonly Action<string> notice;
    private readonly System.Windows.Threading.DispatcherTimer timer;
    private readonly ContextClickGesture gesture=new();private CancellationTokenSource? pending;private Window? wheel;private readonly Action? unresolvedDownload;
    private (Point Point,nint Window,Task<string?> Name)? pressed;private bool disposed;
    internal ContextMediaController(Window owner,PreferencesStore preferences,Action<MediaToolSource,ToolChoice> chosen,Action<string> notice,Action? unresolvedDownload=null)
    {
        this.owner=owner;this.preferences=preferences;this.chosen=chosen;this.notice=notice;
        this.unresolvedDownload=unresolvedDownload;
        timer=new(){Interval=TimeSpan.FromMilliseconds(16)};timer.Tick+=(_,_)=>Poll();timer.Start();
    }
    private bool Available=>!disposed&&preferences.Value.Enabled&&DesktopGeometry.IsInteractive(owner);
    internal static async Task<string?> ResolveItemNameAsync(Task<string?> initial,Func<bool> prepareRetry,Func<Task<string?>> retry,CancellationToken token)
    {
        var name=await ReadItemNameAsync(initial,token);token.ThrowIfCancellationRequested();
        if(name is not null||!prepareRetry())return name;
        return await ReadItemNameAsync(retry(),token);
    }
    internal static async Task<string?> ReadItemNameAsync(Task<string?> name,CancellationToken token)
    {
        try{return await name.WaitAsync(TimeSpan.FromSeconds(3),token);}
        catch(TimeoutException){return null;}
    }
    private void Poll()
    {
        var phase=gesture.Update(PrimaryMouseButton.SecondaryIsPressed,(GetAsyncKeyState(0x11)&0x8000)!=0,(GetAsyncKeyState(0x10)&0x8000)!=0||(GetAsyncKeyState(0x12)&0x8000)!=0,Available);
        if(!Available){pressed=null;return;}
        if(phase==ContextClickPhase.Pressed)
        {
            Trace.WriteLine("Context secondary click captured");
            pressed=null;pending?.Cancel();
            if(!GetCursorPos(out var point))return;
            var window=GetAncestor(WindowFromPoint(point),2);GetWindowThreadProcessId(window,out var process);
            if(window!=0&&process!=Environment.ProcessId)pressed=(point,window,Task.Run(()=>ShellMediaSource.ItemAt(point,window)));
        }
        else if(phase==ContextClickPhase.Released&&pressed is {} target)
        {
            Trace.WriteLine("Context secondary click released");
            pressed=null;_ = OpenAsync(target.Point,target.Window,target.Name);
        }
    }
    private async Task OpenAsync(Point point,nint window,Task<string?> itemName)
    {
        if(!Available)return;
        pending?.Cancel();var cancel=new CancellationTokenSource();pending=cancel;var token=cancel.Token;wheel?.Close();wheel=null;
        try
        {
            GetWindowThreadProcessId(window,out var processId);using var process=Process.GetProcessById((int)processId);var name=process.ProcessName.ToLowerInvariant();
            Trace.WriteLine("Context source process: "+name);
            MediaToolSource? source;var browser=name is "chrome" or "msedge" or "firefox" or "brave" or "opera" or "vivaldi";
            await Task.Delay(150,token);
            if(name=="explorer")
            {
                var nameHint=await ResolveItemNameAsync(itemName,()=>
                {
                    if(!BrowserContextMedia.IsSourceForeground(window))return false;
                    Trace.WriteLine("Retry Explorer hit test after dismissing its native menu");BrowserContextMedia.DismissContextMenu(window);return true;
                },async()=>
                {
                    await Task.Delay(75,token);
                    if(!BrowserContextMedia.IsSourceForeground(window)||!BrowserContextMedia.IsSourceWindow(window,GetAncestor(WindowFromPoint(point),2)))return null;
                    return await Task.Run(()=>ShellMediaSource.ItemAt(point,window),token);
                },token);
                if(nameHint is null){Trace.WriteLine("Explorer context item name unavailable");return;}
                if(!BrowserContextMedia.IsSourceForeground(window))return;
                BrowserContextMedia.DismissContextMenu(window);
                source=await ShellMediaSource.AtAsync(window,nameHint,token).WaitAsync(TimeSpan.FromSeconds(3),token);
                if(source is not null)PostMessage(window,0x001F,0,0);
            }
            else if(browser)
            {
                window=GetAncestor(window,3);
                try{source=await BrowserContextMedia.ReadAsync((int)processId,token,window);}
                catch(Exception error)when(error is TimeoutException or ArgumentException or System.Windows.Automation.ElementNotAvailableException||ClipboardImport.IsClipboardError(error)){Trace.WriteLine(error);source=null;}
                if(source is null)try{source=await BrowserContextMedia.KnownVideoPageAsync(window,token);}catch(Exception error)when(error is TimeoutException or ArgumentException or System.Windows.Automation.ElementNotAvailableException||ClipboardImport.IsClipboardError(error)){Trace.WriteLine(error);}
            }
            else return;
            token.ThrowIfCancellationRequested();if(!Available||(!browser&&source is null)){Trace.WriteLine("Context source unavailable or owner disabled");return;}
            if(!BrowserContextMedia.IsSourceForeground(window)){Trace.WriteLine("Context source is no longer foreground");return;}
            if(browser)BrowserContextMedia.DismissContextMenu(window);else PostMessage(window,0x001F,0,0);
            var target=source;
            Window menu=browser?new BrowserDownloadMenu(source,()=>{if(!Available)return;if(target is not null)chosen(target,new("download:media","Download Media","↓"));else unresolvedDownload?.Invoke();}):new WheelWindow(null,source!.Choices,choice=>{if(Available)chosen(target!,choice);}){Topmost=true};wheel=menu;
            menu.Closed+=(_,_)=>{if(ReferenceEquals(wheel,menu))wheel=null;};menu.Show();if(browser)DesktopGeometry.PlacePopup(menu,point.X,point.Y);else DesktopGeometry.PlaceWheel(menu,point.X,point.Y,WheelPresentation.Diameter*preferences.Value.WheelScale);
        }
        catch(OperationCanceledException){}
        catch(Exception error)when(error is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or ExternalException or IOException or UnauthorizedAccessException or NotSupportedException or TimeoutException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {Trace.WriteLine(error);if(Available&&!token.IsCancellationRequested)notice("This media address is unavailable. Save the file, then open its tools in Lokum.");}
        finally{if(ReferenceEquals(pending,cancel))pending=null;cancel.Dispose();}
    }
    public void Dispose(){disposed=true;timer.Stop();pressed=null;pending?.Cancel();wheel?.Close();}
}
