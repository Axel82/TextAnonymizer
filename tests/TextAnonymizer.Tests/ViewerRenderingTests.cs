using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TextAnonymizer.Views.Rendering;

namespace TextAnonymizer.Tests;

public class ViewerRenderingTests
{
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Exception("Échec dans le thread STA", failure);
        }
    }

    private static string PlainText(TextBlock block) => new TextRange(block.ContentStart, block.ContentEnd).Text;

    [Fact]
    public void Markdown_TableAndCodeBlock_AreRendered()
    {
        RunSta(() =>
        {
            const string markdown = "# Titre\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```php\n<?php echo 1; ?>\n```\n\n**gras** et [lien](https://exemple.fr)\n";

            var document = MarkdownFlowDocumentBuilder.Build(markdown);

            var table = Assert.Single(document.Blocks.OfType<Table>());
            Assert.Equal(2, table.RowGroups[0].Rows.Count);
            Assert.Equal(2, table.RowGroups[0].Rows[1].Cells.Count);

            var code = document.Blocks.OfType<Paragraph>().First(p => p.FontFamily.Source == "Consolas");
            Assert.Contains("echo", new TextRange(code.ContentStart, code.ContentEnd).Text);
            Assert.True(code.Inlines.OfType<Run>().Count() > 1, "le code doit être découpé en runs colorés");

            var text = document.Blocks.OfType<Paragraph>().Last();
            Assert.Single(text.Inlines.OfType<Bold>());
            Assert.Single(text.Inlines.OfType<Hyperlink>());
        });
    }

    [Fact]
    public void Markdown_HtmlAndJavascriptLinks_AreNotInterpreted()
    {
        RunSta(() =>
        {
            var document = MarkdownFlowDocumentBuilder.Build("<script>alert(1)</script>\n\n[x](javascript:alert(1))");

            Assert.Empty(document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<Hyperlink>()));
            var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
            Assert.Contains("<script>", text);
        });
    }

    [Fact]
    public void JsonTree_ExpandsLazilyAndShowsTypes()
    {
        RunSta(() =>
        {
            using var json = JsonDocument.Parse("{\"nom\":\"Ada\",\"age\":36,\"ok\":true,\"x\":null,\"liste\":[1,2]}");

            var root = JsonTreeBuilder.Build(json.RootElement);

            Assert.Equal(5, root.Items.Count); // premier niveau déplié d'emblée
            var headers = root.Items.OfType<TreeViewItem>().Select(i => PlainText((TextBlock)i.Header)).ToList();
            Assert.Contains(headers, h => h.Contains("\"nom\"") && h.Contains("string"));
            Assert.Contains(headers, h => h.Contains("36") && h.Contains("number"));
            Assert.Contains(headers, h => h.Contains("true") && h.Contains("boolean"));
            Assert.Contains(headers, h => h.Contains("null"));

            var list = root.Items.OfType<TreeViewItem>().Last();
            Assert.Single(list.Items); // marqueur de chargement paresseux
            list.RaiseEvent(new RoutedEventArgs(TreeViewItem.ExpandedEvent, list));
            Assert.Equal(2, list.Items.Count);
        });
    }
}
