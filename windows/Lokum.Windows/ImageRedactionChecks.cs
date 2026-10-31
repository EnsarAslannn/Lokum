using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Lokum.Windows;

// Exercises the production editor and exporter using private, deterministic WPF fixtures.
// Mouse completion is called in-process; this does not claim physical mouse/keyboard coverage.
internal static class ImageRedactionChecks
{
    internal static void Verify(ToolEditor editor, BitmapSource original, string input, string output)
    {
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        var style = Descendants<ComboBox>(editor).Single(x => x.Items.Contains("pixelate"));
        Check(Equals(style.SelectedItem, "blur"), "Image redaction must open with blur selected rather than a black block.");
        var canvas = (Canvas)typeof(ToolEditor).GetField("drawing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
        BitmapSource Preview() => (BitmapSource)((Image)canvas.Children[0]).Source;
        var region = new Rect(300, 40, 160, 180);
        var source = Pixels(original);
        var hash = SHA256.HashData(File.ReadAllBytes(input));
        var expected = ImageEngine.Redact(original, [region], "blur");
        var blurred = Pixels(expected);
        var changed = 0; var outside = true; var nonBlack = false;
        for (var y = 0; y < original.PixelHeight; y++)
        for (var x = 0; x < original.PixelWidth; x++)
        {
            var offset = (y * original.PixelWidth + x) * 4;
            var differs = !source.AsSpan(offset, 4).SequenceEqual(blurred.AsSpan(offset, 4));
            if (region.Contains(new Point(x + .5, y + .5))) { if (differs) changed++; nonBlack |= blurred[offset] + blurred[offset + 1] + blurred[offset + 2] > 0; }
            else outside &= !differs;
        }
        Check(changed > 100 && nonBlack && outside, "Blur must change selected pixels without drawing a black band or changing pixels outside the region.");
        Check(EdgeEnergy(blurred, original.PixelWidth, region) < EdgeEnergy(source, original.PixelWidth, region) * .6, "Redaction must smooth visible detail inside the selection.");
        // A 24-pixel blur must spread across a broad edge, not merely soften its first pixel.
        var sample = (150 * original.PixelWidth + 315) * 4;
        var spread = Enumerable.Range(0, 3).Sum(c => Math.Abs(source[sample + c] - blurred[sample + c]));
        if (spread < 20) ImageEngine.Save(expected, Path.Combine(output, "blur-strength-failure.png"), "png");
        Check(spread >= 20, "Redaction blur is too weak: a broad color edge must soften visibly away from the boundary. Pixel spread=" + spread);
        CompleteRegion(editor, canvas, region);
        Check(Wait(() => Pixels(Preview()).SequenceEqual(blurred)), "Completing a selection must display the same blur that will be exported.");
        Check(canvas.Children.OfType<Rectangle>().All(x => x.Fill is not SolidColorBrush brush || brush.Color.A == 0), "A black selection overlay must not hide the blurred preview.");
        ReadSettings(editor);
        Check(editor.ImageOptions.RedactionStyle == "blur" && editor.ImageOptions.Redactions.SequenceEqual(new[] { region }), "The editor must send blur and the selected source coordinates to the exporter.");
        var exported = ImageEngine.Apply("redactImage", [input], output, editor.ImageOptions).Single();
        Check(Pixels(ImageEngine.Load(exported)).SequenceEqual(blurred), "The actual saved PNG must match the blurred preview.");
        var defaultExport = ImageEngine.Apply("redactImage", [input], output, new() { Redactions = [region] }).Single();
        Check(Pixels(ImageEngine.Load(defaultExport)).SequenceEqual(blurred), "Image exports must default to blur when no style is provided.");
        Check(SHA256.HashData(File.ReadAllBytes(input)).SequenceEqual(hash), "Redaction must preserve the original file.");
        style.SelectedItem = "solid";
        var solid = Pixels(ImageEngine.Redact(original, [region], "solid"));
        Check(Wait(() => Pixels(Preview()).SequenceEqual(solid)), "Changing the cover style must refresh existing regions.");
        style.SelectedItem = "pixelate";
        var pixelated = Pixels(ImageEngine.Redact(original, [region], "pixelate"));
        Check(Wait(() => Pixels(Preview()).SequenceEqual(pixelated)), "Pixelation must also preview the selected style.");
        style.SelectedItem = "blur";
        CompleteRegion(editor, canvas, new Rect(100, 100, 130, 160));
        var two = Pixels(ImageEngine.Redact(original, [region, new Rect(100, 100, 130, 160)], "blur"));
        Check(Wait(() => Pixels(Preview()).SequenceEqual(two)), "Multiple selected areas must all be blurred.");
        Descendants<Button>(editor).Single(x => Equals(x.Content, L10n.Text("Undo area"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Wait(() => Pixels(Preview()).SequenceEqual(blurred)), "Undo must restore the removed area and keep previous blur regions.");
        Descendants<Button>(editor).Single(x => Equals(x.Content, L10n.Text("↶ Reset"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Wait(() => Pixels(Preview()).SequenceEqual(source)) && Equals(style.SelectedItem, "blur"), "Reset must restore original pixels and the default blur style.");
        ReadSettings(editor);
        Check(editor.ImageOptions.Redactions.Length == 0, "Reset must also clear export regions.");
        CompleteRegion(editor, canvas, region);
        Check(Wait(() => Pixels(Preview()).SequenceEqual(blurred)), "A new selection after reset must preview normally.");
    }

    private static void CompleteRegion(ToolEditor editor, Canvas canvas, Rect region)
    {
        var rectangle = new Rectangle { Width = region.Width, Height = region.Height, Fill = Brushes.Black };
        Canvas.SetLeft(rectangle, region.X); Canvas.SetTop(rectangle, region.Y); canvas.Children.Add(rectangle);
        typeof(ToolEditor).GetField("activeRectangle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor, rectangle);
        typeof(ToolEditor).GetMethod("EndDraw", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor,
            [canvas, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent }]);
    }
    private static void ReadSettings(ToolEditor editor) => typeof(ToolEditor).GetMethod("ReadSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, [false]);
    private static byte[] Pixels(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; converted.CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes;
    }
    private static long EdgeEnergy(byte[] pixels, int width, Rect region)
    {
        long result = 0;
        for (var y = (int)region.Y + 1; y < region.Bottom - 1; y++)
        for (var x = (int)region.X + 1; x < region.Right - 1; x++)
        for (var c = 0; c < 3; c++)
        { var i = (y * width + x) * 4 + c; var dx = pixels[i] - pixels[i - 4]; var dy = pixels[i] - pixels[i - width * 4]; result += dx * dx + dy * dy; }
        return result;
    }
    private static bool Wait(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (condition()) return true;
            var frame = new DispatcherFrame(); var tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            tick.Tick += (_, _) => { tick.Stop(); frame.Continue = false; }; tick.Start(); Dispatcher.PushFrame(frame);
        }
        return condition();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}
