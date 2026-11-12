using Wpf.Ui.Appearance;

namespace TextAnonymizer.Services;

public sealed class ThemeService(ISettingsService settings) : IThemeService
{
    public AppTheme Current => settings.Current.Theme;

    public event EventHandler? ThemeChanged;

    public void ApplyPersisted() => Apply(Current);

    public void SetTheme(AppTheme theme)
    {
        settings.Current.Theme = theme;
        settings.Save();
        Apply(theme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Apply(AppTheme theme) =>
        ApplicationThemeManager.Apply(theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
}
