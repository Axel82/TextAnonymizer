using System.Diagnostics;
using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Tests;

public class LineDifferTests
{
    private static string Kinds(DiffResult result) =>
        string.Concat(result.Rows.Select(r => r.Kind switch
        {
            DiffRowKind.Equal => '=',
            DiffRowKind.Added => '+',
            DiffRowKind.Deleted => '-',
            DiffRowKind.Modified => '~',
            _ => '.'
        }));

    [Fact]
    public void IdenticalTexts_HaveNoDifference()
    {
        var result = LineDiffer.Compare("a\nb\nc\n", "a\nb\nc\n");

        Assert.True(result.IsIdentical);
        Assert.Equal("===", Kinds(result));
        Assert.Equal(1d, result.Statistics.Similarity);
        Assert.Equal(0, result.Statistics.Differences);
    }

    [Fact]
    public void EmptyTexts_AreIdentical()
    {
        Assert.True(LineDiffer.Compare("", "").IsIdentical);
        Assert.True(LineDiffer.Compare(null, null).IsIdentical);
    }

    [Fact]
    public void EmptyAgainstText_IsAllAdded()
    {
        var result = LineDiffer.Compare("", "x\ny");

        Assert.Equal("++", Kinds(result));
        Assert.Equal(0d, result.Statistics.Similarity);
        Assert.Single(result.Blocks);
    }

    [Fact]
    public void AddedLines_AreDetected()
    {
        var result = LineDiffer.Compare("un\ndeux\ntrois", "un\ndeux\nnouvelle\ntrois\nfin");

        Assert.Equal("==+=+", Kinds(result));
        Assert.Equal(2, result.Statistics.Added);
        Assert.Equal(2, result.Blocks.Count);
        Assert.Equal("nouvelle", result.Rows[2].Right!.Content);
        Assert.Null(result.Rows[2].Left);
    }

    [Fact]
    public void DeletedLines_AreDetected()
    {
        var result = LineDiffer.Compare("un\ndeux\ntrois\nquatre", "un\nquatre");

        Assert.Equal("=--=", Kinds(result));
        Assert.Equal(2, result.Statistics.Deleted);
        Assert.Single(result.Blocks);
        Assert.Equal(new DiffBlock(0, 1, 2), result.Blocks[0]);
    }

    [Fact]
    public void SimilarLine_IsModified_WithWordDetail()
    {
        var result = LineDiffer.Compare("var total = prix * quantite;", "var total = prix * quantite + taxe;");

        var row = Assert.Single(result.Rows);
        Assert.Equal(DiffRowKind.Modified, row.Kind);
        Assert.Empty(row.LeftSpans);
        var span = Assert.Single(row.RightSpans);
        Assert.Equal(" + taxe", row.Right!.Content.Substring(span.Start, span.Length));
    }

    [Fact]
    public void ReplacedWord_IsRefinedByCharacter()
    {
        var result = LineDiffer.Compare("the colour is red", "the color is red");

        var row = Assert.Single(result.Rows);
        Assert.Equal(DiffRowKind.Modified, row.Kind);
        var span = Assert.Single(row.LeftSpans);
        Assert.Equal("u", row.Left!.Content.Substring(span.Start, span.Length));
        Assert.Empty(row.RightSpans);
    }

    [Fact]
    public void UnrelatedLines_AreDeletedAndAdded_NotModified()
    {
        var result = LineDiffer.Compare("alpha\nbloc ancien totalement\nomega", "alpha\nxyz 123 !!\nomega");

        Assert.Equal("=-+=", Kinds(result));
        Assert.Equal(0, result.Statistics.Modified);
    }

    [Fact]
    public void Pairing_KeepsOrder_AndMatchesTheClosestLines()
    {
        var a = "début\nint a = 1;\nint b = 2;\nfin";
        var b = "début\nligne insérée sans rapport\nint a = 10;\nint b = 20;\nfin";

        var result = LineDiffer.Compare(a, b);

        Assert.Equal("=+~~=", Kinds(result));
        Assert.Equal("int a = 1;", result.Rows[2].Left!.Content);
        Assert.Equal("int a = 10;", result.Rows[2].Right!.Content);
    }

