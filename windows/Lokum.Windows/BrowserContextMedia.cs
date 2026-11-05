using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
namespace Lokum.Windows;

internal static class BrowserContextMedia
{
    [DllImport("user32.dll")]private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")]private static extern nint GetClipboardOwner();
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint handle,out uint process);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern nint GetWindowLongPtr(nint handle,int index);
    private delegate bool WindowVisitor(nint window,nint argument);
    [DllImport("user32.dll")]private static extern bool EnumWindows(WindowVisitor visit,nint argument);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]private static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll")]private static extern nint SendMessageTimeout(nint window,uint message,nint w,nint l,uint flags,uint timeout,out nuint result);
    internal static bool IsSourceForeground(nint window)=>IsSourceWindow(window,GetForegroundWindow());
    internal static bool IsSourceWindow(nint window,nint candidate)
    {
        if(window==0)return false;
        for(var n=0;n<12&&candidate!=0;n++,candidate=GetWindow(candidate,4))if(candidate==window)return true;
        return false;
    }
    internal static void DismissContextMenu(nint window)
    {
        var foreground=GetForegroundWindow();if(!IsSourceWindow(window,foreground))return;
        if(foreground!=window)SendMessageTimeout(foreground,0x001F,0,0,2,150,out _);
        SendMessageTimeout(window,0x001F,0,0,2,150,out _);
    }
    internal static string? CommandKind(string name)
    {
        var normalized=new string(name.Replace("&","").Replace('ı','i').Replace('’','\'').Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant().Trim();
        return normalized switch
        {
            "copy image address" or "copy image link" or "resim adresini kopyala" or "resim baglantisini kopyala" or "gorsel adresini kopyala" or "goruntu adresini kopyala" or "goruntu baglantisini kopyala"=>"image",
            "copy video address" or "copy video link" or "copy video url" or "video adresini kopyala" or "video baglantisini kopyala" or "video url'sini kopyala"=>"video",
            "copy audio address" or "copy audio link" or "ses adresini kopyala" or "ses baglantisini kopyala"=>"audio",
            _=>null
        };
    }
    internal static async Task<MediaToolSource?> ReadAsync(int process,CancellationToken token,nint sourceWindow=0)
    {
        var command=await Task.Run(()=>FindCommand(process),token).WaitAsync(TimeSpan.FromSeconds(2),token);if(command is null)return null;
        token.ThrowIfCancellationRequested();if(sourceWindow!=0&&!IsSourceForeground(sourceWindow))return null;
        if(command.Value.Kind=="video"&&await KnownVideoPageAsync(sourceWindow,token) is {} known)return known;
        if(!command.Value.Item.Current.IsEnabled)return null;
        // Snapshot every original format before invoking Copy Address. If a
        // clipboard provider cannot be snapshotted, leave its contents untouched.
        var sequence=GetClipboardSequenceNumber();var before=await ClipboardSnapshot.CaptureAsync(token);if(GetClipboardSequenceNumber()!=sequence)return null;uint copied=0;
        try
        {
            await Task.Run(()=>((InvokePattern)command.Value.Item.GetCurrentPattern(InvokePattern.Pattern)).Invoke(),token);
            for(var n=0;n<30;n++)
            {
                var current=GetClipboardSequenceNumber();
                if(current!=sequence)
                {
                    GetWindowThreadProcessId(GetClipboardOwner(),out var clipboardProcess);
                    if(clipboardProcess!=process)return null;copied=current;
                    token.ThrowIfCancellationRequested();var text=Clipboard.ContainsText()?Clipboard.GetText():"";var media=MediaToolSource.Address(text,command.Value.Kind);
                    return media??(command.Value.Kind=="video"?await KnownVideoPageAsync(sourceWindow,token):null);
                }
                token.ThrowIfCancellationRequested();
                await Task.Delay(30,token);
            }
            return null;
        }
        finally{if(copied!=0&&GetClipboardSequenceNumber()==copied)before.Restore();}
    }
    internal static (AutomationElement Item,string Kind)? FindCommand(int process)
    {
        (AutomationElement Item,string Kind)? unavailable=null;
        var windows=new List<nint>();
        EnumWindows((window,_)=>{GetWindowThreadProcessId(window,out var owner);if(owner==process&&IsWindowVisible(window)&&((long)GetWindowLongPtr(window,-16)&0x80000000L)!=0)windows.Add(window);return true;},0);
        foreach(var window in windows)
        {
            // Native context menus are popup HWNDs. Never invoke a webpage's
            // arbitrary menu item merely because it imitates Copy Image Address.
            var root=AutomationElement.FromHandle(window);
            var items=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem));
            foreach(AutomationElement item in items)
            {
                if(item.Current.IsOffscreen||CommandKind(item.Current.Name) is not {} kind)continue;
                if(!item.Current.IsEnabled){unavailable??=(item,kind);continue;}
                if(item.TryGetCurrentPattern(InvokePattern.Pattern,out _))return(item,kind);
            }
        }
        return unavailable;
    }
    internal static MediaToolSource? KnownVideoAddress(string address)
    {
        if(!address.Contains("://",StringComparison.Ordinal))address="https://"+address;
        var payload=Lokum.Core.WebMediaPayload.Parse(address);
        return payload?.Kind is "youtube" or "instagramReel"?MediaToolSource.FromWeb(payload,"video"):null;
    }
    internal static Task<MediaToolSource?> KnownVideoPageAsync(nint window,CancellationToken token)=>Task.Run(()=>KnownVideoPage(window),token).WaitAsync(TimeSpan.FromSeconds(2),token);
    private static MediaToolSource? KnownVideoPage(nint window)
    {
        if(window==0)return null;var root=AutomationElement.FromHandle(window);
        var edits=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));
        foreach(AutomationElement edit in edits)
        {
            var current=edit.Current;if(current.IsPassword||current.IsOffscreen)continue;
            if(current.ClassName is not ("Chrome_OmniboxView" or "OmniboxViewViews")&&current.AutomationId!="urlbar-input"&&current.Name is not ("Address and search bar" or "Adres ve arama çubuğu" or "Adres çubuğu"))continue;
            var parent=TreeWalker.ControlViewWalker.GetParent(edit);var document=false;
            for(var n=0;n<12&&parent is not null;n++,parent=TreeWalker.ControlViewWalker.GetParent(parent))
            {if(parent.Current.ControlType==ControlType.Document){document=true;break;}if((nint)parent.Current.NativeWindowHandle==window)break;}
            if(document)continue;
            if(edit.TryGetCurrentPattern(ValuePattern.Pattern,out var pattern))return KnownVideoAddress(((ValuePattern)pattern).Current.Value);
        }
        return null;
    }
}

