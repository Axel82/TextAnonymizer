namespace TextAnonymizer;

/// <summary>
/// Version de l'application, à incrémenter à chaque évolution (voir <c>CHANGELOG.md</c> à la racine).
/// Schéma : MAJEUR.MINEUR.CORRECTIF — MINEUR pour une nouvelle fonctionnalité ou un nouveau prompt de conception,
/// CORRECTIF pour une correction. Garder cette valeur identique à la dernière entrée du changelog.
/// </summary>
public static class AppVersion
{
    public const int Major = 0;
    public const int Minor = 4;
    public const int Patch = 0;

    /// <summary>Numéro de version, par exemple « 0.2.1 ».</summary>
    public const string Current = "0.4.0";
}
