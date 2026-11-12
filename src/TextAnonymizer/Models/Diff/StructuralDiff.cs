using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TextAnonymizer.Models.Diff;

/// <summary>Mode de comparaison ; <see cref="Auto"/> n'est qu'un choix de l'utilisateur, jamais un format détecté.</summary>
public enum DiffFormat
{
    Auto,
    Text,
    Json,
    Xml,
    Markdown
}

public enum StructuralChangeKind
{
    Added,
    Removed,
    Modified,
    TypeChanged,
    Reordered
}

/// <summary>Un changement de fond : propriété, élément, attribut ou section, repéré par son chemin.</summary>
public sealed record StructuralChange(StructuralChangeKind Kind, string Path, string? OldValue, string? NewValue)
{
    public string KindLabel => Kind switch
    {
        StructuralChangeKind.Added => "Ajouté",
        StructuralChangeKind.Removed => "Supprimé",
        StructuralChangeKind.Modified => "Modifié",
        StructuralChangeKind.TypeChanged => "Type changé",
        _ => "Ordre changé"
    };
}

/// <summary>Résultat de la comparaison structurelle (JSON, XML ou Markdown), en plus de la comparaison ligne à ligne.</summary>
public sealed record StructuralComparison(
    DiffFormat Format,
    IReadOnlyList<StructuralChange> Changes,
    int TotalChanges,
    string? Error,
    string? NormalizedA,
    string? NormalizedB)
{
    /// <summary>Au-delà, la liste est tronquée (le total reste exact).</summary>
    public const int MaxListedChanges = 5000;

    public bool IsValid => Error is null;

    public string Summary => Error is not null
        ? Error
        : TotalChanges == 0
            ? Format == DiffFormat.Markdown
                ? "Aucune différence de contenu entre les sections."
                : "Aucun changement structurel : les différences éventuelles ne portent que sur la présentation (indentation, espaces, ordre des propriétés)."
            : TotalChanges > Changes.Count
                ? $"{TotalChanges} changements structurels ({Changes.Count} affichés)."
                : $"{TotalChanges} changement(s) structurel(s).";
}

/// <summary>Collecte les changements en bornant la liste affichée.</summary>
internal sealed class ChangeCollector
{
    public List<StructuralChange> Changes { get; } = [];

    public int Total { get; private set; }

    public void Add(StructuralChangeKind kind, string path, string? oldValue, string? newValue)
    {
        Total++;
        if (Changes.Count < StructuralComparison.MaxListedChanges)
        {
            Changes.Add(new StructuralChange(kind, path, Shorten(oldValue), Shorten(newValue)));
        }
    }

    private static string? Shorten(string? value)
    {
        if (value is null)
        {
            return null;
        }

        value = value.ReplaceLineEndings(" ");
        return value.Length <= 120 ? value : value[..117] + "…";
    }
}

public static partial class DiffFormatDetector
{
    /// <summary>Format commun aux deux contenus ; s'ils divergent, texte brut. L'extension des fichiers prime sur le contenu.</summary>
    public static DiffFormat DetectCommon(string? textA, string? textB, string? pathA = null, string? pathB = null)
    {
        var a = Detect(textA, pathA);
        var b = Detect(textB, pathB);
        if (string.IsNullOrWhiteSpace(textA))
        {
            return b;
        }

        if (string.IsNullOrWhiteSpace(textB))
        {
            return a;
        }

        return a == b ? a : DiffFormat.Text;
    }

    public static DiffFormat Detect(string? text, string? path = null)
    {
        var byExtension = System.IO.Path.GetExtension(path ?? string.Empty).ToLowerInvariant() switch
        {
            ".json" => DiffFormat.Json,
            ".xml" or ".xaml" or ".csproj" or ".config" or ".svg" or ".xsd" or ".resx" or ".props" or ".targets" => DiffFormat.Xml,
            ".md" or ".markdown" => DiffFormat.Markdown,
            _ => DiffFormat.Auto
        };
        if (byExtension != DiffFormat.Auto)
        {
            return byExtension;
        }

        var trimmed = text?.TrimStart() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return DiffFormat.Text;
        }

