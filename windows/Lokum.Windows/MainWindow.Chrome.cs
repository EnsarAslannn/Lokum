using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
namespace Lokum.Windows;

public sealed partial class MainWindow
{
    private ChromeMediaServer? chromeMedia;private WheelWindow? chromeWheel;
    [StructLayout(LayoutKind.Sequential)]private struct ChromePoint{public int X,Y;}
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out ChromePoint point);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    private void StartChromeMedia()
    {
        chromeMedia=new(ChromeMediaProtocol.PipeName,request=>Dispatcher.InvokeAsync(()=>ReceiveChromeMedia(request)).Task);
    }
    private ChromeMediaReply ReceiveChromeMedia(ChromeMediaRequest request)
    {
        if(!preferences.Value.Enabled)return new(false,L10n.Text("Enable Lokum shortcuts in Settings first."));
        if(busy||!DesktopGeometry.IsInteractive(this))return new(false,L10n.Text("Finish or cancel the current operation first."));
        GetWindowThreadProcessId(GetForegroundWindow(),out var id);
        try{using var process=Process.GetProcessById((int)id);if(process.ProcessName is not ("chrome" or "msedge"))return new(false,L10n.Text("Return to Chrome and try Ctrl + left-click again."));}
        catch(ArgumentException){return new(false,L10n.Text("Return to Chrome and try Ctrl + left-click again."));}
        var source=request.Validate().Source();GetCursorPos(out var point);chromeWheel?.Close();
        var wheel=new WheelWindow(null,source.Choices,choice=>_ = OpenMediaToolAsync(source,choice)){Topmost=true};chromeWheel=wheel;
        wheel.Closed+=(_,_)=>{if(ReferenceEquals(chromeWheel,wheel))chromeWheel=null;};wheel.Show();DesktopGeometry.PlaceWheel(wheel,point.X,point.Y,WheelPresentation.Diameter*preferences.Value.WheelScale);
        return new(true);
    }
}
