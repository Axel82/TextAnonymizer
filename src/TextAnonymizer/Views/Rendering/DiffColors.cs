using System.Windows.Media;
using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Views.Rendering;

/// <summary>Couleurs du comparateur : vert = ajout, rouge = suppression, jaune orangé = modification (versions claire et sombre).</summary>
public sealed class DiffColors
{
    public static DiffColors Light { get; } = new(
        added: "#E6FFEC", addedSpan: "#ABF2BC",
        deleted: "#FFEBE9", deletedSpan: "#FFC1BE",
        modified: "#FFF5CC", filler: "#F0F2F4",
        current: "#0969DA", marker: "#9A6700");

    public static DiffColors Dark { get; } = new(
        added: "#2E2EA043", addedSpan: "#662EA043",
        deleted: "#2EF85149", deletedSpan: "#66F85149",
        modified: "#33D29922", filler: "#14FFFFFF",
        current: "#58A6FF", marker: "#D29922");

    private DiffColors(string added, string addedSpan, string deleted, string deletedSpan, string modified, string filler, string current, string marker)
    {
        Added = Brush(added);
        AddedSpan = Brush(addedSpan);
        Deleted = Brush(deleted);
        DeletedSpan = Brush(deletedSpan);
        Modified = Brush(modified);
        Filler = Brush(filler);
        Marker = Brush(marker);
        CurrentBlock = new Pen(Brush(current), 2);
        CurrentBlock.Freeze();
    }

    public Brush Added { get; }

    public Brush AddedSpan { get; }

    public Brush Deleted { get; }

    public Brush DeletedSpan { get; }

    public Brush Modified { get; }

    public Brush Filler { get; }

    public Brush Marker { get; }

    public Pen CurrentBlock { get; }

    public static DiffColors For(bool dark) => dark ? Dark : Light;

    /// <summary>Fond d'une ligne ; <c>null</c> pour une ligne identique.</summary>
    public Brush? LineBackground(DiffLineInfo line) => line switch
    {
        { IsFiller: true } => Filler,
        { Kind: DiffRowKind.Added } => Added,
        { Kind: DiffRowKind.Deleted } => Deleted,
        { Kind: DiffRowKind.Modified } => Modified,
        _ => null
    };

    public Brush SpanBackground(DiffLineInfo line) => line.IsDeletionSide ? DeletedSpan : AddedSpan;

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