        if (trimmed[0] is '{' or '[' && JsonStructuralDiff.IsValid(text!))
        {
            return DiffFormat.Json;
        }

        if (trimmed[0] == '<' && XmlStructuralDiff.TryParse(text!, out _, out _))
        {
            return DiffFormat.Xml;
        }

        return MarkdownSignRegex().IsMatch(text!) ? DiffFormat.Markdown : DiffFormat.Text;
    }

    [GeneratedRegex(@"^(#{1,6}\s+\S|```|\s*[-*+]\s+\[[ xX]\]\s|\|.*\|\s*$)", RegexOptions.Multiline)]
    private static partial Regex MarkdownSignRegex();
}

public static class StructuralDiff
{
    /// <summary>Comparaison de fond selon le format ; <c>null</c> pour le texte brut.</summary>
    public static StructuralComparison? Compare(string textA, string textB, DiffFormat format, bool ignorePropertyOrder, CancellationToken cancellationToken = default) =>
        format switch
        {
            DiffFormat.Json => JsonStructuralDiff.Compare(textA, textB, ignorePropertyOrder, cancellationToken),
            DiffFormat.Xml => XmlStructuralDiff.Compare(textA, textB, cancellationToken),
            DiffFormat.Markdown => MarkdownStructuralDiff.Compare(textA, textB, cancellationToken),
            _ => null
        };

    /// <summary>Appariement ordonné de deux listes d'éléments : identiques (ignorés), remplacés (comparés en profondeur), ajoutés ou supprimés.</summary>
    internal static void Align<T>(
        IReadOnlyList<T> a,
        IReadOnlyList<T> b,
        Func<T, string> canonical,
        Action<T, T> replaced,
        Action<T> removed,
        Action<T> added)
    {
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        int Id(T item)
        {
            var key = canonical(item);
            if (!ids.TryGetValue(key, out var id))
            {
                id = ids.Count;
                ids[key] = id;
            }

            return id;
        }

        var idsA = a.Select(Id).ToArray();
        var idsB = b.Select(Id).ToArray();
        var (deleted, inserted) = MyersDiff.Compute(idsA, idsB);
        int i = 0, j = 0;
        while (i < a.Count || j < b.Count)
        {
            if (i < a.Count && j < b.Count && !deleted[i] && !inserted[j])
            {
                i++;
                j++;
                continue;
            }

            var gapA = new List<T>();
            var gapB = new List<T>();
            while (i < a.Count && deleted[i])
            {
                gapA.Add(a[i++]);
            }

            while (j < b.Count && inserted[j])
            {
                gapB.Add(b[j++]);
            }

            var paired = Math.Min(gapA.Count, gapB.Count);
            for (var k = 0; k < paired; k++)
            {
                replaced(gapA[k], gapB[k]);
            }

            foreach (var item in gapA.Skip(paired))
            {
                removed(item);
            }

            foreach (var item in gapB.Skip(paired))
            {
                added(item);
            }
        }
    }
}

