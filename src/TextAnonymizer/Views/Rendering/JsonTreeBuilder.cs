using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TextAnonymizer.Views.Rendering;

/// <summary>
/// Construit l'arborescence interactive d'un JSON. Les enfants d'un nœud ne sont créés qu'à son dépliage,
/// pour rester fluide sur de gros documents.
/// </summary>
public static class JsonTreeBuilder
{
    private const int MaxDisplayedLength = 300;

    /// <summary>Racine de l'arbre, dépliée sur son premier niveau.</summary>
    public static TreeViewItem Build(JsonElement root)
    {
        var item = CreateItem(null, root);
        item.IsExpanded = true;
        return item;
    }

    private static TreeViewItem CreateItem(string? key, JsonElement element)
    {
        var item = new TreeViewItem { Header = CreateHeader(key, element), Tag = element };
        if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array && Count(element) > 0)
        {
            item.Items.Add(new TreeViewItem());
            item.Expanded += OnExpanded;
        }

        return item;
    }

    private static void OnExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item || e.OriginalSource != item || item.Tag is not JsonElement element)
        {
            return;
        }

        item.Expanded -= OnExpanded;
        item.Items.Clear();
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                item.Items.Add(CreateItem(property.Name, property.Value));
            }
        }
        else
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
            {
                item.Items.Add(CreateItem($"[{index++}]", child));
            }
        }
    }

    private static int Count(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? element.EnumerateObject().Count() : element.GetArrayLength();

    private static TextBlock CreateHeader(string? key, JsonElement element)
    {
        var dark = SyntaxPalette.IsDark;
        var header = new TextBlock { FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };

        if (key is not null)
        {
            header.Inlines.Add(Run(key.StartsWith('[') ? key : $"\"{key}\"", dark ? "#9CDCFE" : "#0451A5"));
            header.Inlines.Add(Run(": ", dark ? "#CCCCCC" : "#555555"));
        }

        var (text, color, type) = element.ValueKind switch
        {
            JsonValueKind.Object => ($"{{{Count(element)}}}", dark ? "#CCCCCC" : "#555555", "object"),
            JsonValueKind.Array => ($"[{Count(element)}]", dark ? "#CCCCCC" : "#555555", "array"),
            JsonValueKind.String => (Quote(element.GetString() ?? string.Empty), dark ? "#CE9178" : "#A31515", "string"),
            JsonValueKind.Number => (element.GetRawText(), dark ? "#B5CEA8" : "#098658", "number"),
            JsonValueKind.True => ("true", dark ? "#569CD6" : "#0000FF", "boolean"),
            JsonValueKind.False => ("false", dark ? "#569CD6" : "#0000FF", "boolean"),
            _ => ("null", dark ? "#808080" : "#7A7A7A", "null")
        };

        header.Inlines.Add(Run(text, color));
        var badge = Run($"  {type}", dark ? "#808080" : "#8A8A8A");
        badge.FontSize = 11;
        badge.FontFamily = new FontFamily("Segoe UI");
        header.Inlines.Add(badge);
        return header;
    }

    private static string Quote(string value)
    {
        var shown = value.Length > MaxDisplayedLength ? value[..MaxDisplayedLength] + "…" : value;
        return "\"" + shown.ReplaceLineEndings("\\n") + "\"";
    }

    private static System.Windows.Documents.Run Run(string text, string color) =>
        new(text) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) };
}
