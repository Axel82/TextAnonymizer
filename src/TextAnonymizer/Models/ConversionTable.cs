using System.Collections.ObjectModel;

namespace TextAnonymizer.Models;

/// <summary>Table de conversion : entrées et stratégie de matching.</summary>
public sealed class ConversionTable
{
    public ObservableCollection<ConversionEntry> Entries { get; } = [];

    public MatchingStrategy Strategy { get; set; } = MatchingStrategy.Strict;

    /// <summary>Valide la bijection. Avec <paramref name="allowEmptyReplacements"/>, les remplaçants vides sont tolérés.</summary>
    public BijectionResult ValidateBijection(bool allowEmptyReplacements = false) =>
        BijectionValidator.Validate(
            Entries.Select(e => (e.Original, e.Replacement)),
            Strategy,
            allowEmptyReplacements);
}
