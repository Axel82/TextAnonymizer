using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Tests;

public class StructuralDiffTests
{
    private static StructuralComparison Json(string a, string b, bool ignoreOrder = true) =>
        JsonStructuralDiff.Compare(a, b, ignoreOrder);

    [Fact]
    public void Json_PresentationOnly_HasNoStructuralChange()
    {
        var a = "{\"nom\":\"Durand\",\"age\":42,\"tags\":[\"a\",\"b\"],\"ville\":\"Lyon\"}";
        var b = "{\n    \"ville\" : \"Lyon\",\n    \"age\": 42.0,\n    \"nom\": \"Dur\\u0061nd\",\n    \"tags\": [ \"a\", \"b\" ]\n}";

        var result = Json(a, b);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.TotalChanges);
        Assert.Contains("présentation", result.Summary);
        Assert.Equal(result.NormalizedA, result.NormalizedB);
    }

    [Fact]
    public void Json_PropertyOrder_IsReportedOnlyWhenRequested()
    {
        var a = "{\"a\":1,\"b\":2}";
        var b = "{\"b\":2,\"a\":1}";

        Assert.Equal(0, Json(a, b).TotalChanges);
        var change = Assert.Single(Json(a, b, ignoreOrder: false).Changes);
        Assert.Equal(StructuralChangeKind.Reordered, change.Kind);
        Assert.Equal("$", change.Path);
    }

    [Fact]
    public void Json_ReportsAddedRemovedModifiedAndTypeChanges_WithPaths()
    {
        var a = "{\"client\":{\"nom\":\"Durand\",\"tel\":\"0102\",\"actif\":true},\"total\":10,\"lignes\":[1,2,3]}";
        var b = "{\"client\":{\"nom\":\"Martin\",\"email\":\"m@x.fr\",\"actif\":false},\"total\":\"10\",\"lignes\":[1,3,4]}";

        var changes = Json(a, b).Changes.ToDictionary(c => c.Path);

        Assert.Equal(StructuralChangeKind.Modified, changes["$.client.nom"].Kind);
        Assert.Equal("\"Durand\"", changes["$.client.nom"].OldValue);
        Assert.Equal("\"Martin\"", changes["$.client.nom"].NewValue);
        Assert.Equal(StructuralChangeKind.Removed, changes["$.client.tel"].Kind);
        Assert.Equal(StructuralChangeKind.Added, changes["$.client.email"].Kind);
        Assert.Equal(StructuralChangeKind.Modified, changes["$.client.actif"].Kind);
        Assert.Equal(StructuralChangeKind.TypeChanged, changes["$.total"].Kind);
        Assert.Equal(StructuralChangeKind.Removed, changes["$.lignes[1]"].Kind);
        Assert.Equal(StructuralChangeKind.Added, changes["$.lignes[2]"].Kind);
    }

    [Fact]
    public void Json_Invalid_GivesTheReadableError()
    {
        var result = Json("{\"a\": 1,}", "{}");

        Assert.False(result.IsValid);
        Assert.StartsWith("A : JSON invalide (ligne 1", result.Error);
    }

    [Fact]
    public void Xml_PresentationOnly_HasNoStructuralChange()
    {
        var a = "<?xml version=\"1.0\"?><config><item id=\"1\" nom=\"x\">valeur</item><!-- note --></config>";
        var b = "<config>\n  <item nom=\"x\"   id=\"1\">\n    valeur\n  </item>\n</config>\n";

        var result = XmlStructuralDiff.Compare(a, b);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.TotalChanges);
        Assert.Equal(result.NormalizedA, result.NormalizedB);
    }

    [Fact]
    public void Xml_ReportsElementsAttributesAndText()
    {
        var a = "<config><item id=\"1\" mode=\"a\">un</item><item id=\"2\"/><old/></config>";
        var b = "<config><item id=\"1\" mode=\"b\" actif=\"oui\">deux</item><item id=\"2\"/><new/></config>";

        var changes = XmlStructuralDiff.Compare(a, b).Changes;

        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Modified, Path: "/config/item[1]/@mode", OldValue: "a", NewValue: "b" });
        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Added, Path: "/config/item[1]/@actif" });
        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Modified, Path: "/config/item[1]/text()", OldValue: "un", NewValue: "deux" });
        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Removed, Path: "/config/old[1]" });
        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Added, Path: "/config/new[1]" });
    }

    [Fact]
    public void Xml_DoesNotResolveExternalEntities()
    {
        var hostile = "<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]><x>&e;</x>";

        var result = XmlStructuralDiff.Compare(hostile, "<x/>");

        Assert.DoesNotContain(result.Changes, c => (c.OldValue ?? string.Empty).Contains("fonts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Markdown_ComparesSections()
    {
        var a = "# Guide\nIntro\n## Installation\nÉtape 1\n## Usage\nTexte\n## Ancien\nx\n";
        var b = "# Guide\nIntro\n## Installation\nÉtape 1\nÉtape 2\n## Usage\nTexte\n## Nouveau\ny\n";

        var changes = MarkdownStructuralDiff.Compare(a, b).Changes;

        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Modified, Path: "Guide › Installation" });
        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Removed, Path: "Guide › Ancien" });
        Assert.Contains(changes, c => c is { Kind: StructuralChangeKind.Added, Path: "Guide › Nouveau" });
        Assert.DoesNotContain(changes, c => c.Path == "Guide › Usage");
    }

    [Fact]
    public void Markdown_IgnoresHeadingsInsideCodeBlocks()
    {
        var a = "# Titre\n```\n# pas un titre\n```\n";

        var result = MarkdownStructuralDiff.Compare(a, a);

        Assert.Equal(0, result.TotalChanges);
    }

    [Theory]
    [InlineData("{\"a\": 1}", null, DiffFormat.Json)]
    [InlineData("<root><a/></root>", null, DiffFormat.Xml)]
    [InlineData("# Titre\n\ntexte", null, DiffFormat.Markdown)]
    [InlineData("simple texte\nsur deux lignes", null, DiffFormat.Text)]
    [InlineData("pas du json", "c:\\x\\donnees.json", DiffFormat.Json)]
    [InlineData("{ invalide", null, DiffFormat.Text)]
    public void Format_IsDetected(string text, string? path, DiffFormat expected) =>
        Assert.Equal(expected, DiffFormatDetector.Detect(text, path));

    [Fact]
    public void Format_DifferentOnEachSide_FallsBackToText() =>
        Assert.Equal(DiffFormat.Text, DiffFormatDetector.DetectCommon("{\"a\":1}", "<a/>"));

    [Fact]
    public void Comparison_CanUseTheNormalizedForm()
    {
        var a = "{\"b\":1,\"a\":2}";
        var b = "{\n  \"a\": 2,\n  \"b\": 1\n}";

        var raw = DiffComparison.Run(new DiffRequest(a, b, DiffOptions.Default, DiffFormat.Auto));
        var normalized = DiffComparison.Run(new DiffRequest(a, b, DiffOptions.Default, DiffFormat.Auto, CompareNormalized: true));

        Assert.Equal(DiffFormat.Json, raw.Format);
        Assert.False(raw.Result.IsIdentical);
        Assert.Equal(0, raw.Structural!.TotalChanges);
        Assert.True(normalized.IsNormalized);
        Assert.True(normalized.Result.IsIdentical);
    }
}
