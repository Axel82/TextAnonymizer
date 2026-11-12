namespace TextAnonymizer.Services;

public enum AppTheme
{
    Light,
    Dark
}

/// <summary>Préférences utilisateur persistées (<c>settings.json</c>).</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Light;

    public bool AutoGenerateEnabled { get; set; }
}

public interface ISettingsService
{
    /// <summary>Paramètres courants (valeurs par défaut si le fichier est absent ou illisible).</summary>
    AppSettings Current { get; }

    /// <summary>Écrit <see cref="Current"/> sur disque ; renvoie <c>false</c> si l'écriture échoue.</summary>
    bool Save();
}
