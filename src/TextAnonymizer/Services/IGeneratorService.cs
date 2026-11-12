using TextAnonymizer.Models;

namespace TextAnonymizer.Services;

/// <summary>Valeur fictive proposée pour une entrée de la table.</summary>
public sealed record GeneratedValue(ConversionEntry Entry, string Value);

public interface IGeneratorService
{
    /// <summary>
    /// Génère une valeur fictive réaliste, absente de <paramref name="existing"/> (comparaison insensible à la casse et aux accents).
    /// </summary>
    /// <param name="relatedName">Pour <see cref="EntityType.Email"/> : nom (« Prénom Nom ») dont l'adresse est dérivée.</param>
    string Generate(EntityType type, IReadOnlyCollection<string> existing, string? relatedName = null);

    /// <summary>
    /// Propose une valeur pour chaque entrée de la table dont le remplaçant est vide.
    /// Les valeurs sont uniques et distinctes de tous les originaux/remplaçants de la table ;
    /// un email reprend le prochain nom (type <see cref="EntityType.Nom"/>) de la table non encore associé à un email.
    /// </summary>
    IReadOnlyList<GeneratedValue> GenerateMissing(ConversionTable table);
}
