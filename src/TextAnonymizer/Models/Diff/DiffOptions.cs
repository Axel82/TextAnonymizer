namespace TextAnonymizer.Models.Diff;

/// <summary>
/// Options de comparaison, toutes indépendantes. Elles n'agissent que sur la clé de comparaison :
/// le texte affiché et exporté reste toujours celui d'origine.
/// </summary>
public sealed record DiffOptions
{
    public static DiffOptions Default { get; } = new();

    /// <summary>« Abc » et « abc » sont identiques.</summary>
    public bool IgnoreCase { get; init; }

    /// <summary>Les espaces en début et fin de ligne ne comptent pas.</summary>
    public bool TrimWhitespace { get; init; }

    /// <summary>Une suite d'espaces vaut un seul espace.</summary>
    public bool CollapseWhitespace { get; init; }

    /// <summary>Une tabulation vaut un espace.</summary>
    public bool IgnoreTabs { get; init; }

    /// <summary>Les lignes vides (ou blanches) sont écartées de la comparaison.</summary>
    public bool IgnoreBlankLines { get; init; }

    /// <summary>CRLF, LF et CR sont équivalents, de même que l'absence de saut de ligne final.</summary>
    public bool IgnoreLineEndings { get; init; } = true;

    /// <summary>Comparaison insensible aux espaces : tous les blancs sont ignorés.</summary>
    public bool IgnoreAllWhitespace { get; init; }

    /// <summary>Une option autre que les fins de ligne est active : le texte égal peut différer octet par octet.</summary>
    public bool AltersContent =>
        IgnoreCase || TrimWhitespace || CollapseWhitespace || IgnoreTabs || IgnoreBlankLines || IgnoreAllWhitespace;
}
