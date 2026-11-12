using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TextAnonymizer.Models;

/// <summary>Résultat de l'analyse d'un JSON : document (à disposer) ou erreur lisible avec position.</summary>
public sealed class JsonInspection : IDisposable
{
    private readonly JsonDocument? _document;

    private JsonInspection(JsonDocument? document, string? error, int line, int column)
    {
        _document = document;
        Error = error;
        Line = line;
        Column = column;
    }

    public bool IsValid => _document is not null;

    public JsonElement Root => _document?.RootElement ?? default;

    /// <summary>Message compréhensible en français, <c>null</c> si le JSON est valide.</summary>
    public string? Error { get; }

    /// <summary>Position de l'erreur (base 1).</summary>
    public int Line { get; }

    public int Column { get; }

    public static JsonInspection Success(JsonDocument document) => new(document, null, 0, 0);

    public static JsonInspection Failure(string error, int line, int column) => new(null, error, line, column);

    public void Dispose() => _document?.Dispose();
}

public static class JsonInspector
{
    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonInspection Inspect(string text)
    {
        try
        {
            return JsonInspection.Success(JsonDocument.Parse(text));
        }
        catch (JsonException ex)
        {
            return Describe(text, ex);
        }
    }

    /// <summary>Texte JSON indenté (les nombres et chaînes sont conservés tels quels).</summary>
    public static string Format(JsonElement element) => JsonSerializer.Serialize(element, PrettyOptions);

    private static JsonInspection Describe(string text, JsonException ex)
    {
        var zeroBasedLine = (int)(ex.LineNumber ?? 0);
        var line = zeroBasedLine + 1;
        var column = ColumnInChars(text, zeroBasedLine, ex.BytePositionInLine ?? 0) + 1;
        var hint = Hint(text, OffsetOf(text, line, column));
        return JsonInspection.Failure($"JSON invalide (ligne {line}, colonne {column}) : {hint}", line, column);
    }

    /// <summary>La position de <see cref="JsonException"/> est en octets UTF-8 ; on la convertit en caractères.</summary>
    private static int ColumnInChars(string text, int zeroBasedLine, long bytePosition)
    {
        var lines = text.Split('\n');
        if (zeroBasedLine >= lines.Length)
        {
            return 0;
        }

        var bytes = Encoding.UTF8.GetBytes(lines[zeroBasedLine]);
        var count = (int)Math.Min(bytePosition, bytes.Length);
        return Encoding.UTF8.GetCharCount(bytes, 0, count);
    }

    private static int OffsetOf(string text, int line, int column)
    {
        var offset = 0;
        for (var current = 1; current < line; current++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0)
            {
                return text.Length;
            }

            offset = next + 1;
        }

        return Math.Min(offset + column - 1, text.Length);
    }

    private static string Hint(string text, int offset)
    {
        if (offset >= text.Length || text[offset..].Trim().Length == 0)
        {
            return "fin inattendue du texte, il manque probablement une accolade, un crochet ou un guillemet fermant.";
        }

        var found = text[offset];
        var previous = LastNonSpaceBefore(text, offset);

        if ((found is '}' or ']') && previous == ',')
        {
            return $"virgule en trop avant « {found} ».";
        }

        if (found == '\'')
        {
            return "les chaînes et les clés doivent utiliser des guillemets doubles (\"), pas des apostrophes.";
        }

        if (found == ',' && previous is ',' or '{' or '[')
        {
            return "virgule inattendue : une valeur est attendue avant.";
        }

        if (found is '/' or '#')
        {
            return "les commentaires ne sont pas autorisés en JSON.";
        }

        if (char.IsLetter(found) && previous is '{' or ',')
        {
            return "les clés doivent être entourées de guillemets doubles (\").";
        }

        if (previous is '"' or '}' or ']' or (>= '0' and <= '9') && found is '"' or '{' or '[' or (>= '0' and <= '9'))
        {
            return "virgule manquante entre deux éléments.";
        }

        var start = Math.Max(0, offset - 10);
        var around = text.Substring(start, Math.Min(30, text.Length - start)).ReplaceLineEndings(" ");
        return $"caractère inattendu « {found} » près de : {around}";
    }

    private static char LastNonSpaceBefore(string text, int offset)
    {
        for (var i = offset - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return text[i];
            }
        }

        return '\0';
    }
}