/// <summary>
/// JSON : propriétés comparées par nom (l'ordre n'est signalé que si on le demande), valeurs par type et par valeur
/// (<c>1.0</c> = <c>1</c>, <c>"é"</c> = <c>"é"</c>), tableaux par position après alignement des éléments identiques.
/// </summary>
public static class JsonStructuralDiff
{
    public static bool IsValid(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static StructuralComparison Compare(string textA, string textB, bool ignorePropertyOrder, CancellationToken cancellationToken = default)
    {
        using var a = JsonInspector.Inspect(textA);
        using var b = JsonInspector.Inspect(textB);
        if (!a.IsValid || !b.IsValid)
        {
            var errors = new List<string>();
            if (!a.IsValid)
            {
                errors.Add("A : " + a.Error);
            }

            if (!b.IsValid)
            {
                errors.Add("B : " + b.Error);
            }

            return new StructuralComparison(DiffFormat.Json, [], 0, string.Join("\n", errors), null, null);
        }

        var collector = new ChangeCollector();
        Walk(a.Root, b.Root, "$", ignorePropertyOrder, collector, cancellationToken);
        return new StructuralComparison(
            DiffFormat.Json,
            collector.Changes,
            collector.Total,
            null,
            Normalize(a.Root, ignorePropertyOrder),
            Normalize(b.Root, ignorePropertyOrder));
    }

    /// <summary>Forme normalisée : indentation homogène, échappements uniformes, propriétés triées si l'ordre est ignoré.</summary>
    public static string Normalize(JsonElement element, bool sortProperties)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }))
        {
            Write(writer, element, sortProperties);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element, bool sort)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = element.EnumerateObject().ToList();
                if (sort)
                {
                    properties = [.. properties.OrderBy(p => p.Name, StringComparer.Ordinal)];
                }

                foreach (var property in properties)
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value, sort);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(writer, item, sort);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number when element.TryGetDecimal(out var number):
                // 42, 42.0 et 4.2E1 ont la même valeur : une seule écriture (zéros non significatifs retirés).
                writer.WriteNumberValue(number / 1.000000000000000000000000000000000m);
                break;
            default:
                writer.WriteRawValue(element.GetRawText());
                break;
        }
    }

    private static void Walk(JsonElement a, JsonElement b, string path, bool ignoreOrder, ChangeCollector changes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var kindA = Kind(a);
        var kindB = Kind(b);
        if (kindA != kindB)
        {
            changes.Add(StructuralChangeKind.TypeChanged, path, $"{TypeName(a)} {Raw(a)}", $"{TypeName(b)} {Raw(b)}");
            return;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                WalkObject(a, b, path, ignoreOrder, changes, cancellationToken);
                break;
            case JsonValueKind.Array:
                var itemsA = a.EnumerateArray().Select((e, i) => (Element: e, Index: i)).ToList();
                var itemsB = b.EnumerateArray().Select((e, i) => (Element: e, Index: i)).ToList();
                StructuralDiff.Align(
                    itemsA,
                    itemsB,
                    item => Normalize(item.Element, ignoreOrder),
                    (x, y) => Walk(x.Element, y.Element, $"{path}[{y.Index}]", ignoreOrder, changes, cancellationToken),
                    x => changes.Add(StructuralChangeKind.Removed, $"{path}[{x.Index}]", Raw(x.Element), null),
                    y => changes.Add(StructuralChangeKind.Added, $"{path}[{y.Index}]", null, Raw(y.Element)));
                break;
            default:
                if (!SameValue(a, b))
                {
                    changes.Add(StructuralChangeKind.Modified, path, Raw(a), Raw(b));
                }

                break;
        }
    }

    private static void WalkObject(JsonElement a, JsonElement b, string path, bool ignoreOrder, ChangeCollector changes, CancellationToken cancellationToken)
    {
        // En cas de nom en double, la dernière valeur l'emporte (comme la plupart des analyseurs).
        var propertiesA = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var orderA = new List<string>();
        foreach (var property in a.EnumerateObject())
        {
            if (propertiesA.TryAdd(property.Name, property.Value))
            {
                orderA.Add(property.Name);
            }
            else
            {
                propertiesA[property.Name] = property.Value;
            }
        }

        var propertiesB = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var orderB = new List<string>();
        foreach (var property in b.EnumerateObject())
        {
            if (propertiesB.TryAdd(property.Name, property.Value))
            {
                orderB.Add(property.Name);
            }
            else
            {
                propertiesB[property.Name] = property.Value;
            }
        }

        foreach (var name in orderA)
        {
            var childPath = Child(path, name);
            if (propertiesB.TryGetValue(name, out var valueB))
            {
                Walk(propertiesA[name], valueB, childPath, ignoreOrder, changes, cancellationToken);
            }
            else
            {
                changes.Add(StructuralChangeKind.Removed, childPath, Raw(propertiesA[name]), null);
            }
        }

        foreach (var name in orderB.Where(n => !propertiesA.ContainsKey(n)))
        {
            changes.Add(StructuralChangeKind.Added, Child(path, name), null, Raw(propertiesB[name]));
        }

        if (!ignoreOrder)
        {
            var commonA = orderA.Where(propertiesB.ContainsKey).ToList();
            var commonB = orderB.Where(propertiesA.ContainsKey).ToList();
            if (!commonA.SequenceEqual(commonB))
            {
                changes.Add(StructuralChangeKind.Reordered, path, string.Join(", ", commonA), string.Join(", ", commonB));
            }
        }
    }

    private static string Child(string path, string name) =>
        Regex.IsMatch(name, "^[A-Za-z_$][A-Za-z0-9_$]*$") ? $"{path}.{name}" : $"{path}['{name.Replace("'", "\\'")}']";

    /// <summary>Vrai et faux sont du même type (booléen).</summary>
    private static JsonValueKind Kind(JsonElement element) =>
        element.ValueKind == JsonValueKind.False ? JsonValueKind.True : element.ValueKind;

    private static bool SameValue(JsonElement a, JsonElement b) => a.ValueKind switch
    {
        JsonValueKind.String => a.GetString() == b.GetString(),
        JsonValueKind.Number => a.TryGetDecimal(out var x) && b.TryGetDecimal(out var y)
            ? x == y
            : a.TryGetDouble(out var dx) && b.TryGetDouble(out var dy) ? dx.Equals(dy) : a.GetRawText() == b.GetRawText(),
        JsonValueKind.True or JsonValueKind.False => a.ValueKind == b.ValueKind,
        _ => true
    };

    private static string TypeName(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "objet",
        JsonValueKind.Array => "tableau",
        JsonValueKind.String => "chaîne",
        JsonValueKind.Number => "nombre",
        JsonValueKind.True or JsonValueKind.False => "booléen",
        _ => "null"
    };

    private static string Raw(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => $"{{…}} ({element.EnumerateObject().Count()} propriété(s))",
        JsonValueKind.Array => $"[…] ({element.GetArrayLength()} élément(s))",
        _ => element.GetRawText()
    };
}

