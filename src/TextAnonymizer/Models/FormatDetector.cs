using System.Text.Json;
using System.Text.RegularExpressions;

namespace TextAnonymizer.Models;

/// <summary>Détecte le format d'un texte collé : PHP, JSON, HTML, sinon Markdown (le texte brut s'y rend comme des paragraphes).</summary>
public static partial class FormatDetector
{
    public static ViewerFormat Detect(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return ViewerFormat.Markdown;
        }

        if (trimmed.Contains("<?php", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("<?=", StringComparison.Ordinal))
        {
            return ViewerFormat.Php;
        }

        if (LooksLikeJson(trimmed))
        {
            return ViewerFormat.Json;
        }

        if (LooksLikeHtml(trimmed))
        {
            return ViewerFormat.Html;
        }

        return ViewerFormat.Markdown;
    }

    /// <summary>JSON valide, ou JSON manifestement visé mais invalide (pour afficher l'erreur plutôt que du Markdown).</summary>
    private static bool LooksLikeJson(string trimmed)
    {
        if (trimmed[0] is not ('{' or '['))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(trimmed);
            return true;
        }
        catch (JsonException)
        {
            return trimmed[0] == '{'
                ? JsonKeyRegex().IsMatch(trimmed)
                : JsonArrayStartRegex().IsMatch(trimmed) && !MarkdownLinkRegex().IsMatch(trimmed);
        }
    }

    private static bool LooksLikeHtml(string trimmed) =>
        DocumentStartRegex().IsMatch(trimmed) || (trimmed[0] == '<' && HtmlTagRegex().IsMatch(trimmed));

    [GeneratedRegex("^\\{\\s*\"[^\"]*\"\\s*:")]
    private static partial Regex JsonKeyRegex();

    [GeneratedRegex("^\\[\\s*(\\{|\\[|\"|-?\\d|true\\b|false\\b|null\\b|\\]|$)")]
    private static partial Regex JsonArrayStartRegex();

    [GeneratedRegex("\\[[^\\]]*\\]\\(")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex("^(<!doctype\\s+html|<html[\\s>]|<head[\\s>]|<body[\\s>])", RegexOptions.IgnoreCase)]
    private static partial Regex DocumentStartRegex();

    [GeneratedRegex("<([a-z][a-z0-9]*)(\\s[^<>]*)?>[\\s\\S]*</\\1\\s*>|<(br|hr|img|input|meta|link)(\\s[^<>]*)?/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagRegex();
}
