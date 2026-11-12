using System.Text;
using TextAnonymizer.Models;

namespace TextAnonymizer.Services;

public sealed class GeneratorService : IGeneratorService
{
    private const int RandomAttempts = 200;

    private static readonly string[] FirstNames =
    [
        "Jean", "Pierre", "Michel", "Philippe", "Alain", "Nicolas", "Laurent", "Julien", "Thomas", "Antoine",
        "Maxime", "Lucas", "Hugo", "Louis", "Mathieu", "Sébastien", "Olivier", "Étienne", "Vincent", "Guillaume",
        "Romain", "Bastien", "Clément", "Damien", "Éric", "Marie", "Nathalie", "Isabelle", "Sophie", "Catherine",
        "Sylvie", "Valérie", "Céline", "Julie", "Camille", "Léa", "Emma", "Chloé", "Manon", "Claire",
        "Hélène", "Élodie", "Aurélie", "Caroline", "Émilie", "Laurence", "Martine", "Delphine", "Pauline", "Margaux"
    ];

    private static readonly string[] LastNames =
    [
        "Martin", "Bernard", "Dubois", "Thomas", "Robert", "Richard", "Petit", "Durand", "Leroy", "Moreau",
        "Simon", "Laurent", "Lefebvre", "Michel", "Garcia", "David", "Bertrand", "Roux", "Vincent", "Fournier",
        "Morel", "Girard", "André", "Mercier", "Dupont", "Lambert", "Bonnet", "François", "Martinez", "Legrand",
        "Garnier", "Faure", "Rousseau", "Blanc", "Guerin", "Muller", "Henry", "Roussel", "Nicolas", "Perrin",
        "Morin", "Mathieu", "Clément", "Gauthier", "Dumont", "Lopez", "Fontaine", "Chevalier", "Robin", "Masson"
    ];

    private static readonly string[] Cities =
    [
        "Paris", "Marseille", "Lyon", "Toulouse", "Nice", "Nantes", "Montpellier", "Strasbourg", "Bordeaux", "Lille",
        "Rennes", "Reims", "Toulon", "Grenoble", "Dijon", "Angers", "Nîmes", "Villeurbanne", "Clermont-Ferrand", "Le Mans",
        "Aix-en-Provence", "Brest", "Tours", "Amiens", "Limoges", "Annecy", "Perpignan", "Metz", "Besançon", "Orléans",
        "Rouen", "Mulhouse", "Caen", "Nancy", "Avignon", "Poitiers", "La Rochelle", "Pau", "Calais", "Cannes",
        "Antibes", "Ajaccio", "Bayonne", "Colmar", "Troyes", "Lorient", "Quimper", "Valence", "Chambéry", "Albi"
    ];

    private static readonly string[] EmailDomains = ["exemple.fr", "courrier-test.fr", "messagerie-exemple.fr"];

    private static readonly string[] PhonePrefixes = ["01", "02", "03", "04", "05", "06", "07", "09"];

    private readonly Random _random;

    public GeneratorService()
        : this(new Random())
    {
    }

    public GeneratorService(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public string Generate(EntityType type, IReadOnlyCollection<string> existing, string? relatedName = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var used = new HashSet<string>(
            existing.Select(Fold),
            StringComparer.Ordinal);
        return GenerateCore(type, used, relatedName);
    }

    public IReadOnlyList<GeneratedValue> GenerateMissing(ConversionTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in table.Entries)
        {
            used.Add(Fold(entry.Original));
            if (!string.IsNullOrWhiteSpace(entry.Replacement))
            {
                used.Add(Fold(entry.Replacement));
            }
        }

        var generated = new Dictionary<ConversionEntry, string>();
        foreach (var entry in table.Entries.Where(e => NeedsValue(e) && e.EntityType != EntityType.Email))
        {
            generated[entry] = GenerateCore(entry.EntityType, used, null);
        }

        var names = table.Entries
            .Where(e => e.EntityType == EntityType.Nom)
            .Select(e => !string.IsNullOrWhiteSpace(e.Replacement) ? e.Replacement : generated.GetValueOrDefault(e))
            .OfType<string>()
            .ToList();

        var nextName = 0;
        foreach (var entry in table.Entries.Where(e => e.EntityType == EntityType.Email))
        {
            var name = nextName < names.Count ? names[nextName++] : null;
            if (NeedsValue(entry))
            {
                generated[entry] = GenerateCore(EntityType.Email, used, name);
            }
        }

        return table.Entries
            .Where(generated.ContainsKey)
            .Select(e => new GeneratedValue(e, generated[e]))
            .ToList();
    }