/// <summary>
/// XML : éléments comparés dans l'ordre (alignement des éléments identiques), attributs par nom sans tenir compte
/// de leur ordre, textes sans les blancs de mise en forme. Commentaires et instructions de traitement ignorés.
/// DTD ignorée et aucune ressource externe résolue.
/// </summary>
public static class XmlStructuralDiff
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true
    };

    public static bool TryParse(string text, out XDocument? document, out string? error)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), ReaderSettings);
            document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            error = null;
            return document.Root is not null;
        }
        catch (XmlException ex)
        {
            document = null;
            error = $"XML invalide (ligne {ex.LineNumber}, colonne {ex.LinePosition}) : {ex.Message}";
            return false;
        }
    }

    public static StructuralComparison Compare(string textA, string textB, CancellationToken cancellationToken = default)
    {
        var okA = TryParse(textA, out var a, out var errorA);
        var okB = TryParse(textB, out var b, out var errorB);
        if (!okA || !okB)
        {
            var errors = new List<string>();
            if (!okA)
            {
                errors.Add("A : " + (errorA ?? "document XML vide."));
            }

            if (!okB)
            {
                errors.Add("B : " + (errorB ?? "document XML vide."));
            }

            return new StructuralComparison(DiffFormat.Xml, [], 0, string.Join("\n", errors), null, null);
        }

        var collector = new ChangeCollector();
        WalkElement(a!.Root!, b!.Root!, "/" + a.Root!.Name.LocalName, collector, cancellationToken);
        return new StructuralComparison(DiffFormat.Xml, collector.Changes, collector.Total, null, Normalize(a.Root), Normalize(b.Root!));
    }

    /// <summary>Forme normalisée : attributs triés, indentation homogène, sans déclaration ni commentaires.</summary>
    public static string Normalize(XElement element) =>
        Canonical(element).ToString(SaveOptions.None).ReplaceLineEndings("\n") + "\n";

    private static XElement Canonical(XElement element) => new(
        element.Name,
        element.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal)
            .Select(a => new XAttribute(a.Name, a.Value)),
        element.Nodes().Select(n => n switch
        {
            XElement child => (object)Canonical(child),
            XText text => new XText(text.Value.Trim()),
            _ => null!
        }).Where(n => n is not null));

    private static void WalkElement(XElement a, XElement b, string path, ChangeCollector changes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (a.Name != b.Name)
        {
            changes.Add(StructuralChangeKind.Modified, path, $"<{a.Name.LocalName}>", $"<{b.Name.LocalName}>");
        }

        var attributesA = a.Attributes().Where(x => !x.IsNamespaceDeclaration).ToDictionary(x => x.Name);
        var attributesB = b.Attributes().Where(x => !x.IsNamespaceDeclaration).ToDictionary(x => x.Name);
        foreach (var (name, attribute) in attributesA)
        {
            var attributePath = $"{path}/@{name.LocalName}";
            if (!attributesB.TryGetValue(name, out var other))
            {
                changes.Add(StructuralChangeKind.Removed, attributePath, attribute.Value, null);
            }
            else if (attribute.Value != other.Value)
            {
                changes.Add(StructuralChangeKind.Modified, attributePath, attribute.Value, other.Value);
            }
        }

        foreach (var (name, attribute) in attributesB.Where(x => !attributesA.ContainsKey(x.Key)))
        {
            changes.Add(StructuralChangeKind.Added, $"{path}/@{name.LocalName}", null, attribute.Value);
        }

        var childrenA = Children(a);
        var childrenB = Children(b);
        StructuralDiff.Align(
            childrenA,
            childrenB,
            child => child.Node is XElement e ? Canonical(e).ToString(SaveOptions.DisableFormatting) : "#text:" + ((XText)child.Node).Value.Trim(),
            (x, y) =>
            {
                if (x.Node is XElement ex && y.Node is XElement ey && ex.Name == ey.Name)
                {
                    WalkElement(ex, ey, $"{path}/{y.Step}", changes, cancellationToken);
                }
                else if (x.Node is XText tx && y.Node is XText ty)
                {
                    changes.Add(StructuralChangeKind.Modified, $"{path}/text()", tx.Value.Trim(), ty.Value.Trim());
                }
                else
                {
                    changes.Add(StructuralChangeKind.Removed, $"{path}/{x.Step}", Describe(x.Node), null);
                    changes.Add(StructuralChangeKind.Added, $"{path}/{y.Step}", null, Describe(y.Node));
                }
            },
            x => changes.Add(StructuralChangeKind.Removed, $"{path}/{x.Step}", Describe(x.Node), null),
            y => changes.Add(StructuralChangeKind.Added, $"{path}/{y.Step}", null, Describe(y.Node)));
    }

    private readonly record struct Child(XNode Node, string Step);

    /// <summary>Enfants significatifs, avec leur étape de chemin (<c>item[2]</c> = deuxième élément <c>item</c>).</summary>
    private static List<Child> Children(XElement element)
    {
        var counts = new Dictionary<XName, int>();
        var children = new List<Child>();
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement child:
                    counts[child.Name] = counts.GetValueOrDefault(child.Name) + 1;
                    children.Add(new Child(child, $"{child.Name.LocalName}[{counts[child.Name]}]"));
                    break;
                case XText text when !string.IsNullOrWhiteSpace(text.Value):
                    children.Add(new Child(text, "text()"));
                    break;
            }
        }

        return children;
    }

    private static string Describe(XNode node) => node switch
    {
        XElement element => Canonical(element).ToString(SaveOptions.DisableFormatting),
        XText text => text.Value.Trim(),
        _ => node.ToString()
    };
}

