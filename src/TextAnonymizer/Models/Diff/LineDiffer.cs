using System.Text;

namespace TextAnonymizer.Models.Diff;

/// <summary>
/// Comparaison ligne par ligne : clé de comparaison selon les options, algorithme de Myers sur les clés,
/// puis appariement des lignes proches d'un même bloc en « lignes modifiées » avec détail au mot ou au caractère.
/// </summary>
public static class LineDiffer
{
    /// <summary>Similarité minimale pour qu'une ligne supprimée et une ligne ajoutée soient présentées comme une modification.</summary>
    public const double ModifiedThreshold = 0.5;

    /// <summary>Au-delà de ce nombre de paires (lignes supprimées × ajoutées), l'appariement est fait rang par rang.</summary>
    private const int MaxPairingCells = 2500;

    public static DiffResult Compare(string? textA, string? textB, DiffOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= DiffOptions.Default;
        var linesA = LineSplitter.Split(textA);
        var linesB = LineSplitter.Split(textB);

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var (keptA, idsA) = Prepare(linesA, options, ids, cancellationToken);
        var (keptB, idsB) = Prepare(linesB, options, ids, cancellationToken);
        var (deleted, inserted) = MyersDiff.Compute(idsA, idsB, cancellationToken);

        var builder = new RowBuilder(linesA, linesB, options);
        int i = 0, j = 0;
        while (i < keptA.Length || j < keptB.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (i < keptA.Length && j < keptB.Length && !deleted[i] && !inserted[j])
            {
                builder.Equal(keptA[i], keptB[j]);
                i++;
                j++;
                continue;
            }

            var i0 = i;
            var j0 = j;
            while (i < keptA.Length && deleted[i])
            {
                i++;
            }

            while (j < keptB.Length && inserted[j])
            {
                j++;
            }

            builder.Block(keptA[i0..i], keptB[j0..j]);
        }

        builder.Flush(linesA.Count, linesB.Count);
        return new DiffResult(builder.Rows, linesA, linesB, options);
    }

    /// <summary>Clé utilisée pour comparer une ligne (le contenu affiché, lui, n'est jamais modifié).</summary>
    public static string Key(TextLine line, DiffOptions options)
    {
        var value = line.Content;
        if (options.IgnoreTabs)
        {
            value = value.Replace('\t', ' ');
        }

        if (options.IgnoreAllWhitespace)
        {
            value = string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
        }
        else
        {
            if (options.TrimWhitespace)
            {
                value = value.Trim();
            }

            if (options.CollapseWhitespace)
            {
                value = Collapse(value);
            }
        }

        if (options.IgnoreCase)
        {
            value = value.ToUpperInvariant();
        }

        // Le caractère NUL ne peut pas apparaître dans une ligne de texte : il sépare sans ambiguïté la fin de ligne.
        return options.IgnoreLineEndings ? value : value + "\0" + line.Terminator;
    }

    private static string Collapse(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inSpace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inSpace)
                {
                    builder.Append(' ');
                }

