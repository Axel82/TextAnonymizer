using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;

using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TextAnonymizer.Views.Rendering;

/// <summary>
/// Convertit un document Markdown (analysé par Markdig) en <see cref="FlowDocument"/> natif.
/// Aucun HTML n'est interprété : le HTML en ligne est affiché comme du texte, ce qui exclut toute injection.
/// </summary>
public static class MarkdownFlowDocumentBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly string[] AllowedLinkSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto];

    public static FlowDocument Build(string markdown)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            PagePadding = new Thickness(4),
            ColumnWidth = double.PositiveInfinity
        };
        document.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");

        AppendBlocks(document.Blocks, Markdown.Parse(markdown, Pipeline));
        return document;
    }

    private static void AppendBlocks(BlockCollection target, ContainerBlock container, bool tight = false)
    {
        foreach (var block in container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    target.Add(CreateHeading(heading));
                    break;
                case ParagraphBlock paragraph:
                    var created = new Paragraph { Margin = tight ? new Thickness(0) : new Thickness(0, 0, 0, 10) };
                    AppendInlines(created.Inlines, paragraph.Inline);
                    target.Add(created);
                    break;
                case FencedCodeBlock or CodeBlock:
                    target.Add(CreateCode((LeafBlock)block));
                    break;
                case QuoteBlock quote:
                    var section = new Section
                    {
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        Padding = new Thickness(10, 0, 0, 0),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    section.SetResourceReference(System.Windows.Documents.Block.BorderBrushProperty, "ControlStrongStrokeColorDefaultBrush");
                    section.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");
                    AppendBlocks(section.Blocks, quote);
                    target.Add(section);
                    break;
                case ListBlock list:
                    target.Add(CreateList(list));
                    break;
                case Markdig.Extensions.Tables.Table table:
                    target.Add(CreateTable(table));
                    break;
                case ThematicBreakBlock:
                    var rule = new Border { Height = 1, Margin = new Thickness(0, 6, 0, 16) };
                    rule.SetResourceReference(Border.BackgroundProperty, "ControlStrongStrokeColorDefaultBrush");
                    target.Add(new BlockUIContainer(rule));
                    break;
                case HtmlBlock html:
                    target.Add(CreateLiteralBlock(html.Lines.ToString()));
                    break;
                case ContainerBlock inner:
                    AppendBlocks(target, inner, tight);
                    break;
                case LeafBlock leaf when leaf.Inline is not null:
                    var fallback = new Paragraph();
                    AppendInlines(fallback.Inlines, leaf.Inline);
                    target.Add(fallback);
                    break;
            }
        }
    }

    private static Paragraph CreateHeading(HeadingBlock heading)
    {
        var size = heading.Level switch { 1 => 30.0, 2 => 24.0, 3 => 20.0, 4 => 17.0, _ => 15.0 };
        var paragraph = new Paragraph
        {
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, heading.Level <= 2 ? 12 : 8, 0, 8)
        };
        AppendInlines(paragraph.Inlines, heading.Inline);
        return paragraph;
    }

    private static Paragraph CreateCode(LeafBlock block)
    {
        var language = (block as FencedCodeBlock)?.Info;
        var paragraph = new Paragraph
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10)
        };
        paragraph.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
        paragraph.Inlines.AddRange(SyntaxPalette.Highlight(block.Lines.ToString(), SyntaxPalette.ForLanguage(language)));
        return paragraph;
    }

    private static Paragraph CreateLiteralBlock(string text)
    {
        var paragraph = new Paragraph(new Run(text)) { FontFamily = new FontFamily("Consolas"), FontSize = 13 };
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");
        return paragraph;
    }

    private static List CreateList(ListBlock list)
    {
        var result = new List
        {
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(24, 0, 0, 0)
        };

        if (list.IsOrdered && int.TryParse(list.OrderedStart, out var start))
        {
            result.StartIndex = start;
        }

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var listItem = new ListItem();
            AppendBlocks(listItem.Blocks, item, !list.IsLoose);
            result.ListItems.Add(listItem);
        }

        return result;
    }

    private static System.Windows.Documents.Table CreateTable(Markdig.Extensions.Tables.Table markdownTable)
    {
        var table = new System.Windows.Documents.Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 10),
            BorderThickness = new Thickness(1, 1, 0, 0)
        };
        table.SetResourceReference(System.Windows.Documents.Block.BorderBrushProperty, "ControlStrongStrokeColorDefaultBrush");

        var columnCount = markdownTable.ColumnDefinitions.Count;
        for (var i = 0; i < columnCount; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        foreach (var markdownRow in markdownTable.OfType<Markdig.Extensions.Tables.TableRow>())
        {
            var row = new System.Windows.Documents.TableRow();
            if (markdownRow.IsHeader)
            {
                row.FontWeight = FontWeights.SemiBold;
                row.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
            }

            var columnIndex = 0;
            foreach (var markdownCell in markdownRow.OfType<Markdig.Extensions.Tables.TableCell>())
            {
                var cell = new System.Windows.Documents.TableCell
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    ColumnSpan = Math.Max(1, markdownCell.ColumnSpan)
                };
                cell.SetResourceReference(System.Windows.Documents.Block.BorderBrushProperty, "ControlStrongStrokeColorDefaultBrush");
                AppendBlocks(cell.Blocks, markdownCell, tight: true);

                var alignment = columnIndex < columnCount ? markdownTable.ColumnDefinitions[columnIndex].Alignment : null;
                foreach (var paragraph in cell.Blocks.OfType<Paragraph>())
                {
                    paragraph.TextAlignment = alignment switch
                    {
                        Markdig.Extensions.Tables.TableColumnAlign.Center => TextAlignment.Center,
                        Markdig.Extensions.Tables.TableColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left
                    };
                }

                row.Cells.Add(cell);
                columnIndex += cell.ColumnSpan;
            }

            group.Rows.Add(row);
        }

        return table;
    }

    private static void AppendInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    target.Add(new Run(literal.Content.ToString()));
                    break;
                case LineBreakInline lineBreak:
                    target.Add(lineBreak.IsHard ? new LineBreak() : new Run(" "));
                    break;
                case CodeInline code:
                    var run = new Run(code.Content) { FontFamily = new FontFamily("Consolas") };
                    run.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
                    target.Add(run);
                    break;
                case HtmlEntityInline entity:
                    target.Add(new Run(entity.Transcoded.ToString()));
                    break;
                case HtmlInline html:
                    target.Add(new Run(html.Tag));
                    break;
                case TaskList task:
                    target.Add(new Run(task.Checked ? "☑ " : "☐ "));
                    break;
                case AutolinkInline auto:
                    target.Add(CreateLink(auto.Url, link => link.Inlines.Add(new Run(auto.Url))));
                    break;
                case LinkInline { IsImage: true } image:
                    target.Add(CreateImage(image));
                    break;
                case LinkInline link:
                    target.Add(CreateLink(link.Url, hyperlink => AppendInlines(hyperlink.Inlines, link), link.Title));
                    break;
                case EmphasisInline emphasis:
                    var span = CreateEmphasis(emphasis);
                    AppendInlines(span.Inlines, emphasis);
                    target.Add(span);
                    break;
                case ContainerInline nested:
                    AppendInlines(target, nested);
                    break;
            }
        }
    }

    private static Span CreateEmphasis(EmphasisInline emphasis)
    {
        switch (emphasis.DelimiterChar, emphasis.DelimiterCount)
        {
            case ('~', 2):
                return new Span { TextDecorations = TextDecorations.Strikethrough };
            case ('+', 2):
                return new Span { TextDecorations = TextDecorations.Underline };
            case ('=', 2):
                return new Span { Background = Brushes.Gold, Foreground = Brushes.Black };
            case ('~', 1):
            case ('^', 1):
                return new Span { BaselineAlignment = emphasis.DelimiterChar == '^' ? BaselineAlignment.Superscript : BaselineAlignment.Subscript, FontSize = 11 };
            case (_, 1):
                return new Italic();
            case (_, 2):
                return new Bold();
            default:
                return new Bold(new Italic());
        }
    }

    /// <summary>Lien cliquable pour http, https et mailto ; toute autre cible (javascript:, file:…) reste du texte.</summary>
    private static System.Windows.Documents.Inline CreateLink(string? url, Action<Span> fillContent, string? title = null)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && AllowedLinkSchemes.Contains(uri.Scheme))
        {
            var hyperlink = new Hyperlink { NavigateUri = uri, ToolTip = title ?? url };
            fillContent(hyperlink);
            return hyperlink;
        }

        var span = new Span();
        fillContent(span);
        return span;
    }

    /// <summary>Image http(s) ou <c>data:image/…;base64</c> ; sinon, le texte alternatif.</summary>
    private static System.Windows.Documents.Inline CreateImage(LinkInline image)
    {
        var alt = string.Concat(image.OfType<LiteralInline>().Select(l => l.Content.ToString()));
        var source = LoadImage(image.Url);
        if (source is null)
        {
            return new Run($"[image : {(alt.Length > 0 ? alt : image.Url)}]");
        }

        var control = new Image { Source = source, MaxWidth = 480, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, ToolTip = alt };
        return new InlineUIContainer(control);
    }

    private static BitmapImage? LoadImage(string? url)
    {
        try
        {
            var bitmap = new BitmapImage();
            if (url is not null && url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var comma = url.IndexOf(',');
                if (comma < 0 || !url[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var bytes = Convert.FromBase64String(url[(comma + 1)..]);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = new MemoryStream(bytes);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.EndInit();
                return bitmap;
            }
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or IOException or ArgumentException)
        {
            return null;
        }

        return null;
    }
}
