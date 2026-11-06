using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
namespace Lokum.Windows;

internal static class ContextClickGestureChecks
{
    internal static async Task ExplorerRowBounds(Action<bool,string> check)
    {
        var row=new ListBoxItem{Content="Source screenshot.png"};AutomationProperties.SetName(row,"Source screenshot.png");
        var list=new ListBox();list.Items.Add(row);
        var window=new Window{Title="Lokum file row fixture",Width=360,Height=160,ShowInTaskbar=false,ShowActivated=false,Content=list};window.Show();window.UpdateLayout();
        try
        {
            var handle=new WindowInteropHelper(window).Handle;
            var inside=row.PointToScreen(new Point(Math.Max(4,row.ActualWidth-12),row.ActualHeight/2));
            var outside=window.PointToScreen(new Point(window.ActualWidth+20,20));
            var result=await Task.Run(()=>ShellMediaSource.ItemNameAt(AutomationElement.FromHandle(handle),inside));
            check(result=="Source screenshot.png","A point in the file row beyond its filename cell still resolves the actual containing file item");
            check(await Task.Run(()=>ShellMediaSource.ItemNameAt(AutomationElement.FromHandle(handle),outside)) is null,"A point outside the file row never falls back to an unrelated selected item");
        }
        finally{window.Close();}
    }
    internal static async Task DelayedExplorerHint(Action<bool,string> check)
    {
        var hint=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=ContextMediaController.ReadItemNameAsync(hint.Task,CancellationToken.None);
        await Task.Delay(200);hint.SetResult("Source screenshot.png");
        check(await pending=="Source screenshot.png","An Explorer name lookup slower than the native-menu delay still opens tools for the clicked file");
        using var cancelled=new CancellationTokenSource();
        var unresolved=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped=ContextMediaController.ReadItemNameAsync(unresolved.Task,cancelled.Token);cancelled.Cancel();
        try{await stopped;check(false,"A canceled context click must stop awaiting its previous Explorer lookup");}
        catch(OperationCanceledException){check(true,"A newer or canceled click stops waiting for the previous Explorer lookup");}
        var menuClosed=false;
        var retryName=await ContextMediaController.ResolveItemNameAsync(Task.FromResult<string?>(null),()=>{menuClosed=true;return true;},()=>Task.FromResult<string?>(menuClosed?"Source screenshot.png":null),CancellationToken.None);
        check(retryName=="Source screenshot.png","An initial hit test obscured by the native context menu resolves the clicked item after dismissing that menu");
        var retried=false;
        var switched=await ContextMediaController.ResolveItemNameAsync(Task.FromResult<string?>(null),()=>false,()=>{retried=true;return Task.FromResult<string?>("Unrelated file.png");},CancellationToken.None);
        check(switched is null&&!retried,"Switching to another window prevents the context menu from reading an unrelated item on retry");
    }
    internal static void Run(Action<bool,string> check)
    {
        var gesture=new ContextClickGesture();
        check(gesture.Update(false,true,false,true)==ContextClickPhase.None,"Holding Ctrl without a secondary click does not open a download menu");
        check(gesture.Update(true,true,false,true)==ContextClickPhase.Pressed,"Ctrl plus secondary-button press captures a context click independently of dragging");
        check(gesture.Update(true,true,false,true)==ContextClickPhase.None,"Holding the secondary button cannot start duplicate context clicks");
        check(gesture.Update(false,false,false,true)==ContextClickPhase.Released,"Releasing the secondary button completes the captured click even if Ctrl was released first");
        check(gesture.Update(false,false,false,true)==ContextClickPhase.None,"One click completes only once");
        check(gesture.Update(true,false,false,true)==ContextClickPhase.None&&gesture.Update(false,true,false,true)==ContextClickPhase.None,"An ordinary right click cannot be armed retroactively by pressing Ctrl");
        check(gesture.Update(true,true,true,true)==ContextClickPhase.None&&gesture.Update(false,true,false,true)==ContextClickPhase.None,"Ctrl plus another modifier leaves the normal context menu alone");
        gesture.Update(true,true,false,true);gesture.Update(true,true,false,false);
        check(gesture.Update(false,true,false,true)==ContextClickPhase.None,"Disabling shortcuts cancels an in-progress click without reopening it on release");
    }
    internal static void OwnedPopup(Action<bool,string> check)
    {
        var source=new Window{Title="Lokum context source fixture",Width=240,Height=140,ShowInTaskbar=false,ShowActivated=false};source.Show();
        var popup=new Window{Title="Lokum owned context popup fixture",Owner=source,Width=160,Height=90,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false};
        var other=new Window{Title="Lokum unrelated window fixture",Width=160,Height=90,ShowInTaskbar=false,ShowActivated=false};
        try
        {
            popup.Show();
            var sourceHandle=new WindowInteropHelper(source).Handle;var popupHandle=new WindowInteropHelper(popup).Handle;
            check(BrowserContextMedia.IsSourceWindow(sourceHandle,popupHandle),"An owned browser context popup must not be mistaken for switching to another application");
            other.Show();
            check(!BrowserContextMedia.IsSourceWindow(sourceHandle,new WindowInteropHelper(other).Handle),"An unrelated window in the same process cancels a pending context menu without stealing focus");
        }
        finally{popup.Close();other.Close();source.Close();}
    }
}