/// <summary>Markdown : découpage en sections par titres (hors blocs de code) et comparaison du contenu de chaque section.</summary>
public static partial class MarkdownStructuralDiff
{
    private sealed record Section(string Path, string Title, List<string> Lines);

    public static StructuralComparison Compare(string textA, string textB, CancellationToken cancellationToken = default)
    {
        var sectionsA = Sections(textA);
        var sectionsB = Sections(textB);
        var collector = new ChangeCollector();
        var byPathB = sectionsB.GroupBy(s => s.Path).ToDictionary(g => g.Key, g => new Queue<Section>(g));
        var matchedB = new HashSet<Section>();

        foreach (var section in sectionsA)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (byPathB.TryGetValue(section.Path, out var queue) && queue.Count > 0)
            {
                var other = queue.Dequeue();
                matchedB.Add(other);
                var (added, removed) = CountChanges(section.Lines, other.Lines);
                if (added + removed > 0)
                {
                    collector.Add(StructuralChangeKind.Modified, section.Path, $"{removed} ligne(s) retirée(s)", $"{added} ligne(s) ajoutée(s)");
                }
            }
            else
            {
                collector.Add(StructuralChangeKind.Removed, section.Path, $"{Content(section)} ligne(s)", null);
            }
        }

        foreach (var section in sectionsB.Where(s => !matchedB.Contains(s)))
        {
            collector.Add(StructuralChangeKind.Added, section.Path, null, $"{Content(section)} ligne(s)");
        }

