namespace TextAnonymizer.Services;

/// <summary>Accès au presse-papiers en texte brut uniquement.</summary>
public interface IClipboardService
{
    /// <summary>Texte brut du presse-papiers, ou <c>null</c> s'il ne contient pas de texte.</summary>
    string? GetText();

    void SetText(string text);
}
