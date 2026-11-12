using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using TextAnonymizer.Models;
using Wpf.Ui.Appearance;

namespace TextAnonymizer.Views.Rendering;

/// <summary>
/// Coloration syntaxique : choix de la définition AvalonEdit par format, adaptation de ses couleurs au thème,
/// et conversion de code en <see cref="Run"/> pour les blocs de code du rendu Markdown.
/// </summary>
public static class SyntaxPalette
{
    /// <summary>Couleurs d'origine des définitions (les définitions AvalonEdit sont partagées par toute l'application).</summary>
    private static readonly Dictionary<HighlightingColor, Color?> OriginalForegrounds = [];

    public static bool IsDark => ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

    /// <summary>Nom de la définition AvalonEdit utilisée pour l'éditeur de code d'un format (JSON utilise la grammaire JavaScript).</summary>
    public static string DefinitionName(ViewerFormat format) => format switch
    {
        ViewerFormat.Json => "JavaScript",
        ViewerFormat.Html => "HTML",
        ViewerFormat.Php => "PHP",
        _ => "MarkDown"
    };

    public static IHighlightingDefinition? ForFormat(ViewerFormat format) => Adapted(HighlightingManager.Instance.GetDefinition(DefinitionName(format)));

    /// <summary>Définition correspondant à l'étiquette d'un bloc de code Markdown (<c>```php</c>, <c>```js</c>…), ou <c>null</c>.</summary>
    public static IHighlightingDefinition? ForLanguage(string? language)
    {
        var name = language?.Trim().Split(' ', 2)[0].ToLowerInvariant();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        name = name switch
        {
            "json" or "js" or "javascript" or "ts" or "typescript" => "JavaScript",
            "html" or "htm" => "HTML",
            "xml" or "xaml" or "svg" => "XML",
            "cs" or "csharp" or "c#" => "C#",
            "c" or "cpp" or "c++" => "C++",
            "md" or "markdown" => "MarkDown",
            "ps" or "ps1" or "powershell" => "PowerShell",
            "vb" or "vbnet" => "VB",
            _ => name
        };

        var definition = HighlightingManager.Instance.GetDefinition(name)
                         ?? HighlightingManager.Instance.GetDefinitionByExtension("." + name);
        return Adapted(definition);
    }

    /// <summary>Éclaircit les couleurs en thème sombre, rétablit les originales en thème clair.</summary>
    public static IHighlightingDefinition? Adapted(IHighlightingDefinition? definition)
    {
        if (definition is null)
        {
            return null;
        }

        var dark = IsDark;
        foreach (var color in ColorsOf(definition))
        {
            if (!OriginalForegrounds.TryGetValue(color, out var original))
            {
                original = (color.Foreground?.GetBrush(null) as SolidColorBrush)?.Color;
                OriginalForegrounds[color] = original;
            }

            if (original is { } value && !color.IsFrozen)
            {
                color.Foreground = new SimpleHighlightingBrush(dark ? Lighten(value) : value);
            }
        }

        return definition;
    }

    /// <summary>Convertit du code en runs colorés (une chaîne simple si le langage est inconnu).</summary>
    public static IEnumerable<Inline> Highlight(string code, IHighlightingDefinition? definition)
    {
        if (definition is null)
        {
            yield return new Run(code);
            yield break;
        }

        var document = new TextDocument(code);
        using var highlighter = new DocumentHighlighter(document, definition);
        for (var number = 1; number <= document.LineCount; number++)
        {
            var line = document.GetLineByNumber(number);
            var highlighted = highlighter.HighlightLine(number);
            var position = line.Offset;
            foreach (var section in highlighted.Sections.OrderBy(s => s.Offset).ThenByDescending(s => s.Length))
            {
                if (section.Offset < position)
                {
                    continue;
                }

                if (section.Offset > position)
                {
                    yield return new Run(document.GetText(position, section.Offset - position));
                }

                yield return Colored(document.GetText(section.Offset, section.Length), section.Color);
                position = section.Offset + section.Length;
            }

            if (position < line.EndOffset)
            {
                yield return new Run(document.GetText(position, line.EndOffset - position));
            }

            if (number < document.LineCount)
            {
                yield return new LineBreak();
            }
        }
    }

    private static Run Colored(string text, HighlightingColor color)
    {
        var run = new Run(text);
        if (color.Foreground?.GetBrush(null) is { } brush)
        {
            run.Foreground = brush;
        }

        if (color.FontWeight is { } weight)
        {
            run.FontWeight = weight;
        }

        if (color.FontStyle is { } style)
        {
            run.FontStyle = style;
        }

        return run;
    }

    private static Color Lighten(Color color)
    {
        const double factor = 0.6;
        byte Mix(byte channel) => (byte)Math.Round(channel + ((255 - channel) * factor));
        return Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B));
    }

    private static IEnumerable<HighlightingColor> ColorsOf(IHighlightingDefinition definition)
    {
        var colors = new HashSet<HighlightingColor>();
        var visited = new HashSet<HighlightingRuleSet>();

        foreach (var named in definition.NamedHighlightingColors)
        {
            colors.Add(named);
        }

        Visit(definition.MainRuleSet);
        return colors;

        void Visit(HighlightingRuleSet? ruleSet)
        {
            if (ruleSet is null || !visited.Add(ruleSet))
            {
                return;
            }

            foreach (var rule in ruleSet.Rules)
            {
                AddColor(rule.Color);
            }

            foreach (var span in ruleSet.Spans)
            {
                AddColor(span.SpanColor);
                AddColor(span.StartColor);
                AddColor(span.EndColor);
                Visit(span.RuleSet);
            }
        }

        void AddColor(HighlightingColor? color)
        {
            if (color is not null)
            {
                colors.Add(color);
            }
        }
    }
}
