using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Tests;

public class DiffExporterTests
{
    /// <summary>Applique un patch unifié à un texte (implémentation de référence minimale, contexte vérifié).</summary>
    private static string Apply(string original, string patch)
    {
        var source = LineSplitter.Split(original).Select(l => l.Content).ToList();
        var output = new List<string>();
        var lines = patch.Split('\n');
        var position = 0;
        var noNewlineAtEnd = original.Length > 0 && !original.EndsWith('\n');
        var i = 2;
        while (i < lines.Length && lines[i].StartsWith("@@"))
        {
            var header = lines[i].Split(' ')[1].TrimStart('-').Split(',');
            var start = int.Parse(header[0]);
            var count = header.Length > 1 ? int.Parse(header[1]) : 1;
            var firstLine = count == 0 ? start : start - 1;
            output.AddRange(source.Skip(position).Take(firstLine - position));
            position = firstLine;
            i++;
            while (i < lines.Length && lines[i].Length > 0 && lines[i][0] is ' ' or '-' or '+' or '\\')
            {
                var line = lines[i];
                switch (line[0])
                {
                    case ' ':
                        Assert.Equal(source[position], line[1..]);
                        output.Add(source[position++]);
                        break;
                    case '-':
                        Assert.Equal(source[position], line[1..]);
                        position++;
                        break;
                    case '+':
                        output.Add(line[1..]);
                        noNewlineAtEnd = i + 1 < lines.Length && lines[i + 1].StartsWith('\\');
                        break;
                }

                i++;
            }
        }

        output.AddRange(source.Skip(position));
        return string.Join("\n", output) + (output.Count > 0 && !noNewlineAtEnd ? "\n" : string.Empty);
    }

    [Theory]
    [InlineData("a\nb\nc\nd\ne\nf\ng\nh\ni\nj\n", "a\nB\nc\nd\ne\nf\ng\nh\ni\nj\nk\n")]
    [InlineData("un\ndeux\ntrois\n", "zéro\nun\ntrois\n")]
    [InlineData("", "nouveau\nfichier\n")]
    [InlineData("tout\nsupprimé\n", "")]
    [InlineData("1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n13\n14\n15\n16\n17\n18\n19\n20\n", "1\n2\nX\n4\n5\n6\n7\n8\n9\n10\n11\n12\n13\n14\n15\n16\n17\nY\n19\n20\n")]
    public void Patch_TransformsAIntoB(string a, string b)
    {
        var result = LineDiffer.Compare(a, b, new DiffOptions { IgnoreLineEndings = false });

        var patch = DiffExporter.ToPatch(result, "a/f.txt", "b/f.txt");

        Assert.StartsWith("--- a/f.txt\n+++ b/f.txt\n@@ ", patch);
        Assert.Equal(b, Apply(a, patch));
    }

    [Fact]
    public void Patch_HasStandardHunkHeaders_AndSplitsDistantChanges()
    {
        var a = string.Join("\n", Enumerable.Range(1, 20)) + "\n";
        var b = string.Join("\n", Enumerable.Range(1, 20).Select(i => i switch { 3 => "trois", 18 => "dix-huit", _ => i.ToString() })) + "\n";

        var patch = DiffExporter.ToPatch(LineDiffer.Compare(a, b), "a", "b");

        var headers = patch.Split('\n').Where(l => l.StartsWith("@@")).ToList();
        Assert.Equal(["@@ -1,6 +1,6 @@", "@@ -15,6 +15,6 @@"], headers);
        Assert.Contains("-3\n+trois\n", patch);
    }

    [Fact]
    public void Patch_MarksMissingFinalNewline()
    {
        var patch = DiffExporter.ToPatch(LineDiffer.Compare("a\nb", "a\nc", new DiffOptions { IgnoreLineEndings = false }), "a", "b");

        Assert.Contains("-b\n\\ No newline at end of file\n+c\n\\ No newline at end of file\n", patch);
    }

    [Fact]
    public void Patch_IsEmpty_ForIdenticalFiles() =>
        Assert.Equal(string.Empty, DiffExporter.ToPatch(LineDiffer.Compare("a\n", "a\n"), "a", "b"));

    [Fact]
    public void Text_ContainsSummaryNumbersAndPrefixes()
    {
        var result = LineDiffer.Compare("a\nb\nc\n", "a\nx\nc\nd\n");
        var text = DiffExporter.ToText(result, DiffDocument.Build(result), "v1.txt", "v2.txt");

        Assert.Contains("A (original) : v1.txt", text);
        Assert.Contains("différence(s)", text);
        Assert.Contains("- b", text);
        Assert.Contains("+ x", text);
        Assert.Contains("+ d", text);
        Assert.Contains("     3      3 │   c", text);
    }

    [Theory]
    [InlineData(DiffDisplayMode.SideBySide)]
    [InlineData(DiffDisplayMode.Unified)]
    public void Html_KeepsColorsAndEscapesContent(DiffDisplayMode mode)
    {
        var result = LineDiffer.Compare("<b>texte</b> = 1\nfin", "<b>texte</b> = 2\nfin\najout");
        var html = DiffExporter.ToHtml(result, DiffDocument.Build(result), mode, "a<1>.txt", "b.txt");

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        Assert.Contains("background: #e6ffec", html);
        Assert.Contains("&lt;b&gt;texte&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>texte</b>", html);
        Assert.Contains("a&lt;1&gt;.txt", html);
        Assert.Contains("modified\"", html);
        Assert.Contains("<span class=\"add\">2</span>", html);
        Assert.Contains("<span class=\"del\">1</span>", html);
    }

    [Fact]
    public void Document_AlignsBothSides_WithFillers()
    {
        var result = LineDiffer.Compare("a\nb\nc", "a\nc\nd");
        var document = DiffDocument.Build(result);

        Assert.Equal(result.Rows.Count, document.Left.Lines.Count);
        Assert.Equal(result.Rows.Count, document.Right.Lines.Count);
        Assert.Equal(document.Left.Text.Split('\n').Length, document.Right.Text.Split('\n').Length);
        Assert.Contains(document.Left.Lines, l => l.IsFiller);
        Assert.Contains(document.Right.Lines, l => l.IsFiller);
        Assert.All(document.Left.Lines.Where(l => l.IsFiller), l => Assert.Equal(DiffRowKind.Added, l.Kind));
    }

    [Fact]
    public void Document_Unified_GroupsDeletionsBeforeAdditions()
    {
        var result = LineDiffer.Compare("a\nvaleur = 1\ntexte = un\nz", "a\nvaleur = 2\ntexte = deux\nz");
        var unified = DiffDocument.Build(result).Unified;

        Assert.Equal(
            ["  a", "- valeur = 1", "- texte = un", "+ valeur = 2", "+ texte = deux", "  z"],
            unified.Text.Split('\n'));
        Assert.Equal(new DiffViewBlock(2, 4), Assert.Single(unified.Blocks));
        Assert.Equal(2, unified.Lines[1].NumberA);
        Assert.Equal(0, unified.Lines[1].NumberB);
    }
}
