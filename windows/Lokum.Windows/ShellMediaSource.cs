using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
namespace Lokum.Windows;

internal static class ShellMediaSource
{
    internal static string? ItemNameFromElement(AutomationElement element,AutomationElement? root,Point point)
    {
        for(var n=0;n<6&&element is not null;n++,element=TreeWalker.ControlViewWalker.GetParent(element))
        {
            if(element.Current.IsPassword)return null;
            if(element.Current.ControlType==ControlType.Edit)break;
            if(element.Current.ControlType==ControlType.ListItem||element.Current.ControlType==ControlType.DataItem)return element.Current.Name;
        }
        return root is null?null:ItemNameAt(root,point);
    }
    internal static string? ItemNameAt(AutomationElement root,Point point)
    {
        var items=root.FindAll(TreeScope.Descendants,new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem),
            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.DataItem),
            new PropertyCondition(AutomationElement.IsSelectionItemPatternAvailableProperty,true)));
        var matches=new List<string>();
        foreach(AutomationElement item in items)
        {
            try
            {
                var current=item.Current;
                if(current.IsOffscreen||current.IsPassword||current.BoundingRectangle.IsEmpty||!current.BoundingRectangle.Contains(point)||string.IsNullOrWhiteSpace(current.Name))continue;
                matches.Add(current.Name);if(matches.Count>1)return null;
            }
            catch(ElementNotAvailableException){}
        }
        System.Diagnostics.Trace.WriteLine("Explorer row bounds matches: "+matches.Count);
        return matches.Count==1?matches[0]:null;
    }
    internal static Task<MediaToolSource?> AtAsync(nint window,string? name,CancellationToken token)
    {
        var completion=new TaskCompletionSource<MediaToolSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try{token.ThrowIfCancellationRequested();completion.SetResult(Read(window,name));}
            catch(Exception error){completion.SetException(error);}
        }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task.WaitAsync(token);
    }
    internal static string? ItemAt(ContextMediaController.Point point,nint sourceWindow=0)
    {
        try
        {
            if(sourceWindow!=0)
            {
                var type=new System.Text.StringBuilder(64);GetClassName(sourceWindow,type,type.Capacity);
                if(type.ToString() is not ("CabinetWClass" or "Progman" or "WorkerW"))return null;
            }
            var element=AutomationElement.FromPoint(new Point(point.X,point.Y));
            System.Diagnostics.Trace.WriteLine("Explorer hit control: "+element.Current.ControlType.ProgrammaticName);
            return ItemNameFromElement(element,sourceWindow==0?null:AutomationElement.FromHandle(sourceWindow),new Point(point.X,point.Y));
        }
        catch(Exception error)when(error is InvalidOperationException or COMException){}
        return null;
    }
    private static MediaToolSource? Read(nint window,string? name)
    {
        var names=name is null?new List<string>():[name];
        var com=new List<object>();object Track(object value){if(value is not null)com.Add(value);return value!;}
        try
        {
            dynamic shell=Track(Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!);dynamic windows=Track(shell.Windows());
            for(var n=0;n<(int)windows.Count;n++)
            {
                dynamic browser=Track(windows.Item(n));if(browser is null||(nint)(long)browser.HWND!=window)continue;
                dynamic document=Track(browser.Document);dynamic selected=Track(document.SelectedItems());
                var target=Match((object)selected,names,Track,allowSingle:true);if(target is not null)return target;
                dynamic folder=Track(document.Folder);dynamic items=Track(folder.Items());return Match((object)items,names,Track);
            }
            var type=new System.Text.StringBuilder(64);GetClassName(window,type,type.Capacity);
            if(type.ToString() is not ("Progman" or "WorkerW"))return null;
            object location=0,unused=0;int desktopHandle;
            dynamic desktopWindow=Track(windows.FindWindowSW(ref location,ref unused,8,out desktopHandle,1));
            dynamic view=Track(desktopWindow.Document);dynamic desktopSelected=Track(view.SelectedItems());
            return Match((object)desktopSelected,names,Track,allowSingle:true);
        }
        finally{for(var n=com.Count-1;n>=0;n--)if(Marshal.IsComObject(com[n]))Marshal.ReleaseComObject(com[n]);}
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(nint window,System.Text.StringBuilder text,int count);
    private static MediaToolSource? Match(object collection,List<string> names,Func<object,object> track,bool allowSingle=false)
    {
        dynamic items=collection;
        var matches=new List<string>();
        for(var n=0;n<(int)items.Count;n++)
        {
            dynamic item=track(items.Item(n));string name=item.Name;string path=item.Path;
            if((allowSingle&&names.Count==0&&(int)items.Count==1)||names.Any(value=>string.Equals(value,name,StringComparison.OrdinalIgnoreCase)||string.Equals(value,Path.GetFileName(path),StringComparison.OrdinalIgnoreCase)))matches.Add(path);
        }
        return matches.Distinct(StringComparer.OrdinalIgnoreCase).Count()==1?MediaToolSource.Local(matches):null;
    }
}