    private static bool NeedsValue(ConversionEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Original) && string.IsNullOrWhiteSpace(entry.Replacement);

    /// <summary>Génère une valeur absente de <paramref name="used"/> et l'y ajoute.</summary>
    private string GenerateCore(EntityType type, HashSet<string> used, string? relatedName)
    {
        var value = type switch
        {
            EntityType.Nom => GenerateName(used),
            EntityType.Ville => GenerateCity(used),
            EntityType.Email => GenerateEmail(used, relatedName),
            EntityType.Tel => GeneratePhone(used),
            _ => GenerateGeneric(used)
        };

        used.Add(Fold(value));
        return value;
    }

    private string GenerateName(HashSet<string> used)
    {
        for (var i = 0; i < RandomAttempts; i++)
        {
            var candidate = RandomName();
            if (!used.Contains(Fold(candidate)))
            {
                return candidate;
            }
        }

        foreach (var first in FirstNames)
        {
            foreach (var last in LastNames)
            {
                var candidate = $"{first} {last}";
                if (!used.Contains(Fold(candidate)))
                {
                    return candidate;
                }
            }
        }

        return Numbered(RandomName(), used);
    }

    private string GenerateCity(HashSet<string> used)
    {
        var start = _random.Next(Cities.Length);
        for (var i = 0; i < Cities.Length; i++)
        {
            var candidate = Cities[(start + i) % Cities.Length];
            if (!used.Contains(Fold(candidate)))
            {
                return candidate;
            }
        }

        return Numbered(Cities[start], used);
    }

    private string GenerateEmail(HashSet<string> used, string? relatedName)
    {
        var name = string.IsNullOrWhiteSpace(relatedName) ? RandomName() : relatedName;
        var local = Slug(name);
        if (local.Length == 0)
        {
            local = Slug(RandomName());
        }

        var domain = EmailDomains[_random.Next(EmailDomains.Length)];
        var candidate = $"{local}@{domain}";
        for (var n = 2; used.Contains(Fold(candidate)); n++)
        {
            candidate = $"{local}{n}@{domain}";
        }

        return candidate;
    }

    private string GeneratePhone(HashSet<string> used)
    {
        while (true)
        {
            var builder = new StringBuilder(PhonePrefixes[_random.Next(PhonePrefixes.Length)]);
            for (var i = 0; i < 4; i++)
            {
                builder.Append(' ').Append(_random.Next(100).ToString("D2"));
            }

            var candidate = builder.ToString();
            if (!used.Contains(Fold(candidate)))
            {
                return candidate;
            }
        }
    }

    private static string GenerateGeneric(HashSet<string> used)
    {
        var n = 1;
        while (used.Contains(Fold($"ELEMENT_{n}")))
        {
            n++;
        }

        return $"ELEMENT_{n}";
    }

    private string RandomName() =>
        $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}";

    private static string Numbered(string baseValue, HashSet<string> used)
    {
        var n = 2;
        while (used.Contains(Fold($"{baseValue} {n}")))
        {
            n++;
        }

        return $"{baseValue} {n}";
    }

    private static string Fold(string value) =>
        TextNormalizer.Normalize(value, MatchingStrategy.IgnoreCaseAndDiacritics);

    /// <summary>« Hélène Dupont » → « helene.dupont ».</summary>
    private static string Slug(string name)
    {
        var folded = Fold(name);
        var builder = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '.')
            {
                builder.Append('.');
            }
        }

        return builder.ToString().Trim('.');
    }
}
