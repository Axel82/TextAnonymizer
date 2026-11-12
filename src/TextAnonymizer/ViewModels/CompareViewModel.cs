using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextAnonymizer.Models.Diff;
using TextAnonymizer.Services;

namespace TextAnonymizer.ViewModels;

/// <summary>Choix affiché dans le sélecteur de mode de comparaison.</summary>
public sealed record DiffFormatOption(DiffFormat Format, string Label);

/// <summary>Choix affiché dans le sélecteur d'affichage.</summary>
public sealed record DisplayModeOption(DiffDisplayMode Mode, string Label);

/// <summary>
/// Page « Comparateur de fichiers » : saisie de A et B, comparaison en arrière-plan, navigation entre les différences,
/// options et exports. Les fichiers d'origine ne sont jamais écrits.
/// </summary>
public sealed partial class CompareViewModel : ObservableObject, IDisposable
{
    private const string AllFiles = "Tous les fichiers (*.*)|*.*";

    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;
    private CancellationTokenSource? _comparison;

    public CompareViewModel(IClipboardService clipboard, IDialogService dialogs, IThemeService theme)
    {
        _clipboard = clipboard;
        _dialogs = dialogs;
        _theme = theme;
        _selectedFormat = Formats[0];
        _selectedDisplayMode = DisplayModes[0];
        _theme.ThemeChanged += OnThemeChanged;
    }

    public ComparePane A { get; } = new("A", "Original");

    public ComparePane B { get; } = new("B", "Nouvelle version");

    public IReadOnlyList<DiffFormatOption> Formats { get; } =
    [
        new(DiffFormat.Auto, "Auto"),
        new(DiffFormat.Text, "Texte brut"),
        new(DiffFormat.Json, "JSON"),
        new(DiffFormat.Xml, "XML"),
        new(DiffFormat.Markdown, "Markdown")
    ];

    public IReadOnlyList<DisplayModeOption> DisplayModes { get; } =
    [
        new(DiffDisplayMode.SideBySide, "Côte à côte"),
        new(DiffDisplayMode.Unified, "Unifié")
    ];

