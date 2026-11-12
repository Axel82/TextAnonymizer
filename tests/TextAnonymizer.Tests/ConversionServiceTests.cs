using TextAnonymizer.Models;
using TextAnonymizer.Services;

namespace TextAnonymizer.Tests;

public class ConversionServiceTests
{
    private readonly ConversionService _service = new();

    private static ConversionTable Table(MatchingStrategy strategy, params (string Original, string Replacement)[] pairs)
    {
        var table = new ConversionTable { Strategy = strategy };
        foreach (var (original, replacement) in pairs)
        {
            table.Entries.Add(new ConversionEntry { Original = original, Replacement = replacement });
        }

        return table;
    }

    [Fact]
    public void Anonymize_Strict_RespectsCaseAndAccents()
    {
        var table = Table(MatchingStrategy.Strict, ("Hélène", "Claire"));

        var result = _service.Anonymize("Hélène, hélène, Helene, HÉLÈNE", table);

        Assert.Equal("Claire, hélène, Helene, HÉLÈNE", result.Text);
        Assert.Single(result.Segments);
    }

    [Fact]
    public void Anonymize_IgnoreCase_MatchesAnyCaseButKeepsAccents()
    {
        var table = Table(MatchingStrategy.IgnoreCase, ("Hélène", "Claire"));

        var result = _service.Anonymize("Hélène, hélène, HÉLÈNE, Helene", table);

        Assert.Equal("Claire, Claire, Claire, Helene", result.Text);
        Assert.Equal(3, result.Segments.Count);
    }

    [Fact]
    public void Anonymize_IgnoreCaseAndDiacritics_MatchesCaseAndAccentVariants()
    {
        var table = Table(MatchingStrategy.IgnoreCaseAndDiacritics, ("Hélène", "Claire"));

        var result = _service.Anonymize("Hélène, hélène, HELENE, Helene", table);

        Assert.Equal("Claire, Claire, Claire, Claire", result.Text);
        Assert.Equal(4, result.Segments.Count);
    }

    [Fact]
    public void Anonymize_LongestMatchWins_WhenRulesOverlap()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"), ("Jean Dupont", "Marc Leroy"), ("Dupont", "Durand"));

        var result = _service.Anonymize("Jean Dupont et Jean", table);