        var commonA = sectionsA.Select(s => s.Path).Where(p => sectionsB.Any(s => s.Path == p)).Distinct().ToList();
        var commonB = sectionsB.Select(s => s.Path).Where(p => sectionsA.Any(s => s.Path == p)).Distinct().ToList();
        if (!commonA.SequenceEqual(commonB))
        {
            collector.Add(StructuralChangeKind.Reordered, "(plan du document)", string.Join(" · ", commonA), string.Join(" · ", commonB));
        }

        return new StructuralComparison(DiffFormat.Markdown, collector.Changes, collector.Total, null, null, null);
    }

    private static int Content(Section section) => section.Lines.Count(l => !string.IsNullOrWhiteSpace(l));

    /// <summary>Lignes de contenu ajoutées et retirées, sans tenir compte des lignes vides ni des espaces de fin.</summary>
    private static (int Added, int Removed) CountChanges(List<string> a, List<string> b)
    {
        var options = new DiffOptions { IgnoreBlankLines = true, TrimWhitespace = true };
        var stats = LineDiffer.Compare(string.Join("\n", a), string.Join("\n", b), options).Statistics;
        return (stats.Added + stats.Modified, stats.Deleted + stats.Modified);
    }

    private static List<Section> Sections(string text)
    {
        var sections = new List<Section>();
        var titles = new string?[7];
        var current = new Section("(introduction)", string.Empty, []);
        var inFence = false;
        foreach (var line in LineSplitter.Split(text))
        {
            var content = line.Content;
            if (FenceRegex().IsMatch(content))
            {
                inFence = !inFence;
            }

            var heading = inFence ? null : HeadingRegex().Match(content);
            if (heading is { Success: true })
            {
                if (current.Lines.Count > 0 || current.Title.Length > 0)
                {
                    sections.Add(current);
                }

                var level = heading.Groups[1].Length;
                titles[level] = heading.Groups[2].Value.Trim().TrimEnd('#').Trim();
                for (var k = level + 1; k < titles.Length; k++)
                {
                    titles[k] = null;
                }

                var path = string.Join(" › ", titles.Skip(1).Take(level).Where(t => t is not null));
                current = new Section(path, titles[level]!, []);
                continue;
            }

            current.Lines.Add(content);
        }

        if (current.Lines.Count > 0 || current.Title.Length > 0)
        {
            sections.Add(current);
        }

        return sections;
    }

    [GeneratedRegex(@"^ {0,3}(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^ {0,3}(```|~~~)")]
    private static partial Regex FenceRegex();
}
