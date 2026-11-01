using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Lokum.Windows;

internal sealed class ColorPalette:StackPanel
{
    internal ColorPalette(TextBox field)
    {
        var spectrum=new ColorSpectrum{Height=140,Margin=new(4,8,4,4)};
        AutomationProperties.SetAutomationId(spectrum,"BackgroundColorSpectrum");
        L10n.Bind(spectrum,AutomationProperties.NameProperty,"Color palette");
        spectrum.ToolTip="Pick a color. Arrow keys adjust saturation and brightness.";
        Children.Add(spectrum);
        Children.Add(L10n.Label(new TextBlock{Text="Hue",Margin=new(4,6,4,0)}));
        var rainbow=new LinearGradientBrush{StartPoint=new(0,.5),EndPoint=new(1,.5)};
        for(var n=0;n<=6;n++)rainbow.GradientStops.Add(new(ColorSpectrum.Hsv(n*60,1,1),n/6d));
        Children.Add(new Border{Background=rainbow,Height=10,CornerRadius=new(5),Margin=new(4,5,4,0)});
        var hue=new Slider{Minimum=0,Maximum=360,SmallChange=1,LargeChange=30,Margin=new(4,3,4,8)};
        AutomationProperties.SetAutomationId(hue,"BackgroundHue");L10n.Bind(hue,AutomationProperties.NameProperty,"Hue");Children.Add(hue);
        var syncing=false;
        spectrum.Changed+=value=>{if(syncing)return;syncing=true;field.Text=$"#{value.R:X2}{value.G:X2}{value.B:X2}";syncing=false;};
        hue.ValueChanged+=(_,_)=>{if(!syncing)spectrum.SetHue(hue.Value);};
        void ReadColor()
        {
            if(syncing||!System.Text.RegularExpressions.Regex.IsMatch(field.Text,"^#[0-9a-fA-F]{6}$"))return;
            syncing=true;spectrum.SetColor((Color)ColorConverter.ConvertFromString(field.Text));hue.Value=spectrum.Hue;syncing=false;
        }
        field.TextChanged+=(_,_)=>ReadColor();ReadColor();
    }
}

internal sealed class ColorSpectrum:FrameworkElement
{
    internal event Action<Color>? Changed;
    internal double Hue{get;private set;}
    private double saturation=1,value=1;
    internal ColorSpectrum(){Focusable=true;Cursor=Cursors.Cross;MinWidth=100;}
    internal static Color Hsv(double hue,double saturation,double value)
    {
        hue=(hue%360+360)%360;var chroma=value*saturation;var x=chroma*(1-Math.Abs(hue/60%2-1));var m=value-chroma;
        var (r,g,b)=hue switch{<60=>(chroma,x,0d),<120=>(x,chroma,0d),<180=>(0d,chroma,x),<240=>(0d,x,chroma),<300=>(x,0d,chroma),_=>(chroma,0d,x)};
        return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
    }
    internal void SetColor(Color color)
    {
        var r=color.R/255d;var g=color.G/255d;var b=color.B/255d;var max=Math.Max(r,Math.Max(g,b));var min=Math.Min(r,Math.Min(g,b));var delta=max-min;
        if(delta>0)Hue=((max==r?(g-b)/delta:max==g?(b-r)/delta+2:(r-g)/delta+4)*60+360)%360;
        saturation=max==0?0:delta/max;value=max;InvalidateVisual();
    }
    internal void SetHue(double hue){Hue=hue;ChooseNormalized(saturation,value);}
    internal void ChooseNormalized(double s,double v){saturation=Math.Clamp(s,0,1);value=Math.Clamp(v,0,1);InvalidateVisual();Changed?.Invoke(Hsv(Hue,saturation,value));}
    protected override void OnRender(DrawingContext dc)
    {
        var bounds=new Rect(0,0,ActualWidth,ActualHeight);dc.DrawRectangle(new SolidColorBrush(Hsv(Hue,1,1)),null,bounds);
        dc.DrawRectangle(new LinearGradientBrush(Colors.White,Colors.Transparent,new Point(0,.5),new Point(1,.5)),null,bounds);
        dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent,Colors.Black,new Point(.5,0),new Point(.5,1)),null,bounds);
        var point=new Point(Math.Clamp(ActualWidth*saturation,5,Math.Max(5,ActualWidth-5)),Math.Clamp(ActualHeight*(1-value),5,Math.Max(5,ActualHeight-5)));
        dc.DrawEllipse(null,new Pen(Brushes.Black,3),point,5,5);dc.DrawEllipse(null,new Pen(Brushes.White,1.5),point,5,5);
        if(IsKeyboardFocused)dc.DrawRectangle(null,new Pen(Theme.Brush("Gold"),2),bounds);
    }
    private void Pick(Point p){if(ActualWidth>0&&ActualHeight>0)ChooseNormalized(p.X/ActualWidth,1-p.Y/ActualHeight);}
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);Focus();CaptureMouse();Pick(e.GetPosition(this));e.Handled=true;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(IsMouseCaptured)Pick(e.GetPosition(this));}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(IsMouseCaptured){Pick(e.GetPosition(this));ReleaseMouseCapture();}e.Handled=true;}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var step=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?.05:.01;
        switch(e.Key){case Key.Left:ChooseNormalized(saturation-step,value);break;case Key.Right:ChooseNormalized(saturation+step,value);break;case Key.Down:ChooseNormalized(saturation,value-step);break;case Key.Up:ChooseNormalized(saturation,value+step);break;default:base.OnKeyDown(e);return;}e.Handled=true;
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnGotKeyboardFocus(e);InvalidateVisual();}
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e){base.OnLostKeyboardFocus(e);InvalidateVisual();}
}
