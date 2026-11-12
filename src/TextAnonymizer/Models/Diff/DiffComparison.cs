namespace TextAnonymizer.Models.Diff;

/// <summary>Paramètres d'une comparaison (instantané pris sur le fil de l'interface).</summary>
public sealed record DiffRequest(
    string TextA,
    string TextB,
    DiffOptions Options,
    DiffFormat Format,
    string? PathA = null,
    string? PathB = null,
    bool IgnorePropertyOrder = true,
    bool CompareNormalized = false);

/// <summary>Tout ce qu'une comparaison produit : résultat ligne à ligne, vues prêtes à afficher, analyse structurelle.</summary>
public sealed record DiffOutcome(
    DiffRequest Request,
    DiffFormat Format,
    DiffResult Result,
    DiffDocument Document,
    StructuralComparison? Structural,
    bool IsNormalized);

public static class DiffComparison
{
    /// <summary>Calcul complet, à exécuter hors du fil de l'interface (annulable).</summary>
    public static DiffOutcome Run(DiffRequest request, CancellationToken cancellationToken = default)
    {
        var format = request.Format == DiffFormat.Auto
            ? DiffFormatDetector.DetectCommon(request.TextA, request.TextB, request.PathA, request.PathB)
            : request.Format;
        cancellationToken.ThrowIfCancellationRequested();

        var structural = StructuralDiff.Compare(request.TextA, request.TextB, format, request.IgnorePropertyOrder, cancellationToken);
        var normalized = request.CompareNormalized && structural is { NormalizedA: not null, NormalizedB: not null };
        var (textA, textB) = normalized ? (structural!.NormalizedA!, structural.NormalizedB!) : (request.TextA, request.TextB);

        var result = LineDiffer.Compare(textA, textB, request.Options, cancellationToken);
        var document = DiffDocument.Build(result, cancellationToken);
        return new DiffOutcome(request, format, result, document, structural, normalized);
    }
}
