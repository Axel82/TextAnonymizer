using System.IO;
using System.Text;

namespace TextAnonymizer.Models.Diff;

/// <summary>Contenu d'un fichier texte décodé, avec ce qu'on a appris de sa forme d'origine.</summary>
public sealed record TextFileContent(string Path, string Text, string EncodingName, LineEnding LineEnding, long Length)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>Erreur de lecture présentable à l'utilisateur (message en français).</summary>
public sealed class TextFileException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Lecture seule d'un fichier texte : le fichier n'est jamais ouvert en écriture ni verrouillé.
/// Encodage : BOM (UTF-8/16/32), sinon UTF-16 sans BOM reconnaissable, sinon UTF-8 strict, sinon Windows-1252.
/// Les fichiers binaires sont refusés.
/// </summary>
public static class TextFileReader
{
    public const long MaxFileSize = 200L * 1024 * 1024;

    /// <summary>Taille de l'échantillon examiné pour reconnaître un fichier binaire ou de l'UTF-16 sans BOM.</summary>
    private const int SampleSize = 8192;

    private static readonly Lazy<Encoding> Windows1252 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
    });

    public static TextFileContent Read(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        byte[] bytes;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new TextFileException($"Le fichier « {name} » est introuvable.");
            }

            if (info.Length > MaxFileSize)
            {
                throw new TextFileException(
                    $"Le fichier « {name} » fait {info.Length / (1024 * 1024)} Mo : la limite est de {MaxFileSize / (1024 * 1024)} Mo.");
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new TextFileException($"Accès refusé au fichier « {name} ».", ex);
        }
        catch (IOException ex)
        {
            throw new TextFileException($"Impossible de lire le fichier « {name} » : {ex.Message}", ex);
        }

        var (text, encoding) = Decode(bytes, name);
        return new TextFileContent(path, text, encoding, LineSplitter.Detect(text), bytes.LongLength);
    }

    /// <summary>Décode des octets ; lève <see cref="TextFileException"/> si le contenu est binaire.</summary>
    public static (string Text, string EncodingName) Decode(byte[] bytes, string name = "contenu")
    {
        if (Bom(bytes) is { } bom)
        {
            return (bom.Encoding.GetString(bytes, bom.Length, bytes.Length - bom.Length), bom.Name);
        }

        if (Utf16WithoutBom(bytes) is { } bigEndian)
        {
            try
            {
                var strict = new UnicodeEncoding(bigEndian, false, true);
                return (strict.GetString(bytes), bigEndian ? "UTF-16 BE" : "UTF-16 LE");
            }
            catch (DecoderFallbackException)
            {
                // Faux positif : on poursuit avec la détection binaire.
            }
        }

        if (IsBinary(bytes))
        {
            throw new TextFileException($"« {name} » est un fichier binaire : il ne peut pas être comparé comme du texte.");
        }

        try
        {
            return (new UTF8Encoding(false, true).GetString(bytes), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            return (Windows1252.Value.GetString(bytes), "Windows-1252");
        }
    }

    /// <summary>Octet nul, ou plus de 10 % de caractères de contrôle, dans le début du fichier.</summary>
    public static bool IsBinary(ReadOnlySpan<byte> bytes)
    {
        var sample = bytes[..Math.Min(bytes.Length, SampleSize)];
        if (sample.IsEmpty)
        {
            return false;
        }

        var control = 0;
        foreach (var b in sample)
        {
            if (b == 0)
            {
                return true;
            }

            if ((b < 0x20 && b is not (0x09 or 0x0A or 0x0C or 0x0D or 0x1B)) || b == 0x7F)
            {
                control++;
            }
        }

        return control * 10 > sample.Length;
    }

    private static (Encoding Encoding, int Length, string Name)? Bom(byte[] b)
    {
        if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0 && b[3] == 0)
        {
            return (new UTF32Encoding(false, true), 4, "UTF-32 LE (BOM)");
        }

        if (b.Length >= 4 && b[0] == 0 && b[1] == 0 && b[2] == 0xFE && b[3] == 0xFF)
        {
            return (new UTF32Encoding(true, true), 4, "UTF-32 BE (BOM)");
        }

        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
        {
            return (new UTF8Encoding(true), 3, "UTF-8 (BOM)");
        }

        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
        {
            return (new UnicodeEncoding(false, true), 2, "UTF-16 LE (BOM)");
        }

        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
        {
            return (new UnicodeEncoding(true, true), 2, "UTF-16 BE (BOM)");
        }

        return null;
    }

    /// <summary>Texte latin en UTF-16 sans BOM (un octet sur deux est nul, toujours du même côté) : renvoie « gros-boutiste ? ».</summary>
    private static bool? Utf16WithoutBom(byte[] bytes)
    {
        var length = Math.Min(bytes.Length, SampleSize) & ~1;
        if (length < 4)
        {
            return null;
        }

        int zeroEven = 0, zeroOdd = 0;
        for (var i = 0; i < length; i += 2)
        {
            if (bytes[i] == 0)
            {
                zeroEven++;
            }

            if (bytes[i + 1] == 0)
            {
                zeroOdd++;
            }
        }

        var pairs = length / 2;
        if (zeroOdd * 10 >= pairs * 7 && zeroEven * 20 < pairs)
        {
            return false;
        }

        if (zeroEven * 10 >= pairs * 7 && zeroOdd * 20 < pairs)
        {
            return true;
        }

        return null;
    }
}
