using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace TextAnonymizer.Models;

/// <summary>
/// Neutralise le HTML avant son affichage dans le contrôle <c>WebBrowser</c> (moteur IE, qui n'offre pas de sandbox) :
/// scripts, gestionnaires d'événements, URL <c>javascript:</c>, cadres et objets embarqués sont supprimés.
/// Les liens et images restent affichés ; la navigation est de toute façon bloquée par la vue.
/// </summary>
public static partial class HtmlSanitizer
{
    /// <summary>Éléments retirés avec leur contenu.</summary>
    private static readonly Regex BlockedWithContent = BlockedWithContentRegex();

    /// <summary>Éléments retirés (balises seules).</summary>
    private static readonly HashSet<string> BlockedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "iframe", "frame", "frameset", "object", "embed", "applet", "base", "link", "meta", "noembed", "param"
    };

    private static readonly HashSet<string> BlockedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "srcdoc", "action", "formaction", "ping", "background", "dynsrc", "lowsrc"
    };

    private const string XUaCompatible = "<!DOCTYPE html><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">";

    /// <summary>Renvoie un document HTML complet, sans contenu actif.</summary>
    public static string Sanitize(string html)
    {
        var current = html;
        for (var pass = 0; pass < 5; pass++)
        {
            var next = SanitizeOnce(current);
            if (next == current)
            {
                break;
            }

            current = next;
        }

        return XUaCompatible + current;
    }

    private static string SanitizeOnce(string html)
    {
        var result = CommentRegex().Replace(html, string.Empty);
        result = UnterminatedCommentRegex().Replace(result, string.Empty);
        result = BlockedWithContent.Replace(result, string.Empty);
        result = UnterminatedBlockedRegex().Replace(result, string.Empty);
        result = StyleBlockRegex().Replace(result, m => m.Groups[1].Value + CleanCss(m.Groups[2].Value) + m.Groups[3].Value);
        return TagRegex().Replace(result, RewriteTag);
    }

    private static string RewriteTag(Match tag)
    {
        var name = tag.Groups[2].Value;
        if (BlockedTags.Contains(name))
        {
            return string.Empty;
        }

        if (tag.Groups[1].Length > 0)
        {
            return tag.Value;
        }

        var attributes = new StringBuilder();
        foreach (Match attribute in AttributeRegex().Matches(tag.Groups[3].Value))
        {
            var attributeName = attribute.Groups[1].Value;
            var rawValue = attribute.Groups[3].Success ? attribute.Groups[3].Value : null;
            if (!IsAllowed(attributeName, rawValue))
            {
                continue;
            }

            attributes.Append(' ').Append(attribute.Value.Trim());
        }

        var selfClosing = tag.Groups[3].Value.TrimEnd().EndsWith('/') ? " /" : string.Empty;
        return $"<{name}{attributes}{selfClosing}>";
    }

    private static bool IsAllowed(string name, string? rawValue)
    {
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || BlockedAttributes.Contains(name))
        {
            return false;
        }

        if (rawValue is null)
        {
            return true;
        }

        var value = Normalize(rawValue);
        if (value.StartsWith("javascript:", StringComparison.Ordinal)
            || value.StartsWith("vbscript:", StringComparison.Ordinal)
            || value.StartsWith("livescript:", StringComparison.Ordinal)
            || (value.StartsWith("data:", StringComparison.Ordinal) && !value.StartsWith("data:image/", StringComparison.Ordinal)))
        {
            return false;
        }

        return !name.Equals("style", StringComparison.OrdinalIgnoreCase) || CleanCss(value) == value;
    }

    /// <summary>Décode les entités, retire guillemets, espaces et caractères de contrôle, passe en minuscules.</summary>
    private static string Normalize(string rawValue)
    {
        var decoded = WebUtility.HtmlDecode(rawValue.Trim('"', '\''));
        var builder = new StringBuilder(decoded.Length);
        foreach (var c in decoded)
        {
            if (!char.IsWhiteSpace(c) && !char.IsControl(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>Supprime les constructions CSS qui exécutent du code (<c>expression()</c>, <c>behavior</c>, <c>javascript:</c>).</summary>
    private static string CleanCss(string css)
    {
        var normalized = Normalize(css);
        return normalized.Contains("expression(", StringComparison.Ordinal)
            || normalized.Contains("behavior:", StringComparison.Ordinal)
            || normalized.Contains("-moz-binding", StringComparison.Ordinal)
            || normalized.Contains("javascript:", StringComparison.Ordinal)
            || normalized.Contains("vbscript:", StringComparison.Ordinal)
            || normalized.Contains("@import", StringComparison.Ordinal)
                ? string.Empty
                : css;
    }

    [GeneratedRegex("<!--[\\s\\S]*?-->")]
    private static partial Regex CommentRegex();

    [GeneratedRegex("<!--[\\s\\S]*$")]
    private static partial Regex UnterminatedCommentRegex();

    [GeneratedRegex("<(script|iframe|frameset|object|applet|noembed)\\b[\\s\\S]*?</\\1\\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedWithContentRegex();

    [GeneratedRegex("<(script|iframe|frameset|object|applet|noembed)\\b[\\s\\S]*$", RegexOptions.IgnoreCase)]
    private static partial Regex UnterminatedBlockedRegex();

    [GeneratedRegex("(<style\\b[^>]*>)([\\s\\S]*?)(</style\\s*>)", RegexOptions.IgnoreCase)]
    private static partial Regex StyleBlockRegex();

    [GeneratedRegex("<(/?)([a-zA-Z][a-zA-Z0-9:-]*)((?:[^>\"']|\"[^\"]*\"|'[^']*')*)>")]
    private static partial Regex TagRegex();

    [GeneratedRegex("([^\\s\"'<>/=]+)(\\s*=\\s*(\"[^\"]*\"|'[^']*'|[^\\s\"'>]+))?")]
    private static partial Regex AttributeRegex();
}

/// <summary>Sépare le HTML d'un gabarit PHP (sans jamais exécuter de code).</summary>
public static partial class PhpTemplate
{
    /// <summary>Le fichier contient du HTML en dehors des blocs PHP, donc un aperçu a un sens.</summary>
    public static bool HasHtml(string php) => HtmlTagRegex().IsMatch(StripPhp(php));

    /// <summary>Texte sans les blocs <c>&lt;?php … ?&gt;</c> et <c>&lt;?= … ?&gt;</c>.</summary>
    public static string StripPhp(string php) => PhpBlockRegex().Replace(php, string.Empty);

    [GeneratedRegex("<\\?(php|=)?[\\s\\S]*?(\\?>|$)", RegexOptions.IgnoreCase)]
    private static partial Regex PhpBlockRegex();

    [GeneratedRegex("<[a-zA-Z][^<>]*>")]
    private static partial Regex HtmlTagRegex();
}
