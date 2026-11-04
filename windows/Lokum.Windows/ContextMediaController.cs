using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Lokum.Core;
namespace Lokum.Windows;

// Only explicit Ctrl + right-clicks are inspected. The normal context menu is
// allowed to open; its media command supplies the address the browser actually
// exposes, without reading pages or using a remembered/clipboard URL as a guess.
internal sealed class ContextMediaController:IDisposable
{
    [StructLayout(LayoutKind.Sequential)]internal struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseEvent{public Point Point;public uint Data,Flags,Time;public nuint Extra;}
    private delegate nint MouseHook(int code,nint message,nint data);
    [DllImport("user32.dll",SetLastError=true)]private static extern nint SetWindowsHookEx(int type,MouseHook callback,nint module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]private static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")]private static extern nint GetAncestor(nint handle,uint flags);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint handle,out uint process);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool PostMessage(nint handle,uint message,nint w,nint l);
    private readonly Window owner;private readonly PreferencesStore preferences;private readonly Action<MediaToolSource,ToolChoice> chosen;private readonly Action<string> notice;
    private readonly MouseHook callback;private readonly nint hook;private CancellationTokenSource? pending;private WheelWindow? wheel;
    private (Point Point,nint Window,Task<string?> Name)? pressed;private bool disposed;
    internal ContextMediaController(Window owner,PreferencesStore preferences,Action<MediaToolSource,ToolChoice> chosen,Action<string> notice)
    {
        this.owner=owner;this.preferences=preferences;this.chosen=chosen;this.notice=notice;
        callback=(code,message,data)=>
        {
            if(code>=0&&!disposed)
            {
                if(message==0x0204)
                {
                    pressed=null;
                    if(Available&&(GetAsyncKeyState(0x11)&0x8000)!=0&&(GetAsyncKeyState(0x10)&0x8000)==0&&(GetAsyncKeyState(0x12)&0x8000)==0)
                    {
                        var point=Marshal.PtrToStructure<MouseEvent>(data).Point;var window=GetAncestor(WindowFromPoint(point),2);GetWindowThreadProcessId(window,out var process);
                        if(process!=Environment.ProcessId)pressed=(point,window,Task.Run(()=>ShellMediaSource.ItemAt(point)));
                    }
                }
                else if(message==0x0205&&pressed is {} target)
                {
                    pressed=null;_ = owner.Dispatcher.BeginInvoke(()=>_ = OpenAsync(target.Point,target.Window,target.Name));
                }
            }
            return CallNextHookEx(hook,code,message,data);
        };
        hook=SetWindowsHookEx(14,callback,0,0);
        if(hook==0)notice("Ctrl + right-click could not be registered. Drag a file with Ctrl to open its tools.");
    }
    private bool Available=>!disposed&&preferences.Value.Enabled&&DesktopGeometry.IsInteractive(owner);
    private async Task OpenAsync(Point point,nint window,Task<string?> itemName)
    {
        if(!Available)return;
        pending?.Cancel();var cancel=new CancellationTokenSource();pending=cancel;var token=cancel.Token;wheel?.Close();wheel=null;
        try
        {
            GetWindowThreadProcessId(window,out var processId);using var process=Process.GetProcessById((int)processId);var name=process.ProcessName.ToLowerInvariant();
            Trace.WriteLine("Context source process: "+name);
            MediaToolSource? source;
            await Task.Delay(150,token);
            if(name=="explorer")
            {
                var nameHint=itemName.IsCompletedSuccessfully?itemName.Result:null;if(nameHint is null)return;source=await ShellMediaSource.AtAsync(window,nameHint,token);
                if(source is not null)PostMessage(window,0x001F,0,0); // Cancel Explorer's native context menu.
            }
            else if(name is "chrome" or "msedge" or "firefox" or "brave" or "opera" or "vivaldi")
                source=await BrowserContextMedia.ReadAsync((int)processId,token,window);
            else return;
            token.ThrowIfCancellationRequested();if(!Available||source is null){Trace.WriteLine("No context media source resolved");return;}
            // Never steal focus after the user switched to a different application.
            if(GetForegroundWindow()!=window)return;
            PostMessage(window,0x001F,0,0);
            var target=source;var menu=new WheelWindow(null,source.Choices,choice=>{if(Available)chosen(target,choice);}){Topmost=true};wheel=menu;
            menu.Closed+=(_,_)=>{if(ReferenceEquals(wheel,menu))wheel=null;};menu.Show();DesktopGeometry.PlaceWheel(menu,point.X,point.Y,WheelPresentation.Diameter*preferences.Value.WheelScale);
        }
        catch(OperationCanceledException){}
        catch(Exception error)when(error is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or ExternalException or IOException or UnauthorizedAccessException or NotSupportedException or TimeoutException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {Trace.WriteLine(error);if(Available&&!token.IsCancellationRequested)notice("This media address is unavailable. Save the file, then open its tools in Lokum.");}
        finally{if(ReferenceEquals(pending,cancel))pending=null;cancel.Dispose();}
    }
    public void Dispose(){disposed=true;pending?.Cancel();wheel?.Close();if(hook!=0)UnhookWindowsHookEx(hook);GC.KeepAlive(callback);}
}
