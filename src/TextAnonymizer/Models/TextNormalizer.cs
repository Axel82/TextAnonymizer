using System.Globalization;
using System.Text;

namespace TextAnonymizer.Models;

/// <summary>
/// Normalise un texte selon une <see cref="MatchingStrategy"/>.
/// La normalisation conserve la longueur : l'index d'un caractère normalisé est celui du caractère d'origine.
/// </summary>
public static class TextNormalizer
{
    public static string Normalize(string text, MatchingStrategy strategy)
    {
        if (strategy == MatchingStrategy.Strict || text.Length == 0)
        {
            return text;
        }

        var fold = strategy == MatchingStrategy.IgnoreCaseAndDiacritics;
        return string.Create(text.Length, (text, fold), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                var c = char.ToLowerInvariant(state.text[i]);
                span[i] = state.fold ? StripDiacritic(c) : c;
            }
        });
    }

    private static char StripDiacritic(char c)
    {
        if (c < 0x80)
        {
            return c;
        }

        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
        if (decomposed.Length < 2)
        {
            return c;
        }

        for (var i = 1; i < decomposed.Length; i++)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(decomposed[i]) != UnicodeCategory.NonSpacingMark)
            {
                return c;
            }
        }

        return decomposed[0];
    }
}
