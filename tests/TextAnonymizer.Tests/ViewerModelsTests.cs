using TextAnonymizer.Models;

namespace TextAnonymizer.Tests;

public class ViewerModelsTests
{
    [Theory]
    [InlineData("{\"a\": 1}", ViewerFormat.Json)]
    [InlineData("[1, 2, 3]", ViewerFormat.Json)]
    [InlineData("{\"a\": 1,}", ViewerFormat.Json)]
    [InlineData("<!DOCTYPE html><html><body>x</body></html>", ViewerFormat.Html)]
    [InlineData("<div><p>Bonjour</p></div>", ViewerFormat.Html)]
    [InlineData("<?php echo 'hi'; ?>", ViewerFormat.Php)]
    [InlineData("<html><?php echo 1; ?></html>", ViewerFormat.Php)]
    [InlineData("# Titre\n\n- a\n- b", ViewerFormat.Markdown)]
    [InlineData("[un lien](http://exemple.fr) puis du texte", ViewerFormat.Markdown)]
    [InlineData("Juste du texte", ViewerFormat.Markdown)]
    [InlineData("", ViewerFormat.Markdown)]
    public void Detect_ReturnsExpectedFormat(string text, ViewerFormat expected) =>
        Assert.Equal(expected, FormatDetector.Detect(text));

    [Fact]
    public void Inspect_InvalidJson_ReturnsReadableErrorWithPosition()
    {
        using var result = JsonInspector.Inspect("{\n  \"a\": 1,\n  \"b\": 2,\n}");

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Line);
        Assert.Contains("ligne 4", result.Error);
        Assert.Contains("virgule en trop", result.Error);
        Assert.DoesNotContain("System.Text.Json", result.Error);
    }

    [Fact]
    public void Inspect_TruncatedJson_ExplainsUnexpectedEnd()
    {
        using var result = JsonInspector.Inspect("{\"a\": [1, 2");

        Assert.False(result.IsValid);
        Assert.Contains("fin inattendue", result.Error);
    }

    [Fact]
    public void Inspect_SingleQuotes_ExplainsDoubleQuotes()
    {
        using var result = JsonInspector.Inspect("{'a': 1}");

        Assert.False(result.IsValid);
        Assert.Contains("guillemets doubles", result.Error);
        Assert.Equal(1, result.Line);
        Assert.Equal(2, result.Column);
    }

    [Fact]
    public void Format_IndentsAndKeepsAccents()
    {
        using var result = JsonInspector.Inspect("{\"ville\":\"Orléans\",\"n\":[1,2]}");

        var formatted = JsonInspector.Format(result.Root);

        Assert.Contains("\"ville\": \"Orléans\"", formatted);
        Assert.Contains(Environment.NewLine + "  \"n\"", formatted);
    }

    [Fact]
    public void Sanitize_RemovesScriptsAndHandlers()
    {
        var html = HtmlSanitizer.Sanitize(
            "<h1 onclick=\"x()\">Titre</h1><script>alert(1)</script><img src=\"a.png\" onerror=\"alert(2)\"><a href=\"javascript:alert(3)\">l</a>");

        Assert.Contains("<h1>Titre</h1>", html);
        Assert.DoesNotContain("script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", html);
        Assert.DoesNotContain("onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src=\"a.png\"", html);
    }

    [Theory]
    [InlineData("<scr<script>ipt>alert(1)</scr</script>ipt>")]
    [InlineData("<SCRIPT SRC=//evil.example/x.js></SCRIPT>")]
    [InlineData("<script>alert(1)")]
    [InlineData("<a href=\"jav&#x61;script:alert(1)\">x</a>")]
    [InlineData("<a href=\" java\tscript:alert(1)\">x</a>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("<iframe src=\"http://evil.example\"></iframe>")]
    [InlineData("<object data=\"x\"></object><embed src=\"x\">")]
    [InlineData("<!--[if IE]><script>alert(1)</script><![endif]-->")]
    [InlineData("<div style=\"width: expression(alert(1))\">x</div>")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0;url=http://evil.example\">")]
    [InlineData("<form action=\"http://evil.example\"><input></form>")]
    public void Sanitize_NeutralisesActiveContent(string input)
    {
        var html = HtmlSanitizer.Sanitize(input);

        Assert.DoesNotContain("alert", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<embed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", html);
        Assert.DoesNotContain("expression", html);
        Assert.DoesNotContain(" onload", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PhpTemplate_SeparatesHtmlFromCode()
    {
        const string template = "<?php $titre = 'x'; ?>\n<h1><?= $titre ?></h1>\n<?php if ($a) { ?><p>ok</p><?php } ?>";

        Assert.True(PhpTemplate.HasHtml(template));
        Assert.Equal("\n<h1></h1>\n<p>ok</p>", PhpTemplate.StripPhp(template));
        Assert.False(PhpTemplate.HasHtml("<?php\nfunction f() { return 1 < 2; }\n"));
    }

    [Fact]
    public void Analyze_ForcedFormatOverridesDetection()
    {
        using var analysis = ViewerAnalyzer.Analyze("{\"a\": 1}", ViewerFormat.Markdown);

        Assert.Equal(ViewerFormat.Markdown, analysis.Format);
        Assert.True(analysis.IsForced);
        Assert.Null(analysis.Json);
    }

    [Fact]
    public void Analyze_PhpWithHtml_ProvidesPreview_PhpOnlyDoesNot()
    {
        using var mixed = ViewerAnalyzer.Analyze("<?php $a = 1; ?><p>salut</p>", ViewerFormat.Auto);
        using var pure = ViewerAnalyzer.Analyze("<?php echo 1;", ViewerFormat.Auto);

        Assert.Equal(ViewerFormat.Php, mixed.Format);
        Assert.Contains("<p>salut</p>", mixed.HtmlPreview);
        Assert.DoesNotContain("$a", mixed.HtmlPreview);
        Assert.Null(pure.HtmlPreview);
    }
}
