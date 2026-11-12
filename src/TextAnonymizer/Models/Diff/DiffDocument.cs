using System.Text;

namespace TextAnonymizer.Models.Diff;

/// <summary>Ce qu'un éditeur doit savoir d'une de ses lignes pour la décorer.</summary>
/// <param name="Kind">Nature de la rangée d'origine.</param>
/// <param name="IsFiller">Ligne de remplissage (la ligne existe seulement de l'autre côté).</param>
/// <param name="NumberA">Numéro de ligne dans A, 0 si aucun.</param>
/// <param name="NumberB">Numéro de ligne dans B, 0 si aucun.</param>
/// <param name="Spans">Parties modifiées, relatives au contenu (après le préfixe).</param>
/// <param name="IsDeletionSide">La ligne affichée vient de A (rouge) plutôt que de B (vert).</param>
/// <param name="OnlyLineEndingDiffers">Seule la fin de ligne diffère.</param>
public readonly record struct DiffLineInfo(
    DiffRowKind Kind,
    bool IsFiller,
    int NumberA,
    int NumberB,
    IReadOnlyList<TextSpan> Spans,
    bool IsDeletionSide,
    bool OnlyLineEndingDiffers);

/// <summary>Bloc de différences dans une vue : lignes (base 1) <c>FirstLine</c> à <c>FirstLine + LineCount - 1</c>.</summary>
public readonly record struct DiffViewBlock(int FirstLine, int LineCount);

/// <summary>Texte d'un éditeur de résultat et description de chacune de ses lignes.</summary>
public sealed class DiffView(string text, IReadOnlyList<DiffLineInfo> lines, IReadOnlyList<DiffViewBlock> blocks, int prefixLength)
{
    public string Text { get; } = text;

    /// <summary>Une entrée par ligne de <see cref="Text"/>.</summary>
    public IReadOnlyList<DiffLineInfo> Lines { get; } = lines;

    /// <summary>Une entrée par <see cref="DiffBlock"/>, dans le même ordre.</summary>
    public IReadOnlyList<DiffViewBlock> Blocks { get; } = blocks;

    /// <summary>Longueur du préfixe ajouté à chaque ligne (« + », « - »…) avant le contenu.</summary>
    public int PrefixLength { get; } = prefixLength;

    public static DiffView Empty { get; } = new(string.Empty, [], [], 0);
}

/// <summary>
/// Vues prêtes à afficher d'un <see cref="DiffResult"/> : deux textes alignés (côte à côte, lignes de remplissage
/// comprises) et un texte unifié préfixé par <c>+</c>, <c>-</c> ou un espace. Calculé hors du fil de l'interface.
/// </summary>
public sealed class DiffDocument
{
    public const string AddedPrefix = "+ ";
    public const string DeletedPrefix = "- ";
    public const string ContextPrefix = "  ";

    private DiffDocument(DiffView left, DiffView right, DiffView unified)
    {
        Left = left;
        Right = right;
        Unified = unified;
    }

    public static DiffDocument Empty { get; } = new(DiffView.Empty, DiffView.Empty, DiffView.Empty);

    public DiffView Left { get; }

    public DiffView Right { get; }

    public DiffView Unified { get; }

    public static DiffDocument Build(DiffResult result, CancellationToken cancellationToken = default)
    {
        var rows = result.Rows;
        var sideBlocks = result.Blocks.Select(b => new DiffViewBlock(b.StartRow + 1, b.RowCount)).ToList();
        var left = new SideBuilder();
        var right = new SideBuilder();
        for (var index = 0; index < rows.Count; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var row = rows[index];
            left.Add(row.Left?.Content, Info(row, row.Left is null, isDeletionSide: true));
            right.Add(row.Right?.Content, Info(row, row.Right is null, isDeletionSide: false));
        }

        return new DiffDocument(
            new DiffView(left.Text(), left.Lines, sideBlocks, 0),
            new DiffView(right.Text(), right.Lines, sideBlocks, 0),
            BuildUnified(result, cancellationToken));
    }

    private static DiffLineInfo Info(DiffRow row, bool isFiller, bool isDeletionSide) => new(
        row.Kind,
        isFiller,
        row.Left?.Number ?? 0,
        row.Right?.Number ?? 0,
        isFiller ? [] : isDeletionSide ? row.LeftSpans : row.RightSpans,
        isDeletionSide,
        row.OnlyLineEndingDiffers);

    /// <summary>Dans un bloc, toutes les lignes de A (« - ») puis toutes celles de B (« + »), comme un patch.</summary>
    private static DiffView BuildUnified(DiffResult result, CancellationToken cancellationToken)
    {
        var rows = result.Rows;
        var unified = new SideBuilder();
        var blocks = new List<DiffViewBlock>(result.Blocks.Count);
        var nextBlock = 0;
        var index = 0;
        while (index < rows.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nextBlock < result.Blocks.Count && result.Blocks[nextBlock].StartRow == index)
            {
                var block = result.Blocks[nextBlock++];
                var first = unified.Lines.Count + 1;
                for (var k = block.StartRow; k < block.EndRow; k++)
                {
                    if (rows[k].Left is { } line)
                    {
                        unified.Add(DeletedPrefix + line.Content, UnifiedInfo(rows[k], line.Number, 0, true));
                    }
                }

                for (var k = block.StartRow; k < block.EndRow; k++)
                {
                    if (rows[k].Right is { } line)
                    {
                        unified.Add(AddedPrefix + line.Content, UnifiedInfo(rows[k], 0, line.Number, false));
                    }
                }

                blocks.Add(new DiffViewBlock(first, unified.Lines.Count + 1 - first));
                index = block.EndRow;
                continue;
            }

            var row = rows[index++];
            var shown = row.Left ?? row.Right!;
            unified.Add(ContextPrefix + shown.Content, UnifiedInfo(row, row.Left?.Number ?? 0, row.Right?.Number ?? 0, row.Left is not null));
        }

        return new DiffView(unified.Text(), unified.Lines, blocks, ContextPrefix.Length);
    }

    private static DiffLineInfo UnifiedInfo(DiffRow row, int numberA, int numberB, bool isDeletionSide) => new(
        row.Kind,
        false,
        numberA,
        numberB,
        row.Kind == DiffRowKind.Modified ? isDeletionSide ? row.LeftSpans : row.RightSpans : [],
        isDeletionSide,
        row.OnlyLineEndingDiffers);

    private sealed class SideBuilder
    {
        private readonly StringBuilder _text = new();

        public List<DiffLineInfo> Lines { get; } = [];

        public void Add(string? content, DiffLineInfo info)
        {
            if (Lines.Count > 0)
            {
                _text.Append('\n');
            }

            _text.Append(content);
            Lines.Add(info);
        }

        public string Text() => _text.ToString();
    }
}
