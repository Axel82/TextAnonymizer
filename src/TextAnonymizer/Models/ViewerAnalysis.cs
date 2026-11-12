namespace TextAnonymizer.Models;

/// <summary>
/// Ce que le visualiseur doit afficher pour un texte : format effectif et données prêtes à rendre.
/// Possède le document JSON éventuel : à disposer quand l'analyse est remplacée.
/// </summary>
public sealed class ViewerAnalysis : IDisposable
{
    public static ViewerAnalysis Empty { get; } = new(string.Empty, ViewerFormat.Markdown, false, null, null, null);

    public ViewerAnalysis(
        string text,
        ViewerFormat format,
        bool isForced,
        JsonInspection? json,
        string? formattedJson,
        string? htmlPreview)
    {
        Text = text;
        Format = format;
        IsForced = isForced;
        Json = json;
        FormattedJson = formattedJson;
        HtmlPreview = htmlPreview;
    }

    public string Text { get; }

    /// <summary>Format effectif (jamais <see cref="ViewerFormat.Auto"/>).</summary>
    public ViewerFormat Format { get; }

    /// <summary>Le format a été imposé par l'utilisateur plutôt que détecté.</summary>
    public bool IsForced { get; }

    public JsonInspection? Json { get; }

    /// <summary>JSON indenté, <c>null</c> si le texte n'est pas du JSON valide.</summary>
    public string? FormattedJson { get; }

    /// <summary>Document HTML neutralisé, prêt pour l'iframe/<c>WebBrowser</c> ; <c>null</c> si le format n'a pas d'aperçu HTML.</summary>
    public string? HtmlPreview { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);

    public void Dispose() => Json?.Dispose();
}

public static class ViewerAnalyzer
{
    public static ViewerAnalysis Analyze(string? text, ViewerFormat selected)
    {
        text ??= string.Empty;
        var isForced = selected != ViewerFormat.Auto;
        var format = isForced ? selected : FormatDetector.Detect(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ViewerAnalysis(text, format, isForced, null, null, null);
        }

        switch (format)
        {
            case ViewerFormat.Json:
                var json = JsonInspector.Inspect(text);
                return new ViewerAnalysis(text, format, isForced, json, json.IsValid ? JsonInspector.Format(json.Root) : null, null);
            case ViewerFormat.Html:
                return new ViewerAnalysis(text, format, isForced, null, null, HtmlSanitizer.Sanitize(text));
            case ViewerFormat.Php:
                var html = PhpTemplate.HasHtml(text) ? HtmlSanitizer.Sanitize(PhpTemplate.StripPhp(text)) : null;
                return new ViewerAnalysis(text, format, isForced, null, null, html);
            default:
                return new ViewerAnalysis(text, format, isForced, null, null, null);
        }
    }
}
