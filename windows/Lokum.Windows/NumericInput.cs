using System.Globalization;
namespace Lokum.Windows;

internal static class NumericInput
{
    internal static double ParseFinite(string text,CultureInfo? culture=null)
    {
        if(!double.TryParse(text,NumberStyles.Float|NumberStyles.AllowThousands,culture??CultureInfo.CurrentCulture,out var value)||!double.IsFinite(value))
            throw new ArgumentException("Enter a finite number.");
        return value;
    }
}
