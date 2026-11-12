using System.Text.RegularExpressions;
using TextAnonymizer.Models;
using TextAnonymizer.Services;

namespace TextAnonymizer.Tests;

public class GeneratorServiceTests
{
    private readonly GeneratorService _service = new(new Random(42));

    private static string Fold(string value) => TextNormalizer.Normalize(value, MatchingStrategy.IgnoreCaseAndDiacritics);

    [Theory]
    [InlineData(EntityType.Nom)]
    [InlineData(EntityType.Ville)]
    [InlineData(EntityType.Email)]
    [InlineData(EntityType.Tel)]
    [InlineData(EntityType.Autre)]
    public void Generate_ProducesUniqueValues_EvenBeyondListSizes(EntityType type)
    {
        var values = new List<string>();
        for (var i = 0; i < 300; i++)
        {
            values.Add(_service.Generate(type, values));
        }

        Assert.Equal(values.Count, values.Select(Fold).Distinct().Count());
    }

    [Fact]
    public void Generate_Nom_IsFirstNameThenLastName()
    {
        var value = _service.Generate(EntityType.Nom, []);

        Assert.Matches(@"^\p{L}[\p{L}'-]+ \p{L}[\p{L}'-]+$", value);
    }

    [Fact]
    public void Generate_Ville_IsKnownCity()
    {
        var value = _service.Generate(EntityType.Ville, []);

        Assert.Matches(@"^[\p{L}][\p{L} '-]+$", value);
    }

    [Fact]
    public void Generate_Ville_AvoidsExistingValues()
    {
        var first = _service.Generate(EntityType.Ville, []);

        Assert.NotEqual(first, _service.Generate(EntityType.Ville, [first]));
        Assert.NotEqual(first, _service.Generate(EntityType.Ville, [first.ToUpperInvariant()]));
    }

    [Fact]
    public void Generate_Tel_HasValidFrenchFormat()
    {
        for (var i = 0; i < 100; i++)
        {
            Assert.Matches(@"^0[1-9]( \d{2}){4}$", _service.Generate(EntityType.Tel, []));
        }
    }

    [Fact]
    public void Generate_Email_IsDerivedFromRelatedName()
    {
        var email = _service.Generate(EntityType.Email, [], "Hélène Dupont-Martin");

        Assert.Matches(@"^helene\.dupont\.martin@[a-z.-]+\.fr$", email);
    }

    [Fact]
    public void Generate_Email_WithoutName_IsPlausible()
    {
        Assert.Matches(@"^[a-z]+\.[a-z]+@[a-z.-]+\.fr$", _service.Generate(EntityType.Email, []));
    }

    [Fact]
    public void Generate_Email_StaysUniqueForSameName()
    {
        var first = _service.Generate(EntityType.Email, [], "Jean Dupont");
        var second = _service.Generate(EntityType.Email, [first], "Jean Dupont");
        var third = _service.Generate(EntityType.Email, [first, second], "Jean Dupont");

        Assert.Equal(3, new[] { first, second, third }.Distinct().Count());
    }

    [Fact]
    public void Generate_Autre_UsesSequentialPseudonyms()
    {
        Assert.Equal("ELEMENT_1", _service.Generate(EntityType.Autre, []));
        Assert.Equal("ELEMENT_3", _service.Generate(EntityType.Autre, ["ELEMENT_1", "ELEMENT_2"]));
        Assert.Equal("ELEMENT_2", _service.Generate(EntityType.Autre, ["element_1"]));
    }

    [Fact]
    public void GenerateMissing_FillsOnlyEmptyReplacements_WithUniqueValues()
    {
        var table = new ConversionTable();
        table.Entries.Add(new ConversionEntry { Original = "Jean Dupont", EntityType = EntityType.Nom });
        table.Entries.Add(new ConversionEntry { Original = "Pierre Durand", EntityType = EntityType.Nom });
        table.Entries.Add(new ConversionEntry { Original = "Paris", EntityType = EntityType.Ville });
        table.Entries.Add(new ConversionEntry { Original = "Lille", EntityType = EntityType.Ville, Replacement = "Nantes" });
        table.Entries.Add(new ConversionEntry { Original = "0612345678", EntityType = EntityType.Tel });
        table.Entries.Add(new ConversionEntry { Original = "Projet X", EntityType = EntityType.Autre });
        table.Entries.Add(new ConversionEntry { Original = "Projet Y", EntityType = EntityType.Autre });

        var generated = _service.GenerateMissing(table);

        Assert.Equal(6, generated.Count);
        Assert.DoesNotContain(generated, g => g.Entry.Original == "Lille");
        Assert.Equal(generated.Count, generated.Select(g => Fold(g.Value)).Distinct().Count());
        Assert.DoesNotContain(generated, g => Fold(g.Value) == Fold("Nantes"));
        Assert.Equal(new[] { "ELEMENT_1", "ELEMENT_2" }, generated.Where(g => g.Entry.EntityType == EntityType.Autre).Select(g => g.Value));
        Assert.Matches(@"^0[1-9]( \d{2}){4}$", generated.Single(g => g.Entry.EntityType == EntityType.Tel).Value);
    }

    [Fact]
    public void GenerateMissing_EmailsMatchNamesInOrder()
    {
        var table = new ConversionTable();
        table.Entries.Add(new ConversionEntry { Original = "Jean Dupont", EntityType = EntityType.Nom });
        table.Entries.Add(new ConversionEntry { Original = "jean@acme.com", EntityType = EntityType.Email });
        table.Entries.Add(new ConversionEntry { Original = "Pierre Durand", EntityType = EntityType.Nom, Replacement = "Éric Morel" });
        table.Entries.Add(new ConversionEntry { Original = "pierre@acme.com", EntityType = EntityType.Email });

        var generated = _service.GenerateMissing(table).ToDictionary(g => g.Entry.Original, g => g.Value);
        var firstName = generated["Jean Dupont"];

        Assert.Equal(Slug(firstName), generated["jean@acme.com"].Split('@')[0]);
        Assert.StartsWith("eric.morel@", generated["pierre@acme.com"]);
    }

    [Fact]
    public void GenerateMissing_EmailWithoutAnyName_StillGeneratesEmail()
    {
        var table = new ConversionTable();
        table.Entries.Add(new ConversionEntry { Original = "a@b.fr", EntityType = EntityType.Email });

        var generated = Assert.Single(_service.GenerateMissing(table));

        Assert.Matches(@"^[a-z]+\.[a-z]+@", generated.Value);
    }

    [Fact]
    public void GenerateMissing_ProducesValidBijection()
    {
        var table = new ConversionTable();
        foreach (var type in Enum.GetValues<EntityType>())
        {
            for (var i = 0; i < 20; i++)
            {
                table.Entries.Add(new ConversionEntry { Original = $"{type}-{i}", EntityType = type });
            }
        }

        foreach (var g in _service.GenerateMissing(table))
        {
            g.Entry.Replacement = g.Value;
        }

        Assert.True(table.ValidateBijection().IsValid, table.ValidateBijection().Message);
    }

    private static string Slug(string name) => Regex.Replace(Fold(name), "[^a-z0-9]+", ".").Trim('.');
}