    [Fact]
    public void Statistics_CountEveryKind()
    {
        var result = LineDiffer.Compare("a\nb\nc\nvaleur = 1\nd", "a\nc\nvaleur = 2\nd\ne");

        var stats = result.Statistics;
        Assert.Equal(3, stats.Identical);
        Assert.Equal(1, stats.Deleted);
        Assert.Equal(1, stats.Added);
        Assert.Equal(1, stats.Modified);
        Assert.Equal(3, stats.Differences);
        // A : 5 lignes, B : 5 lignes, 3 identiques → 2 × 3 / 10.
        Assert.Equal(0.6, stats.Similarity, 3);
    }

    [Fact]
    public void LineNumbers_AreThoseOfTheOriginalFiles()
    {
        var result = LineDiffer.Compare("a\nb\nc", "z\na\nc");

        var rowC = result.Rows.Last();
        Assert.Equal(3, rowC.Left!.Number);
        Assert.Equal(3, rowC.Right!.Number);
    }

    [Fact]
    public void TrailingNewline_DoesNotCreateAnExtraLine()
    {
        Assert.Equal(2, LineSplitter.Split("a\nb\n").Count);
        Assert.Equal(3, LineSplitter.Split("a\r\nb\r\n\r\n").Count);
        Assert.Equal(new[] { "a", "b", "c" }, LineSplitter.Split("a\rb\r\nc").Select(l => l.Content));
    }

    [Theory]
    [InlineData("a\nb", LineEnding.Lf)]
    [InlineData("a\r\nb\r\n", LineEnding.CrLf)]
    [InlineData("a\rb", LineEnding.Cr)]
    [InlineData("a\r\nb\n", LineEnding.Mixed)]
    [InlineData("a", LineEnding.None)]
    public void LineEnding_IsDetected(string text, LineEnding expected) => Assert.Equal(expected, LineSplitter.Detect(text));

    [Fact]
    public void LargeTexts_AreComparedQuickly()
    {
        var random = new Random(42);
        var a = Enumerable.Range(0, 100_000).Select(i => $"ligne {i} contenu {random.Next(1000)}").ToList();
        var b = new List<string>(a);
        for (var k = 0; k < 500; k++)
        {
            var index = random.Next(b.Count);
            switch (k % 3)
            {
                case 0:
                    b[index] = b[index] + " modifiée";
                    break;
                case 1:
                    b.Insert(index, $"insérée {k}");
                    break;
                default:
                    b.RemoveAt(index);
                    break;
            }
        }

        var watch = Stopwatch.StartNew();
        var result = LineDiffer.Compare(string.Join("\n", a), string.Join("\n", b));
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"Comparaison trop lente : {watch.Elapsed}");
        Assert.InRange(result.Blocks.Count, 300, 500);
        Assert.Equal(a.Count, result.Rows.Count(r => r.Left is not null));
        Assert.Equal(b.Count, result.Rows.Count(r => r.Right is not null));
    }

    [Fact]
    public void LargeCompletelyDifferentTexts_StayFast()
    {
        var a = string.Join("\n", Enumerable.Range(0, 50_000).Select(i => $"a{i % 977}"));
        var b = string.Join("\n", Enumerable.Range(0, 50_000).Select(i => $"a{(i * 7) % 983}"));

        var watch = Stopwatch.StartNew();
        var result = LineDiffer.Compare(a, b);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"Comparaison trop lente : {watch.Elapsed}");
        Assert.Equal(50_000, result.Rows.Count(r => r.Left is not null));
        Assert.Equal(50_000, result.Rows.Count(r => r.Right is not null));
    }

    [Fact]
    public void Comparison_CanBeCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => LineDiffer.Compare("a\nb", "a\nc", null, cts.Token));
    }
}
