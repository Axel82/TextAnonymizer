using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Tests;

public class DiffOptionsTests
{
    private static readonly DiffOptions Strict = new() { IgnoreLineEndings = false };

    private static bool Same(string a, string b, DiffOptions options) => LineDiffer.Compare(a, b, options).IsIdentical;

    [Fact]
    public void IgnoreCase()
    {
        Assert.False(Same("Bonjour", "BONJOUR", Strict));
        Assert.True(Same("Bonjour", "BONJOUR", Strict with { IgnoreCase = true }));
    }

    [Fact]
    public void TrimWhitespace_OnlyAtBothEnds()
    {
        var options = Strict with { TrimWhitespace = true };
        Assert.False(Same("  a b  ", "a b", Strict));
        Assert.True(Same("  a b  ", "a b", options));
        Assert.False(Same("a  b", "a b", options));
    }

    [Fact]
    public void CollapseWhitespace()
    {
        var options = Strict with { CollapseWhitespace = true };
        Assert.True(Same("a    b\t c", "a b c", options));
        Assert.False(Same("ab", "a b", options));
    }

    [Fact]
    public void IgnoreTabs_TabEqualsSpace()
    {
        var options = Strict with { IgnoreTabs = true };
        Assert.False(Same("a\tb", "a b", Strict));
        Assert.True(Same("a\tb", "a b", options));
        Assert.False(Same("\tx", "    x", options));
    }

    [Fact]
    public void IgnoreBlankLines()
    {
        var a = "a\n\nb\n   \nc";
        var b = "a\nb\nc\n\n";
        var options = DiffOptions.Default with { IgnoreBlankLines = true };

        Assert.False(Same(a, b, DiffOptions.Default));
        var result = LineDiffer.Compare(a, b, options);
        Assert.True(result.IsIdentical);
        Assert.Contains(result.Rows, r => r.Kind == DiffRowKind.Ignored);
        // Les lignes ignorées restent affichées : toutes les lignes des deux fichiers sont présentes.
        Assert.Equal(5, result.Rows.Count(r => r.Left is not null));
        Assert.Equal(4, result.Rows.Count(r => r.Right is not null));
        Assert.Equal(3, result.Statistics.Identical);
    }

    [Fact]
    public void IgnoreLineEndings()
    {
        Assert.False(Same("a\r\nb\r\n", "a\nb\n", Strict));
        Assert.True(Same("a\r\nb\r\n", "a\nb\n", Strict with { IgnoreLineEndings = true }));
        Assert.True(Same("a\nb\n", "a\nb", DiffOptions.Default));
    }

    [Fact]
    public void LineEndingOnlyDifference_IsFlagged()
    {
        var result = LineDiffer.Compare("a\r\nb", "a\nb", Strict);

        var row = result.Rows[0];
        Assert.Equal(DiffRowKind.Modified, row.Kind);
        Assert.True(row.OnlyLineEndingDiffers);
    }

    [Fact]
    public void IgnoreAllWhitespace()
    {
        var options = Strict with { IgnoreAllWhitespace = true };
        Assert.True(Same("f(a, b)", "f( a,b )", options));
        Assert.False(Same("f(a, b)", "f(a, c)", options));
    }

    [Fact]
    public void Options_AreIndependent()
    {
        Assert.False(Same(" ABC ", "abc", Strict with { IgnoreCase = true }));
        Assert.False(Same(" ABC ", "abc", Strict with { TrimWhitespace = true }));
        Assert.True(Same(" ABC ", "abc", Strict with { IgnoreCase = true, TrimWhitespace = true }));
    }

    [Fact]
    public void IgnoredDifferences_AreNotHighlighted()
    {
        var result = LineDiffer.Compare("Valeur = 1", "VALEUR = 2", Strict with { IgnoreCase = true });

        var row = Assert.Single(result.Rows);
        Assert.Equal(DiffRowKind.Modified, row.Kind);
        var span = Assert.Single(row.RightSpans);
        Assert.Equal("2", row.Right!.Content.Substring(span.Start, span.Length));
    }

    [Fact]
    public void DisplayedText_IsNeverAltered()
    {
        var result = LineDiffer.Compare("  Abc\t", "abc", new DiffOptions { IgnoreCase = true, TrimWhitespace = true, IgnoreTabs = true });

        Assert.Equal("  Abc\t", result.Rows[0].Left!.Content);
    }
}
