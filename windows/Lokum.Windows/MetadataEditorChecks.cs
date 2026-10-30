using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lokum.Windows;

internal static class MetadataEditorChecks
{
    internal static Dictionary<string, string> Fixture(string source, bool populated)
    {
        var values = new Dictionary<string, string>
        {
            ["SourceFile"] = source, ["File:FileType"] = "PNG",
            ["System:FileSize"] = "42 kB", ["PNG:ImageWidth"] = "640", ["PNG:ImageHeight"] = "480"
        };
        if (populated) { values["EXIF:Artist"] = "Ensar Çığ"; values["XMP:Title"] = "Deneme başlığı"; }
        return values;
    }

    // Catches the original failure: blank optional inputs hide all the file's actual information.
    internal static void Verify(MetadataEditor editor, bool populated)
    {
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        bool InViewport(FrameworkElement element)
        {
            if (!element.IsVisible || element.ActualHeight <= 0) return false;
            var bounds = element.TransformToAncestor(editor).TransformBounds(new Rect(element.RenderSize));
            return bounds.Top >= 0 && bounds.Bottom <= editor.ActualHeight;
        }
        var texts = Descendants<TextBlock>(editor).Where(InViewport).Select(x => x.Text).ToArray();
        Check(texts.Any(x => x.Contains("PNG", StringComparison.Ordinal)), "PNG file information must be visible immediately, without expanding or scrolling.");
        Check(texts.Any(x => x.Contains("640", StringComparison.Ordinal) && x.Contains("480", StringComparison.Ordinal)), "Image dimensions must be visible when optional EXIF fields are absent.");
        var fields = Descendants<TextBox>(editor).ToArray();
        Check(!fields.Any(x => x.IsVisible && string.IsNullOrEmpty(x.Text)), "Empty optional metadata fields must be collapsed initially.");
        Check(editor.Updates.Count == 0, "Opening metadata must not create edits.");
        if (populated)
        {
            var artist = fields.Single(x => x.Text == "Ensar Çığ");
            Check(artist.IsVisible, "Existing metadata must be shown outside the add-fields section.");
            artist.Text = "Yeni yazar";
            Check(editor.Updates.Count == 1 && editor.Updates.GetValueOrDefault("EXIF:Artist") == "Yeni yazar", "Editing an existing author must preserve its actual EXIF tag.");
            artist.Text = "Ensar Çığ";
        }
        else
        {
            Check(texts.Any(x => x == L10n.Text("No editable tags are saved in this file. You can add a title, author or description below.")), "A tag-free screenshot needs an explanation instead of a blank form.");
            var add = Descendants<Expander>(editor).Single(x => Equals(x.Header, L10n.Text("Add metadata fields")));
            add.IsExpanded = true; editor.UpdateLayout();
            var title = Descendants<TextBox>(editor).Single(x => AutomationProperties.GetAutomationId(x) == "XMP:Title");
            Check(title.IsVisible && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(title)), "Added fields must be accessible by name.");
            title.Text = "Türkçe başlık";
            Check(editor.Updates.Count == 1 && editor.Updates.GetValueOrDefault("XMP:Title") == "Türkçe başlık", "A newly added title must be sent to the writer.");
            title.Text = ""; add.IsExpanded = false;
        }
        var remove = Descendants<CheckBox>(editor).Single();
        remove.IsChecked = true;
        Check(editor.RemoveAll && Descendants<TextBox>(editor).All(x => !x.IsEnabled), "Remove-all must disable editing both existing and optional fields.");
        remove.IsChecked = false;
        Check(!editor.RemoveAll && Descendants<TextBox>(editor).All(x => x.IsEnabled) && editor.Updates.Count == 0, "Unchecking remove-all must restore fields without phantom edits.");
        foreach (var scroll in Descendants<ScrollViewer>(editor)) scroll.ScrollToTop();
        editor.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
