using System.Windows.Media;
using TextAnonymizer.Models;

namespace TextAnonymizer.Converters;

/// <summary>Couleur de surlignage associée à chaque type d'entité.</summary>
public static class EntityColors
{
    private static readonly Dictionary<EntityType, Brush> Brushes = Enum.GetValues<EntityType>()
        .ToDictionary(type => type, type => Freeze(new SolidColorBrush(ColorFor(type))));

    public static Brush BrushFor(EntityType type) => Brushes[type];

    private static Color ColorFor(EntityType type) => type switch
    {
        EntityType.Nom => Color.FromRgb(0xBB, 0xDE, 0xFB),
        EntityType.Ville => Color.FromRgb(0xC8, 0xE6, 0xC9),
        EntityType.Email => Color.FromRgb(0xFF, 0xE0, 0xB2),
        EntityType.Tel => Color.FromRgb(0xE1, 0xBE, 0xE7),
        _ => Color.FromRgb(0xE0, 0xE0, 0xE0)
    };

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
