namespace TextAnonymizer.Models;

/// <summary>Vérifie qu'un ensemble de paires original/remplaçant est bijectif.</summary>
public static class BijectionValidator
{
    /// <param name="pairs">Paires dans l'ordre de la table (le numéro de ligne commence à 1).</param>
    /// <param name="strategy">Les doublons sont détectés selon cette stratégie.</param>
    /// <param name="allowEmptyReplacements">Tolère les remplaçants vides (table en cours de saisie).</param>
    public static BijectionResult Validate(
        IEnumerable<(string Original, string Replacement)> pairs,
        MatchingStrategy strategy,
        bool allowEmptyReplacements = false)
    {
        var errors = new List<string>();
        var originals = new Dictionary<string, (int Line, string Text)>(StringComparer.Ordinal);
        var replacements = new Dictionary<string, (int Line, string Original)>(StringComparer.Ordinal);
        var line = 0;

        foreach (var (original, replacement) in pairs)
        {
            line++;

            if (string.IsNullOrWhiteSpace(original))
            {
                errors.Add($"Ligne {line} : le mot original est vide.");
                continue;
            }

            var originalKey = TextNormalizer.Normalize(original, strategy);
            if (originals.TryGetValue(originalKey, out var previous))
            {
                errors.Add($"Doublon côté original : « {original} » (ligne {line}) est déjà présent à la ligne {previous.Line} (« {previous.Text} »).");
            }
            else
            {
                originals[originalKey] = (line, original);
            }

            if (string.IsNullOrWhiteSpace(replacement))
            {
                if (!allowEmptyReplacements)
                {
                    errors.Add($"Ligne {line} : le remplaçant de « {original} » est vide.");
                }

                continue;
            }

            var replacementKey = TextNormalizer.Normalize(replacement, strategy);
            if (replacements.TryGetValue(replacementKey, out var other))
            {
                errors.Add($"Doublon côté remplaçant : « {replacement} » (ligne {line}, original « {original} ») est déjà utilisé à la ligne {other.Line} pour « {other.Original} ».");
            }
            else
            {
                replacements[replacementKey] = (line, original);
            }
        }

        return new BijectionResult(errors);
    }
}
