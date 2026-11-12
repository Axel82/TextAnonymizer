namespace TextAnonymizer.Services;

public interface IThemeService
{
    AppTheme Current { get; }

    /// <summary>Levé après chaque changement de thème (les vues qui colorent le contenu à la main se redessinent).</summary>
    event EventHandler? ThemeChanged;

    /// <summary>Applique le thème enregistré dans les paramètres (au démarrage).</summary>
    void ApplyPersisted();

    /// <summary>Applique le thème et le mémorise dans les paramètres.</summary>
    void SetTheme(AppTheme theme);
}
