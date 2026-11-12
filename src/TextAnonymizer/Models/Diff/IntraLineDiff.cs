namespace TextAnonymizer.Models.Diff;

/// <summary>
/// Comparaison fine de deux lignes : au niveau des mots, puis des caractères quand un seul mot est remplacé par un autre.
/// Respecte les options (casse, espaces, tabulations) : ce qui est ignoré n'est jamais surligné.
/// </summary>
public static class IntraLineDiff
{
    public sealed record Result(IReadOnlyList<TextSpan> Left, IReadOnlyList<TextSpan> Right, double Similarity);

    private enum TokenKind
    {
        Word,
        Space,
        Symbol
    }

    private readonly record struct Token(int Start, int Length, TokenKind Kind);

    public static Result Compare(string left, string right, DiffOptions options)
    {
        var leftTokens = Included(left, Tokenize(left), options);
        var rightTokens = Included(right, Tokenize(right), options);

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var a = leftTokens.Select(t => Id(ids, Key(left, t, options))).ToArray();
        var b = rightTokens.Select(t => Id(ids, Key(right, t, options))).ToArray();
        var (deleted, inserted) = MyersDiff.Compute(a, b);

        var leftSpans = new List<TextSpan>();
        var rightSpans = new List<TextSpan>();
        var equalChars = 0;
        int i = 0, j = 0;
        while (i < a.Length || j < b.Length)
        {
            if (i < a.Length && j < b.Length && !deleted[i] && !inserted[j])
            {
                equalChars += leftTokens[i].Length + rightTokens[j].Length;
                i++;
                j++;
                continue;
            }

            var i0 = i;
            var j0 = j;
            while (i < a.Length && deleted[i])
            {
                i++;
            }

            while (j < b.Length && inserted[j])
            {
                j++;
            }

            // Un mot remplacé par un autre : on affine au caractère (« colour » → « color »).
            if (i - i0 == 1 && j - j0 == 1 && leftTokens[i0].Kind == TokenKind.Word && rightTokens[j0].Kind == TokenKind.Word)
            {
                equalChars += RefineByCharacter(left, leftTokens[i0], right, rightTokens[j0], options, leftSpans, rightSpans);
                continue;
            }

            for (var k = i0; k < i; k++)
            {
                Add(leftSpans, left, leftTokens[k].Start, leftTokens[k].Length);
            }

            for (var k = j0; k < j; k++)
            {
                Add(rightSpans, right, rightTokens[k].Start, rightTokens[k].Length);
            }
        }

        var total = leftTokens.Sum(t => t.Length) + rightTokens.Sum(t => t.Length);
        var similarity = total == 0 ? 1d : (double)equalChars / total;
        return new Result(leftSpans, rightSpans, similarity);
    }

    /// <summary>Renvoie le nombre de caractères identiques (des deux côtés).</summary>
    private static int RefineByCharacter(
        string left, Token leftToken, string right, Token rightToken, DiffOptions options,
        List<TextSpan> leftSpans, List<TextSpan> rightSpans)
    {
        var a = Chars(left, leftToken, options);
        var b = Chars(right, rightToken, options);
        var (deleted, inserted) = MyersDiff.Compute(a, b);
        var equal = deleted.Count(d => !d) + inserted.Count(d => !d);

        // Moins de la moitié en commun : surligner le mot entier se lit mieux qu'une mosaïque de lettres.
        if (equal * 2 < a.Length + b.Length)
        {
            Add(leftSpans, left, leftToken.Start, leftToken.Length);
            Add(rightSpans, right, rightToken.Start, rightToken.Length);
            return equal;
        }

        for (var k = 0; k < a.Length; k++)
        {
            if (deleted[k])
            {
                Add(leftSpans, left, leftToken.Start + k, 1);
            }
        }

        for (var k = 0; k < b.Length; k++)
        {
            if (inserted[k])
            {
                Add(rightSpans, right, rightToken.Start + k, 1);
            }
        }

        return equal;
    }

    private static int[] Chars(string text, Token token, DiffOptions options)
    {
        var chars = new int[token.Length];
        for (var k = 0; k < token.Length; k++)
        {
            var c = text[token.Start + k];
            chars[k] = options.IgnoreCase ? char.ToUpperInvariant(c) : c;
        }

        return chars;
    }

    /// <summary>Ajoute une plage en la fusionnant avec la précédente si elles se touchent ou ne sont séparées que par des blancs.</summary>
    private static void Add(List<TextSpan> spans, string text, int start, int length)
    {
        if (spans.Count > 0)
        {
            var last = spans[^1];
            if (last.End <= start && text.AsSpan(last.End, start - last.End).IsWhiteSpace())
            {
                spans[^1] = new TextSpan(last.Start, start + length - last.Start);
                return;
            }
        }

        spans.Add(new TextSpan(start, length));
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var kind = KindOf(text[i]);
            var start = i;
            i++;
            if (kind != TokenKind.Symbol)
            {
                while (i < text.Length && KindOf(text[i]) == kind)
                {
                    i++;
                }
            }

            tokens.Add(new Token(start, i - start, kind));
        }

        return tokens;
    }

    private static TokenKind KindOf(char c) =>
        char.IsLetterOrDigit(c) || c == '_' ? TokenKind.Word : char.IsWhiteSpace(c) ? TokenKind.Space : TokenKind.Symbol;

    /// <summary>Retire les blancs que les options demandent d'ignorer : ils ne participent pas à la comparaison.</summary>
    private static List<Token> Included(string text, List<Token> tokens, DiffOptions options)
    {
        if (options.IgnoreAllWhitespace)
        {
            return tokens.Where(t => t.Kind != TokenKind.Space).ToList();
        }

        if (options.TrimWhitespace)
        {
            var start = tokens.Count > 0 && tokens[0].Kind == TokenKind.Space ? 1 : 0;
            var end = tokens.Count > start && tokens[^1].Kind == TokenKind.Space ? tokens.Count - 1 : tokens.Count;
            return tokens.GetRange(start, end - start);
        }

        return tokens;
    }

    private static string Key(string text, Token token, DiffOptions options)
    {
        var value = text.Substring(token.Start, token.Length);
        if (token.Kind == TokenKind.Space)
        {
            if (options.CollapseWhitespace)
            {
                return " ";
            }

            return options.IgnoreTabs ? value.Replace('\t', ' ') : value;
        }

        return options.IgnoreCase ? value.ToUpperInvariant() : value;
    }

    private static int Id(Dictionary<string, int> ids, string key)
    {
        if (!ids.TryGetValue(key, out var id))
        {
            id = ids.Count;
            ids[key] = id;
        }

        return id;
    }
}
