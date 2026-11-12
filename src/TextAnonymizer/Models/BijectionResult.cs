namespace TextAnonymizer.Models;

/// <summary>Résultat de la validation de la bijection d'une table.</summary>
public sealed record BijectionResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public string Message => string.Join(Environment.NewLine, Errors);
}
