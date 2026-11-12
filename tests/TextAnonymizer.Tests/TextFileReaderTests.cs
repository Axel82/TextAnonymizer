using System.IO;
using System.Text;
using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Tests;

public sealed class TextFileReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TextAnonymizerTests", Guid.NewGuid().ToString("N"));

    public TextFileReaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private const string Sample = "Élève à l'école\r\nœuvre ça\r\n";

    public static TheoryData<string, string> Encodings => new()
    {
        { "utf8", "UTF-8" },
        { "utf8bom", "UTF-8 (BOM)" },
        { "utf16le", "UTF-16 LE (BOM)" },
        { "utf16be", "UTF-16 BE (BOM)" },
        { "utf32le", "UTF-32 LE (BOM)" },
        { "utf16nobom", "UTF-16 LE" },
        { "cp1252", "Windows-1252" }
    };

    private static byte[] Encode(string kind)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return kind switch
        {
            "utf8" => new UTF8Encoding(false).GetBytes(Sample),
            "utf8bom" => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Sample)],
            "utf16le" => [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Sample)],
            "utf16be" => [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(Sample)],
            "utf32le" => [.. Encoding.UTF32.GetPreamble(), .. Encoding.UTF32.GetBytes(Sample)],
            "utf16nobom" => Encoding.Unicode.GetBytes(Sample),
            _ => Encoding.GetEncoding(1252).GetBytes(Sample)
        };
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public void Encoding_IsDetected_AndTextDecoded(string kind, string expectedName)
    {
        var path = Write(kind + ".txt", Encode(kind));

        var content = TextFileReader.Read(path);

        Assert.Equal(Sample, content.Text);
        Assert.Equal(expectedName, content.EncodingName);
        Assert.Equal(LineEnding.CrLf, content.LineEnding);
    }

    [Fact]
    public void SameTextInDifferentEncodings_IsIdentical()
    {
        var a = TextFileReader.Read(Write("a.txt", Encode("utf8bom")));
        var b = TextFileReader.Read(Write("b.txt", Encode("cp1252")));

        Assert.True(LineDiffer.Compare(a.Text, b.Text).IsIdentical);
    }

    [Fact]
    public void EmptyFile_IsEmptyText()
    {
        var content = TextFileReader.Read(Write("vide.txt", []));

        Assert.Equal(string.Empty, content.Text);
        Assert.Equal(LineEnding.None, content.LineEnding);
    }

    [Fact]
    public void BinaryFile_IsRejected()
    {
        var path = Write("image.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48]);

        var error = Assert.Throws<TextFileException>(() => TextFileReader.Read(path));
        Assert.Contains("binaire", error.Message);
    }

    [Fact]
    public void MissingFile_GivesAReadableError()
    {
        var error = Assert.Throws<TextFileException>(() => TextFileReader.Read(Path.Combine(_directory, "absent.txt")));
        Assert.Contains("introuvable", error.Message);
    }

    [Fact]
    public void FileOpenedForWritingElsewhere_CanStillBeRead_AndIsNotModified()
    {
        var path = Write("ouvert.txt", Encoding.UTF8.GetBytes("contenu"));
        var before = File.GetLastWriteTimeUtc(path);

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            Assert.Equal("contenu", TextFileReader.Read(path).Text);
        }

        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        Assert.Equal("contenu", File.ReadAllText(path));
    }
}
