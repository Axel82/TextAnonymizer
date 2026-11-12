using TextAnonymizer.Models;

namespace TextAnonymizer.Services;

public interface IConversionService
{
    /// <summary>Remplace chaque original trouvé par son remplaçant (mots entiers, correspondance la plus longue prioritaire).</summary>
    ReplacementResult Anonymize(string text, ConversionTable table);

    /// <summary>Remplace chaque remplaçant trouvé par son original (inversion de la table).</summary>
    ReplacementResult Restore(string text, ConversionTable table);

    /// <summary>
    /// Rétablit un texte. Si <paramref name="currentText"/> est identique au résultat de <paramref name="anonymization"/>,
    /// le texte source exact est restitué (casse et accents d'origine, même en mode IgnoreCase) ;
    /// sinon, repli sur l'inversion de la table.
    /// </summary>
    ReplacementResult Restore(ReplacementResult anonymization, string currentText, ConversionTable table);

    /// <summary>Nombre d'occurrences de chaque original dans <paramref name="text"/> (les remplaçants ne sont pas requis).</summary>
    IReadOnlyDictionary<ConversionEntry, int> CountOccurrences(string text, ConversionTable table);
}