internal sealed class ClipboardSnapshot
{
    private readonly DataObject data=new();private bool empty=true;
    internal static Task<ClipboardSnapshot> CaptureAsync(CancellationToken token)
    {
        var completion=new TaskCompletionSource<ClipboardSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{completion.SetResult(Capture());}catch(Exception error){completion.SetException(error);}}){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task.WaitAsync(TimeSpan.FromSeconds(2),token);
    }
    internal static ClipboardSnapshot Capture(IDataObject? input=null)
    {
        var snapshot=new ClipboardSnapshot();var current=input??Clipboard.GetDataObject();if(current is null)return snapshot;
        foreach(var format in current.GetFormats(false))
        {
            var value=current.GetData(format,false);if(value is null)continue;
            object copied=value switch
            {
                string text=>text,string[] paths=>paths.ToArray(),byte[] bytes=>bytes.ToArray(),
                BitmapSource image=>CopyBitmap(image),MemoryStream memory=>new MemoryStream(memory.ToArray()),
                Stream stream when stream.CanSeek&&stream.Length<=64_000_000=>CopyStream(stream),
                _=>throw new InvalidOperationException("Clipboard is busy. Try again.")
            };
            snapshot.data.SetData(format,copied,false);snapshot.empty=false;
        }
        return snapshot;
    }
    private static MemoryStream CopyStream(Stream source){var position=source.Position;try{source.Position=0;var result=new MemoryStream();source.CopyTo(result);result.Position=0;return result;}finally{source.Position=position;}}
    private static BitmapSource CopyBitmap(BitmapSource source){var image=source.Clone();image.Freeze();return image;}
    internal void Restore(){if(empty)Clipboard.Clear();else Clipboard.SetDataObject(data,true);}
}