        Assert.Equal("Marc Leroy et Paul", result.Text);
    }

    [Fact]
    public void Anonymize_LongestMatchWins_RegardlessOfTableOrder()
    {
        var table = Table(MatchingStrategy.Strict, ("Dupont", "Durand"), ("Jean Dupont", "Marc Leroy"));

        Assert.Equal("Marc Leroy", _service.Anonymize("Jean Dupont", table).Text);
    }

    [Fact]
    public void Anonymize_MatchesWholeWordsOnly()
    {
        var table = Table(MatchingStrategy.Strict, ("Paris", "Lyon"));

        var result = _service.Anonymize("Paris, Parisien, Saint-Paris, XParis", table);

        Assert.Equal("Lyon, Parisien, Saint-Lyon, XParis", result.Text);
    }

    [Fact]
    public void Anonymize_SupportsEmailsAndPhones()
    {
        var table = Table(
            MatchingStrategy.Strict,
            ("jean.dupont@acme.com", "marc.leroy@exemple.fr"),
            ("06 12 34 56 78", "07 98 76 54 32"));

        var result = _service.Anonymize("Écrire à jean.dupont@acme.com ou appeler le 06 12 34 56 78.", table);

        Assert.Equal("Écrire à marc.leroy@exemple.fr ou appeler le 07 98 76 54 32.", result.Text);
    }

    [Fact]
    public void Anonymize_ReturnsSegmentsWithCorrectIndexes()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Alexandre"), ("Paris", "Lyon"));

        var result = _service.Anonymize("Jean habite à Paris.", table);

        Assert.Equal("Alexandre habite à Lyon.", result.Text);
        Assert.Equal(2, result.Segments.Count);

        var first = result.Segments[0];
        Assert.Equal("Alexandre", result.Text.Substring(first.Index, first.Length));
        Assert.Equal("Jean", result.SourceText.Substring(first.SourceIndex, first.SourceLength));
        Assert.Equal("Jean", first.MatchedText);

        var second = result.Segments[1];
        Assert.Equal("Lyon", result.Text.Substring(second.Index, second.Length));
        Assert.Equal("Paris", second.MatchedText);
    }

    [Fact]
    public void Anonymize_WithoutMatch_ReturnsSameText()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"));

        var result = _service.Anonymize("Rien à voir.", table);

        Assert.Equal("Rien à voir.", result.Text);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void Anonymize_EmptyText_ReturnsEmpty()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"));

        Assert.Equal(string.Empty, _service.Anonymize(string.Empty, table).Text);
    }

    [Fact]
    public void Anonymize_ReplacementEqualToAnotherOriginal_IsNotReplacedTwice()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"), ("Paul", "Marc"));

        Assert.Equal("Paul Marc", _service.Anonymize("Jean Paul", table).Text);
    }

    [Theory]
    [InlineData(MatchingStrategy.Strict)]
    [InlineData(MatchingStrategy.IgnoreCase)]
    [InlineData(MatchingStrategy.IgnoreCaseAndDiacritics)]
    public void Restore_WithTable_InvertsAnonymization(MatchingStrategy strategy)
    {
        var table = Table(strategy, ("Jean Dupont", "Marc Leroy"), ("Paris", "Lyon"), ("jean@acme.com", "marc@exemple.fr"));
        const string source = "Jean Dupont (jean@acme.com) vit à Paris.\nParis, encore.";

        var anonymized = _service.Anonymize(source, table);
        var restored = _service.Restore(anonymized.Text, table);

        Assert.NotEqual(source, anonymized.Text);
        Assert.Equal(source, restored.Text);
    }

    [Theory]
    [InlineData(MatchingStrategy.IgnoreCase)]
    [InlineData(MatchingStrategy.IgnoreCaseAndDiacritics)]
    public void Restore_WithAnonymization_ReturnsExactSourceEvenWhenCaseDiffers(MatchingStrategy strategy)
    {
        var table = Table(strategy, ("Hélène Dupont", "Claire Martin"), ("paris", "Lyon"));
        const string source = "HELENE DUPONT habite à Paris, et hélène dupont aussi.";

        var anonymized = _service.Anonymize(source, table);
        var restored = _service.Restore(anonymized, anonymized.Text, table);

        Assert.Equal(source, restored.Text);
        Assert.Equal(anonymized.Segments.Count, restored.Segments.Count);
    }

    [Fact]
    public void Restore_WithAnonymization_FallsBackToTableWhenTextWasEdited()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"));
        var anonymized = _service.Anonymize("Jean est là.", table);

        var restored = _service.Restore(anonymized, "Paul est parti.", table);

        Assert.Equal("Jean est parti.", restored.Text);
    }

    [Fact]
    public void Restore_ExactSegments_PointToRestoredText()
    {
        var table = Table(MatchingStrategy.IgnoreCase, ("Jean", "Paul"));
        var anonymized = _service.Anonymize("JEAN et jean", table);

        var restored = _service.Restore(anonymized, anonymized.Text, table);

        Assert.All(restored.Segments, s => Assert.Equal("Paul", s.MatchedText));
        Assert.Equal("JEAN", restored.Text.Substring(restored.Segments[0].Index, restored.Segments[0].Length));
        Assert.Equal("jean", restored.Text.Substring(restored.Segments[1].Index, restored.Segments[1].Length));
    }

    [Fact]
    public void CountOccurrences_CountsOriginalsEvenWithoutReplacement()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", ""), ("Paris", "Lyon"));

        var counts = _service.CountOccurrences("Jean, Jean et Paris", table);

        Assert.Equal(2, counts[table.Entries[0]]);
        Assert.Equal(1, counts[table.Entries[1]]);
    }
}
