using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TextAnonymizer.Services;

namespace TextAnonymizer.ViewModels;

/// <summary>Page « Paramètres » : thème, stratégie de matching, génération automatique et emplacement de la table.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;

    public SettingsViewModel(IThemeService theme, ISettingsService settings, TableViewModel table, IDialogService dialogs)
    {
        _theme = theme;
        _settings = settings;
        _dialogs = dialogs;
        Table = table;

        _isDarkTheme = theme.Current == AppTheme.Dark;
        theme.ThemeChanged += (_, _) => IsDarkTheme = theme.Current == AppTheme.Dark;
        Table.AutoGenerateEnabled = settings.Current.AutoGenerateEnabled;
        Table.PropertyChanged += OnTablePropertyChanged;
    }

    /// <summary>Table dont la stratégie, la génération automatique et l'emplacement sont présentés.</summary>
    public TableViewModel Table { get; }

    [ObservableProperty]
    private bool _isDarkTheme;

    partial void OnIsDarkThemeChanged(bool value)
    {
        var target = value ? AppTheme.Dark : AppTheme.Light;
        if (_theme.Current == target)
        {
            return;
        }

        _theme.SetTheme(target);
        _dialogs.Notify(value ? "Thème sombre activé." : "Thème clair activé.");
    }

    private void OnTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TableViewModel.AutoGenerateEnabled))
        {
            return;
        }

        _settings.Current.AutoGenerateEnabled = Table.AutoGenerateEnabled;
        _settings.Save();
        _dialogs.Notify(
            Table.AutoGenerateEnabled ? "Génération automatique activée." : "Génération automatique désactivée.",
            NotificationKind.Info);
    }
}
