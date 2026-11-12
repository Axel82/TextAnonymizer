using TextAnonymizer.Models;

namespace TextAnonymizer.Tests;

public class BijectionTests
{
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
    public void ValidTable_IsValid()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"), ("Paris", "Lyon"));

        var result = table.ValidateBijection();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void DuplicateOriginal_IsRejectedWithExplicitMessage()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"), ("Jean", "Marc"));

        var result = table.ValidateBijection();

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("original", error);
        Assert.Contains("Jean", error);
        Assert.Contains("ligne 2", error);
    }

    [Fact]
    public void DuplicateReplacement_IsRejectedWithExplicitMessage()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", "Paul"), ("Pierre", "Paul"));

        var result = table.ValidateBijection();

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("remplaçant", error);
        Assert.Contains("Paul", error);
    }

    [Fact]
    public void Duplicates_AreDetectedAccordingToStrategy()
    {
        var pairs = new[] { ("Hélène", "Claire"), ("helene", "CLAIRE") };

        Assert.True(Table(MatchingStrategy.Strict, pairs).ValidateBijection().IsValid);
        Assert.True(Table(MatchingStrategy.IgnoreCase, pairs[0], ("hélène2", "Marie")).ValidateBijection().IsValid);
        Assert.False(Table(MatchingStrategy.IgnoreCase, ("Hélène", "Claire"), ("HÉLÈNE", "Marie")).ValidateBijection().IsValid);
        Assert.Equal(2, Table(MatchingStrategy.IgnoreCaseAndDiacritics, pairs).ValidateBijection().Errors.Count);
    }

    [Fact]
    public void EmptyOriginal_IsAlwaysRejected()
    {
        var table = Table(MatchingStrategy.Strict, (" ", "Paul"));

        Assert.False(table.ValidateBijection(allowEmptyReplacements: true).IsValid);
    }

    [Fact]
    public void EmptyReplacement_IsRejectedUnlessAllowed()
    {
        var table = Table(MatchingStrategy.Strict, ("Jean", ""), ("Pierre", ""));

        Assert.False(table.ValidateBijection().IsValid);
        Assert.True(table.ValidateBijection(allowEmptyReplacements: true).IsValid);
    }

    [Fact]
    public void AllErrors_AreReported()
    {
        var table = Table(MatchingStrategy.Strict, ("A", "X"), ("A", "Y"), ("B", "X"));

        var result = table.ValidateBijection();

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(Environment.NewLine, result.Message);
    }
}
