namespace TextAnonymizer.Models;

/// <summary>
/// Un segment remplacé. <see cref="Index"/>/<see cref="Length"/> localisent le segment dans le texte transformé,
/// <see cref="SourceIndex"/>/<see cref="SourceLength"/> dans le texte d'entrée.
/// </summary>
/// <param name="MatchedText">Texte trouvé dans l'entrée (valeur d'origine du segment).</param>
public sealed record ReplacementSegment(
    int Index,
    int Length,
    int SourceIndex,
    int SourceLength,
    ConversionEntry Entry,
    string MatchedText);

/// <summary>Texte transformé et segments remplacés (triés par index croissant).</summary>
public sealed record ReplacementResult(
    string SourceText,
    string Text,
    IReadOnlyList<ReplacementSegment> Segments);
