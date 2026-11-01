using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Lokum.Core;

namespace Lokum.Windows;

internal enum ForegroundSelectionShape { Freehand,Rectangle }

internal sealed class ForegroundSelectionSurface:Canvas
{
    private readonly Image image=new(){IsHitTestVisible=false};
    private readonly List<ForegroundRegion> regions=[];
    private readonly List<Point> gesture=[];
    private Polyline? outline;
    internal BitmapSource? SourceBitmap=>image.Source as BitmapSource;
    internal ForegroundSelectionShape Shape{get;set;}
    internal bool Subtract{get;set;}
    internal bool IsDrawing=>outline is not null;
    internal ForegroundRegion[] Regions=>regions.Select(r=>new ForegroundRegion((ForegroundPoint[])r.Points.Clone(),r.Subtract)).ToArray();
    internal event Action? SelectionChanged;

    internal ForegroundSelectionSurface()
    {
        Background=Brushes.Transparent;Focusable=true;Cursor=Cursors.Cross;Children.Add(image);
        AutomationProperties.SetAutomationId(this,"ForegroundSelectionCanvas");
        L10n.Bind(this,AutomationProperties.NameProperty,"Select the foreground to keep");
        MouseLeftButtonDown+=(_,e)=>{Focus();if(CaptureMouse())BeginSelection(e.GetPosition(this));e.Handled=true;};
        MouseMove+=(_,e)=>{if(IsDrawing)ContinueSelection(e.GetPosition(this));};
        MouseLeftButtonUp+=(_,e)=>{if(IsDrawing){ContinueSelection(e.GetPosition(this));FinishSelection();}ReleaseMouseCapture();e.Handled=true;};
        LostMouseCapture+=(_,_)=>CancelSelection();
    }

    internal void Load(BitmapSource bitmap,IReadOnlyList<ForegroundRegion> saved)
    {
        CancelSelection();image.Source=bitmap;Width=image.Width=bitmap.PixelWidth;Height=image.Height=bitmap.PixelHeight;
        regions.Clear();regions.AddRange(saved);Redraw();
    }
    internal void SelectAll()=>Replace([new([new(0,0),new(1,0),new(1,1),new(0,1)])]);
    internal void Clear()=>Replace([]);
    internal void Undo(){if(regions.Count==0)return;regions.RemoveAt(regions.Count-1);Redraw();SelectionChanged?.Invoke();}
    private void Replace(IEnumerable<ForegroundRegion> values){CancelSelection();regions.Clear();regions.AddRange(values);Redraw();SelectionChanged?.Invoke();}
    internal void BeginSelection(Point point)
    {
        if(SourceBitmap is null)return;CancelSelection();gesture.Add(Clamp(point));
        outline=new Polyline{Stroke=Theme.Brush(Subtract?"Gold":"Muted"),StrokeThickness=Math.Max(2,Width/350),IsHitTestVisible=false};
        outline.Points.Add(gesture[0]);Children.Add(outline);
    }
    internal void ContinueSelection(Point point)
    {
        if(outline is null)return;point=Clamp(point);
        if(Shape==ForegroundSelectionShape.Rectangle)
        {
            var first=gesture[0];gesture.Clear();gesture.AddRange([first,new(point.X,first.Y),point,new(first.X,point.Y)]);
            outline.Points=new PointCollection(gesture.Concat([first]));
        }
        else if((point-gesture[^1]).Length>=Math.Max(1,Width/1500))
        {
            if(gesture.Count>=4094){var reduced=gesture.Where((_,i)=>i%2==0).ToArray();gesture.Clear();gesture.AddRange(reduced);}
            gesture.Add(point);outline.Points.Add(point);
        }
    }
    internal void FinishSelection()
    {
        if(outline is null)return;
        var points=gesture.Select(p=>new ForegroundPoint(p.X/Width,p.Y/Height)).ToArray();
        var valid=points.Length>=3&&gesture.Max(p=>p.X)-gesture.Min(p=>p.X)>=1&&gesture.Max(p=>p.Y)-gesture.Min(p=>p.Y)>=1;
        CancelSelection();if(!valid)return;
        regions.Add(new(points,Subtract));Redraw();SelectionChanged?.Invoke();
    }
    internal void CancelSelection(){if(outline is not null)Children.Remove(outline);outline=null;gesture.Clear();}
    private Point Clamp(Point point)=>new(Math.Clamp(point.X,0,Width),Math.Clamp(point.Y,0,Height));
    private void Redraw()
    {
        while(Children.Count>1)Children.RemoveAt(Children.Count-1);
        foreach(var region in regions)
        {
            var polygon=new Polygon{Points=new PointCollection(region.Points.Select(p=>new Point(p.X*Width,p.Y*Height))),
                Stroke=Theme.Brush(region.Subtract?"Gold":"Muted"),StrokeThickness=Math.Max(2,Width/350),
                Fill=new SolidColorBrush(region.Subtract?Color.FromArgb(50,182,63,104):Color.FromArgb(35,74,136,93)),IsHitTestVisible=false};
            Children.Add(polygon);
        }
    }
}