    [ObservableProperty]
    private DiffFormatOption _selectedFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayMode), nameof(ShowSideBySide), nameof(ShowUnified))]
    private DisplayModeOption _selectedDisplayMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSideBySide), nameof(ShowUnified), nameof(ShowStatistics), nameof(ShowStructural))]
    private bool _isEditing = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isScrollSynced = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasResult), nameof(Result), nameof(Document), nameof(Statistics), nameof(SimilarityLabel), nameof(Structural),
        nameof(ShowStatistics), nameof(ShowStructural), nameof(FormatLabel), nameof(HasDifferences), nameof(NavigationLabel),
        nameof(SupportsStructure), nameof(SupportsPropertyOrder))]
    [NotifyCanExecuteChangedFor(
        nameof(ExportTextCommand), nameof(ExportHtmlCommand), nameof(ExportPatchCommand), nameof(CopyResultCommand),
        nameof(NextDifferenceCommand), nameof(PreviousDifferenceCommand))]
    private DiffOutcome? _outcome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavigationLabel), nameof(CurrentBlock))]
    private int _currentBlockIndex = -1;

    // Options de comparaison : toutes indépendantes.
    [ObservableProperty]
    private bool _ignoreCase;

    [ObservableProperty]
    private bool _trimWhitespace;

    [ObservableProperty]
    private bool _collapseWhitespace;

    [ObservableProperty]
    private bool _ignoreTabs;

    [ObservableProperty]
    private bool _ignoreBlankLines;

    [ObservableProperty]
    private bool _ignoreLineEndings = true;

    [ObservableProperty]
    private bool _ignoreAllWhitespace;

    /// <summary>JSON : un simple changement d'ordre des propriétés n'est pas une différence.</summary>
    [ObservableProperty]
    private bool _ignorePropertyOrder = true;

    /// <summary>JSON / XML : comparer ligne à ligne la forme normalisée plutôt que le texte d'origine.</summary>
    [ObservableProperty]
    private bool _compareNormalized;

    public DiffDisplayMode DisplayMode => SelectedDisplayMode.Mode;

    public bool HasResult => Outcome is not null;

    public DiffResult Result => Outcome?.Result ?? DiffResult.Empty;

    public DiffDocument Document => Outcome?.Document ?? DiffDocument.Empty;

    public DiffStatistics Statistics => Outcome?.Result.Statistics ?? DiffStatistics.Empty;

    public StructuralComparison? Structural => Outcome?.Structural;

    public bool ShowSideBySide => !IsEditing && DisplayMode == DiffDisplayMode.SideBySide;

    public bool ShowUnified => !IsEditing && DisplayMode == DiffDisplayMode.Unified;

    public bool ShowStatistics => !IsEditing && HasResult;

    public bool ShowStructural => ShowStatistics && Structural is not null;

    /// <summary>Le format comparé propose une forme normalisée (JSON ou XML valides).</summary>
    public bool SupportsStructure => Outcome?.Format is DiffFormat.Json or DiffFormat.Xml || SelectedFormat.Format is DiffFormat.Json or DiffFormat.Xml;

    public bool SupportsPropertyOrder => Outcome?.Format == DiffFormat.Json || SelectedFormat.Format == DiffFormat.Json;

    public bool HasDifferences => Result.Blocks.Count > 0;

    public DiffBlock? CurrentBlock =>
        CurrentBlockIndex >= 0 && CurrentBlockIndex < Result.Blocks.Count ? Result.Blocks[CurrentBlockIndex] : null;

    public string SimilarityLabel => Statistics.Similarity.ToString("P1", CultureInfo.CurrentCulture);

    public string NavigationLabel => !HasResult
        ? string.Empty
        : HasDifferences ? $"Différence {CurrentBlockIndex + 1} / {Result.Blocks.Count}" : "Aucune différence";

    public string FormatLabel
    {
        get
        {
            if (Outcome is null)
            {
                return string.Empty;
            }

            var name = Formats.First(f => f.Format == Outcome.Format).Label;
            var label = SelectedFormat.Format == DiffFormat.Auto ? $"Format détecté : {name}" : $"Format : {name}";
            return Outcome.IsNormalized ? label + " (forme normalisée)" : label;
        }
    }

    public bool IsDarkTheme => _theme.Current == AppTheme.Dark;

    /// <summary>Options actuellement cochées.</summary>
    public DiffOptions CurrentOptions => new()
    {
        IgnoreCase = IgnoreCase,
        TrimWhitespace = TrimWhitespace,
        CollapseWhitespace = CollapseWhitespace,
        IgnoreTabs = IgnoreTabs,
        IgnoreBlankLines = IgnoreBlankLines,
        IgnoreLineEndings = IgnoreLineEndings,
        IgnoreAllWhitespace = IgnoreAllWhitespace
    };

    partial void OnIgnoreCaseChanged(bool value) => OnSettingsChanged();

    partial void OnTrimWhitespaceChanged(bool value) => OnSettingsChanged();

    partial void OnCollapseWhitespaceChanged(bool value) => OnSettingsChanged();

    partial void OnIgnoreTabsChanged(bool value) => OnSettingsChanged();

    partial void OnIgnoreBlankLinesChanged(bool value) => OnSettingsChanged();

    partial void OnIgnoreLineEndingsChanged(bool value) => OnSettingsChanged();

    partial void OnIgnoreAllWhitespaceChanged(bool value) => OnSettingsChanged();

    partial void OnIgnorePropertyOrderChanged(bool value) => OnSettingsChanged();

    partial void OnCompareNormalizedChanged(bool value) => OnSettingsChanged();

    partial void OnSelectedFormatChanged(DiffFormatOption value)
    {
        OnPropertyChanged(nameof(SupportsStructure));
        OnPropertyChanged(nameof(SupportsPropertyOrder));
        OnSettingsChanged();
    }

    partial void OnOutcomeChanged(DiffOutcome? value) => CurrentBlockIndex = value is { Result.Blocks.Count: > 0 } ? 0 : -1;

    /// <summary>En vue résultat, toute modification des réglages relance la comparaison.</summary>
    private void OnSettingsChanged()
    {
        if (!IsEditing && HasResult)
        {
            _ = RunComparisonAsync(notify: false);
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(IsDarkTheme));

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task CompareAsync() => RunComparisonAsync(notify: true);

    /// <summary>Compare A et B hors du fil de l'interface ; une nouvelle comparaison annule la précédente.</summary>
    public async Task RunComparisonAsync(bool notify = true)
    {
        _comparison?.Cancel();
        var cancellation = new CancellationTokenSource();
        _comparison = cancellation;
        var request = new DiffRequest(
            A.Text, B.Text, CurrentOptions, SelectedFormat.Format, A.FilePath, B.FilePath, IgnorePropertyOrder, CompareNormalized);

        IsBusy = true;
        try
        {
            var outcome = await Task.Run(() => DiffComparison.Run(request, cancellation.Token), cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            Outcome = outcome;
            IsEditing = false;
            if (notify)
            {
                NotifyOutcome(outcome);
            }
        }
        catch (OperationCanceledException)
        {
            // Remplacée par une comparaison plus récente.
        }
        catch (Exception ex)
        {
            _dialogs.ShowError($"La comparaison a échoué : {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_comparison, cancellation))
            {
                IsBusy = false;
                _comparison = null;
            }

            cancellation.Dispose();
        }
    }

    private void NotifyOutcome(DiffOutcome outcome)
    {
        if (outcome.Request.TextA.Length == 0 && outcome.Request.TextB.Length == 0)
        {
            _dialogs.Notify("Les deux contenus sont vides.", NotificationKind.Info);
        }
        else if (outcome.Result.IsIdentical)
        {
            var exact = outcome.Request.TextA == outcome.Request.TextB;
            _dialogs.Notify(
                exact ? "Les deux contenus sont identiques." : "Aucune différence avec les options choisies.",
                NotificationKind.Info);
        }
        else
        {
            _dialogs.Notify($"{outcome.Result.Blocks.Count} différence(s) trouvée(s).", NotificationKind.Info);
        }
    }

    [RelayCommand]
    private void Edit() => IsEditing = true;

    [RelayCommand]
    private async Task SwapAsync()
    {
        A.SwapWith(B);
        if (!IsEditing)
        {
            await RunComparisonAsync(notify: false);
        }

        _dialogs.Notify("A et B ont été inversés.", NotificationKind.Info);
    }

    [RelayCommand]
    private void Clear()
    {
        _comparison?.Cancel();
        A.Clear();
        B.Clear();
        Outcome = null;
        IsEditing = true;
        _dialogs.Notify("Les deux panneaux ont été vidés.", NotificationKind.Info);
    }

    [RelayCommand(CanExecute = nameof(HasDifferences))]
    private void NextDifference() =>
        CurrentBlockIndex = (CurrentBlockIndex + 1) % Result.Blocks.Count;

    [RelayCommand(CanExecute = nameof(HasDifferences))]
    private void PreviousDifference() =>
        CurrentBlockIndex = (CurrentBlockIndex - 1 + Result.Blocks.Count) % Result.Blocks.Count;

    [RelayCommand]
    private async Task OpenFileAsync(ComparePane pane)
    {
        var path = _dialogs.PickOpenFile($"Ouvrir le fichier {pane.Letter}", AllFiles);
        if (path is not null)
        {
            await LoadFileAsync(pane, path);
        }
    }

    [RelayCommand]
    private async Task PasteAsync(ComparePane pane)
    {
        var text = _clipboard.GetText();
        if (string.IsNullOrEmpty(text))
        {
            _dialogs.Notify("Le presse-papiers ne contient pas de texte.", NotificationKind.Warning);
            return;
        }

        pane.SetText(text);
        _dialogs.Notify($"Texte collé dans {pane.Letter}.");
        if (!IsEditing)
        {
            await RunComparisonAsync(notify: false);
        }
    }

    /// <summary>Lit un fichier en lecture seule, sans bloquer l'interface ; en vue résultat, relance la comparaison.</summary>
    public async Task LoadFileAsync(ComparePane pane, string path)
    {
        if (await ReadIntoAsync(pane, path) && !IsEditing)
        {
            await RunComparisonAsync(notify: false);
        }
    }

    /// <summary>Fichiers déposés sur un panneau : le premier va dans ce panneau, un second éventuel dans l'autre (et la comparaison démarre).</summary>
    public async Task DropFilesAsync(ComparePane pane, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        var loaded = await ReadIntoAsync(pane, paths[0]);
        if (paths.Count > 1)
        {
            loaded |= await ReadIntoAsync(ReferenceEquals(pane, A) ? B : A, paths[1]);
        }

        if (loaded && (!IsEditing || paths.Count > 1))
        {
            await RunComparisonAsync(notify: paths.Count > 1);
        }
    }

    private async Task<bool> ReadIntoAsync(ComparePane pane, string path)
    {
        try
        {
            var content = await Task.Run(() => TextFileReader.Read(path));
            pane.Load(content);
            _dialogs.Notify($"{pane.Letter} : {content.FileName} ({content.EncodingName}).");
            return true;
        }
        catch (TextFileException ex)
        {
            _dialogs.ShowError(ex.Message, "Lecture impossible");
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private Task ExportTextAsync() =>
        ExportAsync("Exporter en texte", "Texte (*.txt)|*.txt", "comparaison.txt",
            o => DiffExporter.ToText(o.Result, o.Document, A.DisplayName, B.DisplayName));

    [RelayCommand(CanExecute = nameof(HasResult))]
    private Task ExportHtmlAsync() =>
        ExportAsync("Exporter en HTML", "Page HTML (*.html)|*.html", "comparaison.html",
            o => DiffExporter.ToHtml(o.Result, o.Document, DisplayMode, A.DisplayName, B.DisplayName));

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ExportPatchAsync()
    {
        if (Outcome is not { } outcome)
        {
            return;
        }

        if (outcome.Result.IsIdentical)
        {
            _dialogs.Notify("Aucune différence : le patch serait vide.", NotificationKind.Warning);
            return;
        }

        await ExportAsync("Exporter en patch", "Patch (*.patch;*.diff)|*.patch;*.diff", "comparaison.patch",
            o => DiffExporter.ToPatch(o.Result, "a/" + A.DisplayName, "b/" + B.DisplayName));

        if (outcome.IsNormalized || outcome.Result.Options.AltersContent)
        {
            _dialogs.Notify(
                "Options d'ignorance ou forme normalisée actives : le patch décrit la comparaison affichée et peut ne pas reproduire B à l'identique.",
                NotificationKind.Warning);
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyResult()
    {
        if (Outcome is not { } outcome)
        {
            return;
        }

        _clipboard.SetText(DiffExporter.ToText(outcome.Result, outcome.Document, A.DisplayName, B.DisplayName));
        _dialogs.Notify("Résultat copié dans le presse-papiers.");
    }

    private async Task ExportAsync(string title, string filter, string fileName, Func<DiffOutcome, string> build)
    {
        if (Outcome is not { } outcome)
        {
            return;
        }

        var path = _dialogs.PickSaveFile(title, filter + "|" + AllFiles, null, fileName);
        if (path is null)
        {
            return;
        }

        if (IsSource(path))
        {
            _dialogs.ShowError("Le fichier choisi est l'un des fichiers comparés : les fichiers d'origine ne sont jamais modifiés.");
            return;
        }

        try
        {
            await Task.Run(() => File.WriteAllText(path, build(outcome), new UTF8Encoding(false)));
            _dialogs.Notify($"Exporté : {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"Impossible d'enregistrer le fichier : {ex.Message}");
        }
    }

    private bool IsSource(string path)
    {
        var full = Path.GetFullPath(path);
        return new[] { A.FilePath, B.FilePath }.Any(p => p is not null && string.Equals(Path.GetFullPath(p), full, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _comparison?.Cancel();
        _theme.ThemeChanged -= OnThemeChanged;
    }
}
