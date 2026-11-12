namespace TextAnonymizer.Models;

/// <summary>Stratégie de comparaison des mots de la table avec le texte.</summary>
public enum MatchingStrategy
{
    /// <summary>Respecte la casse et les accents.</summary>
    Strict,

    /// <summary>Insensible à la casse.</summary>
    IgnoreCase,

    /// <summary>Insensible à la casse et aux accents.</summary>
    IgnoreCaseAndDiacritics
}
