namespace Lokum.Windows;

internal enum ContextClickPhase { None, Pressed, Released }

internal sealed class ContextClickGesture
{
    private bool down,armed;
    internal ContextClickPhase Update(bool secondary,bool control,bool otherModifier,bool enabled)
    {
        if(!enabled)armed=false;
        if(secondary==down)return ContextClickPhase.None;
        down=secondary;
        if(secondary){armed=enabled&&control&&!otherModifier;return armed?ContextClickPhase.Pressed:ContextClickPhase.None;}
        var complete=armed&&enabled;armed=false;return complete?ContextClickPhase.Released:ContextClickPhase.None;
    }
}
