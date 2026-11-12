using System.Text;
using TextAnonymizer.Models;

namespace TextAnonymizer.Services;

public sealed class ConversionService : IConversionService
{
    private readonly record struct Rule(string Key, string Value, ConversionEntry Entry);

    private readonly record struct Match(int Start, int Length, Rule Rule);

    public ReplacementResult Anonymize(string text, ConversionTable table)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(table);

        var rules = BuildRules(table, e => e.Original, e => e.Replacement, requireValue: true);
        return Transform(text, rules, table.Strategy);
    }

    public ReplacementResult Restore(string text, ConversionTable table)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(table);

        var rules = BuildRules(table, e => e.Replacement, e => e.Original, requireValue: true);
        return Transform(text, rules, table.Strategy);
    }

    public ReplacementResult Restore(ReplacementResult anonymization, string currentText, ConversionTable table)
    {
        ArgumentNullException.ThrowIfNull(anonymization);
        ArgumentNullException.ThrowIfNull(currentText);

        if (!string.Equals(anonymization.Text, currentText, StringComparison.Ordinal))
        {
            return Restore(currentText, table);
        }

        var segments = anonymization.Segments
            .Select(s => new ReplacementSegment(
                s.SourceIndex,
                s.SourceLength,
                s.Index,
                s.Length,
                s.Entry,
                currentText.Substring(s.Index, s.Length)))
            .ToList();

        return new ReplacementResult(currentText, anonymization.SourceText, segments);
    }

    public IReadOnlyDictionary<ConversionEntry, int> CountOccurrences(string text, ConversionTable table)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(table);

        var rules = BuildRules(table, e => e.Original, e => e.Replacement, requireValue: false);
        var counts = new Dictionary<ConversionEntry, int>();
        foreach (var match in FindMatches(text, rules, table.Strategy))
        {
            counts[match.Rule.Entry] = counts.GetValueOrDefault(match.Rule.Entry) + 1;
        }

        return counts;
    }

    private static List<Rule> BuildRules(
        ConversionTable table,
        Func<ConversionEntry, string> key,
        Func<ConversionEntry, string> value,
        bool requireValue)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rules = new List<Rule>();
        foreach (var entry in table.Entries)
        {
            var k = key(entry);
            var v = value(entry);
            if (string.IsNullOrEmpty(k) || (requireValue && string.IsNullOrEmpty(v)))
            {
                continue;
            }

            // Une table invalide (doublons) n'est pas rejetée ici : la première règle l'emporte.
            if (seen.Add(TextNormalizer.Normalize(k, table.Strategy)))
            {
                rules.Add(new Rule(k, v, entry));
            }
        }

        return rules;
    }

    private static ReplacementResult Transform(string text, List<Rule> rules, MatchingStrategy strategy)
    {
        var matches = FindMatches(text, rules, strategy);
        if (matches.Count == 0)
        {
            return new ReplacementResult(text, text, []);
        }

        var builder = new StringBuilder(text.Length);
        var segments = new List<ReplacementSegment>(matches.Count);
        var cursor = 0;

        foreach (var match in matches)
        {
            builder.Append(text, cursor, match.Start - cursor);
            var index = builder.Length;
            builder.Append(match.Rule.Value);
            segments.Add(new ReplacementSegment(
                index,
                match.Rule.Value.Length,
                match.Start,
                match.Length,
                match.Rule.Entry,
                text.Substring(match.Start, match.Length)));
            cursor = match.Start + match.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return new ReplacementResult(text, builder.ToString(), segments);
    }

    /// <summary>Correspondances sans chevauchement, triées par position ; la plus longue gagne en cas de conflit.</summary>
    private static List<Match> FindMatches(string text, List<Rule> rules, MatchingStrategy strategy)
    {
        var candidates = new List<Match>();
        if (text.Length == 0 || rules.Count == 0)
        {
            return candidates;
        }

        var haystack = TextNormalizer.Normalize(text, strategy);
        foreach (var rule in rules)
        {
            var needle = TextNormalizer.Normalize(rule.Key, strategy);
            var position = 0;
            while ((position = haystack.IndexOf(needle, position, StringComparison.Ordinal)) >= 0)
            {
                if (IsWholeWord(text, position, needle.Length))
                {
                    candidates.Add(new Match(position, needle.Length, rule));
                }

                position++;
            }
        }

        candidates.Sort(static (a, b) =>
        {
            var byLength = b.Length.CompareTo(a.Length);
            return byLength != 0 ? byLength : a.Start.CompareTo(b.Start);
        });

        var occupied = new bool[text.Length];
        var selected = new List<Match>();
        foreach (var candidate in candidates)
        {
            if (Overlaps(occupied, candidate))
            {
                continue;
            }

            Array.Fill(occupied, true, candidate.Start, candidate.Length);
            selected.Add(candidate);
        }

        selected.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return selected;
    }

    private static bool Overlaps(bool[] occupied, Match match)
    {
        for (var i = match.Start; i < match.Start + match.Length; i++)
        {
            if (occupied[i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Une frontière n'est exigée que si le mot commence/finit par une lettre ou un chiffre.</summary>
    private static bool IsWholeWord(string text, int start, int length)
    {
        var end = start + length;
        if (char.IsLetterOrDigit(text[start]) && start > 0 && char.IsLetterOrDigit(text[start - 1]))
        {
            return false;
        }

        return !(char.IsLetterOrDigit(text[end - 1]) && end < text.Length && char.IsLetterOrDigit(text[end]));
    }
}
