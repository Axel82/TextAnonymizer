namespace TextAnonymizer.Models.Diff;

/// <summary>Une ligne d'un texte : numéro (base 1), contenu sans fin de ligne, et fin de ligne d'origine (vide pour la dernière).</summary>
public sealed record TextLine(int Number, string Content, string Terminator);

public enum LineEnding
{
    None,
    Lf,
    CrLf,
    Cr,
    Mixed
}

public static class LineSplitter
{
    /// <summary>
    /// Découpe sur CRLF, LF et CR. Un saut de ligne final ne crée pas de ligne vide supplémentaire
    /// (comme <c>diff</c>) ; un texte vide n'a aucune ligne.
    /// </summary>
    public static IReadOnlyList<TextLine> Split(string? text)
    {
        var lines = new List<TextLine>();
        if (string.IsNullOrEmpty(text))
        {
            return lines;
        }

        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '\r' or '\n')
            {
                var terminator = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : c == '\r' ? "\r" : "\n";
                lines.Add(new TextLine(lines.Count + 1, text[start..i], terminator));
                i += terminator.Length;
                start = i;
            }
            else
            {
                i++;
            }
        }

        if (start < text.Length)
        {
            lines.Add(new TextLine(lines.Count + 1, text[start..], string.Empty));
        }

        return lines;
    }

    public static LineEnding Detect(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return LineEnding.None;
        }

        bool lf = false, crlf = false, cr = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf = true;
                    i++;
                }
                else
                {
                    cr = true;
                }
            }
            else if (text[i] == '\n')
            {
                lf = true;
            }
        }

        var count = (lf ? 1 : 0) + (crlf ? 1 : 0) + (cr ? 1 : 0);
        return count switch
        {
            0 => LineEnding.None,
            > 1 => LineEnding.Mixed,
            _ => crlf ? LineEnding.CrLf : lf ? LineEnding.Lf : LineEnding.Cr
        };
    }

    public static string Label(LineEnding ending) => ending switch
    {
        LineEnding.CrLf => "CRLF (Windows)",
        LineEnding.Lf => "LF (Unix)",
        LineEnding.Cr => "CR (Mac classique)",
        LineEnding.Mixed => "Fins de ligne mixtes",
        _ => "Une seule ligne"
    };
}
