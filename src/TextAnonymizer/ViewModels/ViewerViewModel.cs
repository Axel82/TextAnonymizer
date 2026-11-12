using System.IO;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextAnonymizer.Models;
using TextAnonymizer.Services;

namespace TextAnonymizer.ViewModels;

/// <summary>Choix affiché dans le sélecteur de format.</summary>
public sealed record FormatOption(ViewerFormat Format, string Label);

/// <summary>Page « Moteur de rendu » : texte collé à gauche, rendu du format détecté (ou forcé) à droite.</summary>
public sealed partial class ViewerViewModel : ObservableObject, IDisposable
{
    /// <summary>Délai entre la dernière modification du texte et la mise à jour du rendu.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;
    private readonly DispatcherTimer _debounce;

    public ViewerViewModel(IClipboardService clipboard, IDialogService dialogs, IThemeService theme)
    {
        _clipboard = clipboard;
        _dialogs = dialogs;
        _theme = theme;
        _selectedFormat = Formats[0];

        _debounce = new DispatcherTimer { Interval = DebounceDelay };
        _debounce.Tick += (_, _) => Refresh();
        _theme.ThemeChanged += (_, _) => OnPropertyChanged(nameof(IsDarkTheme));
    }

    public IReadOnlyList<FormatOption> Formats { get; } =
    [
        new(ViewerFormat.Auto, "Auto"),
        new(ViewerFormat.Json, "JSON"),
        new(ViewerFormat.Markdown, "Markdown"),
        new(ViewerFormat.Html, "HTML"),
        new(ViewerFormat.Php, "PHP")
    ];

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private FormatOption _selectedFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(Format), nameof(IsEmpty), nameof(IsJson), nameof(IsMarkdown), nameof(IsHtml), nameof(IsPhp),
        nameof(HasHtmlPreview), nameof(FormatLabel))]
    private ViewerAnalysis _analysis = ViewerAnalysis.Empty;

    public ViewerFormat Format => Analysis.Format;

    public bool IsEmpty => string.IsNullOrEmpty(Text);

    public bool IsJson => !IsEmpty && Format == ViewerFormat.Json;

    public bool IsMarkdown => !IsEmpty && Format == ViewerFormat.Markdown;

    public bool IsHtml => !IsEmpty && Format == ViewerFormat.Html;

    public bool IsPhp => !IsEmpty && Format == ViewerFormat.Php;

    /// <summary>Un onglet d'aperçu HTML est proposé (HTML, ou PHP qui contient aussi du HTML).</summary>
    public bool HasHtmlPreview => !IsEmpty && Analysis.HtmlPreview is not null;

    public string FormatLabel
    {
        get
        {
            var name = Formats.First(f => f.Format == Format).Label;
            return Analysis.IsForced ? $"Format : {name} (forcé)" : $"Format détecté : {name}";
        }
    }

    public bool IsDarkTheme => _theme.Current == AppTheme.Dark;

    partial void OnTextChanged(string value)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    partial void OnSelectedFormatChanged(FormatOption value) => Refresh();

    /// <summary>Recalcule le rendu immédiatement (sans attendre le délai de frappe).</summary>
    public void Refresh()
    {
        _debounce.Stop();
        var previous = Analysis;
        Analysis = ViewerAnalyzer.Analyze(Text, SelectedFormat.Format);
        previous.Dispose();
    }

    [RelayCommand]
    private void Paste()
    {
        var clipboardText = _clipboard.GetText();
        if (string.IsNullOrEmpty(clipboardText))
        {
            _dialogs.Notify("Le presse-papiers ne contient pas de texte.", NotificationKind.Warning);
            return;
        }

        Text = clipboardText;
        Refresh();
        _dialogs.Notify("Texte collé.");
    }

    [RelayCommand]
    private void Copy()
    {
        if (string.IsNullOrEmpty(Text))
        {
            _dialogs.Notify("Rien à copier.", NotificationKind.Warning);
            return;
        }

        _clipboard.SetText(Text);
        _dialogs.Notify("Texte copié.");
    }

    [RelayCommand]
    private void Clear()
    {
        Text = string.Empty;
        Refresh();
        _dialogs.Notify("Zone vidée.", NotificationKind.Info);
    }

    [RelayCommand]
    private void Download()
    {
        if (string.IsNullOrEmpty(Text))
        {
            _dialogs.Notify("Rien à télécharger.", NotificationKind.Warning);
            return;
        }

        Refresh();
        var (extension, filter) = Format switch
        {
            ViewerFormat.Json => ("json", "JSON (*.json)|*.json"),
            ViewerFormat.Html => ("html", "HTML (*.html)|*.html"),
            ViewerFormat.Php => ("php", "PHP (*.php)|*.php"),
            _ => ("md", "Markdown (*.md)|*.md")
        };

        var path = _dialogs.PickSaveFile("Enregistrer le contenu", filter + "|Tous les fichiers (*.*)|*.*", null, $"contenu.{extension}");
        if (path is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(path, Text, new UTF8Encoding(false));
            _dialogs.Notify($"Fichier enregistré : {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Impossible d'enregistrer le fichier : {ex.Message}");
        }
    }

    public void Dispose()
    {
        _debounce.Stop();
        Analysis.Dispose();
    }
}