                inSpace = true;
            }
            else
            {
                builder.Append(c);
                inSpace = false;
            }
        }

        return builder.ToString();
    }

    private static bool IsIgnored(TextLine line, DiffOptions options) =>
        options.IgnoreBlankLines && string.IsNullOrWhiteSpace(line.Content);

    /// <summary>Indices (base 0) des lignes comparées et identifiant de leur clé.</summary>
    private static (int[] Kept, int[] Ids) Prepare(
        IReadOnlyList<TextLine> lines, DiffOptions options, Dictionary<string, int> ids, CancellationToken cancellationToken)
    {
        var kept = new List<int>(lines.Count);
        var keys = new List<int>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (IsIgnored(lines[index], options))
            {
                continue;
            }

            var key = Key(lines[index], options);
            if (!ids.TryGetValue(key, out var id))
            {
                id = ids.Count;
                ids[key] = id;
            }

            kept.Add(index);
            keys.Add(id);
        }

        return ([.. kept], [.. keys]);
    }

    /// <summary>Construit les rangées dans l'ordre, en intercalant les lignes vides ignorées à leur place.</summary>
    private sealed class RowBuilder(IReadOnlyList<TextLine> linesA, IReadOnlyList<TextLine> linesB, DiffOptions options)
    {
        private int _nextA;
        private int _nextB;

        public List<DiffRow> Rows { get; } = [];

        public void Equal(int a, int b)
        {
            Flush(a, b);
            Rows.Add(new DiffRow(DiffRowKind.Equal, linesA[a], linesB[b]));
            _nextA = a + 1;
            _nextB = b + 1;
        }

        public void Block(int[] deleted, int[] added)
        {
            foreach (var (a, b) in Pair(deleted, added))
            {
                if (a >= 0 && b >= 0)
                {
                    Flush(a, b);
                    Rows.Add(Modified(linesA[a], linesB[b]));
                    _nextA = a + 1;
                    _nextB = b + 1;
                }
                else if (a >= 0)
                {
                    Flush(a, _nextB);
                    Rows.Add(new DiffRow(DiffRowKind.Deleted, linesA[a], null));
                    _nextA = a + 1;
                }
                else
                {
                    Flush(_nextA, b);
                    Rows.Add(new DiffRow(DiffRowKind.Added, null, linesB[b]));
                    _nextB = b + 1;
                }
            }
        }

        /// <summary>Émet les lignes ignorées restant avant <paramref name="untilA"/> et <paramref name="untilB"/>, côte à côte si possible.</summary>
        public void Flush(int untilA, int untilB)
        {
            while (_nextA < untilA || _nextB < untilB)
            {
                var left = _nextA < untilA ? linesA[_nextA++] : null;
                var right = _nextB < untilB ? linesB[_nextB++] : null;
                Rows.Add(new DiffRow(DiffRowKind.Ignored, left, right));
            }
        }

        private DiffRow Modified(TextLine left, TextLine right)
        {
            var detail = IntraLineDiff.Compare(left.Content, right.Content, options);
            var onlyEnding = !options.IgnoreLineEndings && detail.Left.Count == 0 && detail.Right.Count == 0;
            return new DiffRow(DiffRowKind.Modified, left, right)
            {
                LeftSpans = detail.Left,
                RightSpans = detail.Right,
                OnlyLineEndingDiffers = onlyEnding && left.Terminator != right.Terminator
            };
        }

        /// <summary>
        /// Ordre d'émission d'un bloc : paires (a, b) modifiées, ou (a, -1) supprimée, ou (-1, b) ajoutée.
        /// L'appariement maximise la somme des similarités tout en conservant l'ordre des lignes.
        /// </summary>
        private IEnumerable<(int A, int B)> Pair(int[] deleted, int[] added)
        {
            List<(int, int)> pairs = deleted.Length == 0 || added.Length == 0
                ? []
                : (long)deleted.Length * added.Length <= MaxPairingCells
                    ? BestPairs(deleted, added)
                    : RankPairs(deleted, added);

            int i = 0, j = 0;
            foreach (var (pi, pj) in pairs.Append((deleted.Length, added.Length)))
            {
                for (; i < pi; i++)
                {
                    yield return (deleted[i], -1);
                }

                for (; j < pj; j++)
                {
                    yield return (-1, added[j]);
                }

                if (pi < deleted.Length && pj < added.Length)
                {
                    yield return (deleted[pi], added[pj]);
                    i = pi + 1;
                    j = pj + 1;
                }
            }
        }

        private double Similarity(int a, int b) =>
            IntraLineDiff.Compare(linesA[a].Content, linesB[b].Content, options).Similarity;

        /// <summary>Programmation dynamique : appariement ordonné de similarité totale maximale.</summary>
        private List<(int, int)> BestPairs(int[] deleted, int[] added)
        {
            int p = deleted.Length, q = added.Length;
            var score = new double[p + 1, q + 1];
            var similarity = new double[p, q];
            for (var i = 0; i < p; i++)
            {
                for (var j = 0; j < q; j++)
                {
                    similarity[i, j] = Similarity(deleted[i], added[j]);
                }
            }

            for (var i = p - 1; i >= 0; i--)
            {
                for (var j = q - 1; j >= 0; j--)
                {
                    var best = Math.Max(score[i + 1, j], score[i, j + 1]);
                    if (similarity[i, j] >= ModifiedThreshold)
                    {
                        best = Math.Max(best, score[i + 1, j + 1] + similarity[i, j]);
                    }

                    score[i, j] = best;
                }
            }

            var pairs = new List<(int, int)>();
            int x = 0, y = 0;
            while (x < p && y < q)
            {
                if (similarity[x, y] >= ModifiedThreshold && score[x, y] == score[x + 1, y + 1] + similarity[x, y])
                {
                    pairs.Add((x, y));
                    x++;
                    y++;
                }
                else if (score[x, y] == score[x + 1, y])
                {
                    x++;
                }
                else
                {
                    y++;
                }
            }

            return pairs;
        }

        /// <summary>Gros bloc : la i-ième ligne supprimée est rapprochée de la i-ième ajoutée si elles se ressemblent.</summary>
        private List<(int, int)> RankPairs(int[] deleted, int[] added)
        {
            var pairs = new List<(int, int)>();
            for (var k = 0; k < Math.Min(deleted.Length, added.Length); k++)
            {
                if (Similarity(deleted[k], added[k]) >= ModifiedThreshold)
                {
                    pairs.Add((k, k));
                }
            }

            return pairs;
        }
    }
}
