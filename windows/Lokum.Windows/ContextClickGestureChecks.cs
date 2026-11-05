using System.Windows;
using System.Windows.Interop;
namespace Lokum.Windows;

internal static class ContextClickGestureChecks
{
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
