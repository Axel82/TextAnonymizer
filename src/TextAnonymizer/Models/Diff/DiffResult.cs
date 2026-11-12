namespace TextAnonymizer.Models.Diff;

public enum DiffRowKind
{
    /// <summary>Ligne identique des deux côtés (au sens des options).</summary>
    Equal,

    /// <summary>Ligne présente seulement dans A.</summary>
    Deleted,

    /// <summary>Ligne présente seulement dans B.</summary>
    Added,

    /// <summary>Ligne de A remplacée par une ligne proche de B.</summary>
    Modified,

    /// <summary>Ligne vide écartée par l'option « Ignorer les lignes vides ».</summary>
    Ignored
}

/// <summary>Plage de caractères modifiés dans le contenu d'une ligne.</summary>
public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// Une rangée de la vue alignée : ligne de A à gauche, ligne de B à droite (l'une des deux peut manquer).
/// </summary>
public sealed class DiffRow
{
    public DiffRow(DiffRowKind kind, TextLine? left, TextLine? right)
    {
        Kind = kind;
        Left = left;
        Right = right;
    }

    public DiffRowKind Kind { get; }

    public TextLine? Left { get; }

    public TextLine? Right { get; }

    /// <summary>Parties modifiées de la ligne de gauche (lignes modifiées uniquement).</summary>
    public IReadOnlyList<TextSpan> LeftSpans { get; init; } = [];

    public IReadOnlyList<TextSpan> RightSpans { get; init; } = [];

    /// <summary>Seule la fin de ligne diffère (CRLF contre LF, ou saut de ligne final absent).</summary>
    public bool OnlyLineEndingDiffers { get; init; }

    public bool IsChange => Kind is DiffRowKind.Deleted or DiffRowKind.Added or DiffRowKind.Modified;
}

/// <summary>Bloc de différences : rangées consécutives modifiées, ajoutées ou supprimées.</summary>
public sealed record DiffBlock(int Index, int StartRow, int RowCount)
{
    public int EndRow => StartRow + RowCount;
}

public sealed record DiffStatistics(
    int Identical,
    int Added,
    int Deleted,
    int Modified,
    int Differences,
    double Similarity)
{
    public static DiffStatistics Empty { get; } = new(0, 0, 0, 0, 0, 1);

    public static DiffStatistics From(IReadOnlyList<DiffRow> rows, int blocks)
    {
        int identical = 0, added = 0, deleted = 0, modified = 0;
        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case DiffRowKind.Equal:
                    identical++;
                    break;
                case DiffRowKind.Added:
                    added++;
                    break;
                case DiffRowKind.Deleted:
                    deleted++;
                    break;
                case DiffRowKind.Modified:
                    modified++;
                    break;
            }
        }

        // Lignes comparées de chaque côté (hors lignes vides ignorées) : A = identiques + supprimées + modifiées.
        var linesA = identical + deleted + modified;
        var linesB = identical + added + modified;
        var similarity = linesA + linesB == 0 ? 1d : 2d * identical / (linesA + linesB);
        return new DiffStatistics(identical, added, deleted, modified, blocks, similarity);
    }
}

public sealed class DiffResult
{
    public DiffResult(IReadOnlyList<DiffRow> rows, IReadOnlyList<TextLine> linesA, IReadOnlyList<TextLine> linesB, DiffOptions options)
    {
        Rows = rows;
        LinesA = linesA;
        LinesB = linesB;
        Options = options;
        Blocks = FindBlocks(rows);
        Statistics = DiffStatistics.From(rows, Blocks.Count);
    }

    public static DiffResult Empty { get; } = new([], [], [], DiffOptions.Default);

    public IReadOnlyList<DiffRow> Rows { get; }

    public IReadOnlyList<DiffBlock> Blocks { get; }

    public IReadOnlyList<TextLine> LinesA { get; }

    public IReadOnlyList<TextLine> LinesB { get; }

    public DiffOptions Options { get; }

    public DiffStatistics Statistics { get; }

    public bool IsIdentical => Blocks.Count == 0;

    private static List<DiffBlock> FindBlocks(IReadOnlyList<DiffRow> rows)
    {
        var blocks = new List<DiffBlock>();
        var start = -1;
        for (var i = 0; i <= rows.Count; i++)
        {
            var isChange = i < rows.Count && rows[i].IsChange;
            if (isChange && start < 0)
            {
                start = i;
            }
            else if (!isChange && start >= 0)
            {
                blocks.Add(new DiffBlock(blocks.Count, start, i - start));
                start = -1;
            }
        }

        return blocks;
    }
}
