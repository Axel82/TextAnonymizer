using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextAnonymizer.Models;
using TextAnonymizer.Services;

namespace TextAnonymizer.ViewModels;

/// <summary>Orchestre les trois zones (source, résultat, table) et les commandes de l'écran d'anonymisation.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IConversionService _conversion;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;

    /// <summary>Dernière anonymisation : permet de rétablir le texte source à l'identique.</summary>
    private ReplacementResult? _lastAnonymization;

    /// <summary>L'avertissement « table modifiée » a déjà été affiché pour l'anonymisation en cours.</summary>
    private bool _modificationWarned;

    public MainViewModel(
        IConversionService conversion,
        IClipboardService clipboard,
        IDialogService dialogs,
        TableViewModel table)
    {
        _conversion = conversion;
        _clipboard = clipboard;
        _dialogs = dialogs;
        Table = table;

        Table.TableChanged += OnTableChanged;
        Table.StatusChanged += (_, message) => _dialogs.Notify(message);
        Table.PropertyChanged += OnTablePropertyChanged;
    }

    public TableViewModel Table { get; }

    [ObservableProperty]
    private string _sourceText = string.Empty;

    /// <summary>Texte affiché dans la zone Résultat, avec les segments à surligner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultText), nameof(ShowResultGuide))]
    private ReplacementResult _result = new(string.Empty, string.Empty, []);

    [ObservableProperty]
    private bool _isTableModifiedWarningVisible;

    public string ResultText => Result.Text;

    /// <summary>État vide : aucune table et rien à afficher, la zone Résultat guide l'utilisateur.</summary>
    public bool ShowResultGuide => Table.IsEmpty && string.IsNullOrEmpty(ResultText);

    /// <summary>Une génération est en cours : les actions sont désactivées et l'indicateur d'activité est affiché.</summary>
    public bool IsBusy => Table.IsGenerating;

    private bool CanRun => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Anonymize()
    {
        if (string.IsNullOrEmpty(SourceText))
        {
            _dialogs.ShowError("Aucun texte à anonymiser : collez ou saisissez un texte dans la zone Source.");
            return;
        }

        if (!TryValidateTable())
        {
            return;
        }

        var result = _conversion.Anonymize(SourceText, Table.Table);
        Result = result;
        _lastAnonymization = result;
        Table.IsReversibilityLocked = true;
        IsTableModifiedWarningVisible = false;
        _modificationWarned = false;

        if (result.Segments.Count == 0)
        {
            _dialogs.Notify("Aucune correspondance trouvée dans le texte source.", NotificationKind.Warning);
        }
        else
        {
            _dialogs.Notify($"{result.Segments.Count} remplacement{(result.Segments.Count > 1 ? "s" : string.Empty)} appliqué{(result.Segments.Count > 1 ? "s" : string.Empty)}.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Restore()
    {
        if (string.IsNullOrEmpty(ResultText))
        {
            _dialogs.ShowError("Aucun texte à rétablir : la zone Résultat est vide.");
            return;
        }

        ReplacementResult restored;
        if (_lastAnonymization is not null && ResultText == _lastAnonymization.Text)
        {
            restored = _conversion.Restore(_lastAnonymization, ResultText, Table.Table);
        }
        else if (TryValidateTable())
        {
            restored = _conversion.Restore(ResultText, Table.Table);
        }
        else
        {
            return;
        }

        SourceText = restored.Text;
        Table.IsReversibilityLocked = false;
        IsTableModifiedWarningVisible = false;

        if (restored.Segments.Count == 0)
        {
            _dialogs.Notify("Aucun remplaçant trouvé dans le texte du résultat.", NotificationKind.Warning);
        }
        else
        {
            _dialogs.Notify("Texte original rétabli.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task GenerateAutomaticallyAsync()
    {
        await Table.ShowGenerationPreviewAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Preview()
    {
        Table.ShowPreview(SourceText);
    }

    [RelayCommand]
    private void PasteSource()
    {
        if (TryGetClipboardText() is { } text)
        {
            SourceText = text;
            _dialogs.Notify("Texte collé dans la zone Source.");
        }
    }

    [RelayCommand]
    private void PasteResult()
    {
        if (TryGetClipboardText() is { } text)
        {
            Result = new ReplacementResult(string.Empty, text, []);
            _dialogs.Notify("Texte collé dans la zone Résultat.");
        }
    }

    [RelayCommand]
    private void CopySource() => CopyToClipboard(SourceText, "Source");

    [RelayCommand]
    private void CopyResult() => CopyToClipboard(ResultText, "Résultat");

    [RelayCommand]
    private void ClearSource()
    {
        SourceText = string.Empty;
        _dialogs.Notify("Zone Source vidée.", NotificationKind.Info);
    }

    [RelayCommand]
    private void ClearResult()
    {
        Result = new ReplacementResult(string.Empty, string.Empty, []);
        _dialogs.Notify("Zone Résultat vidée.", NotificationKind.Info);
    }

    private void CopyToClipboard(string text, string zone)
    {
        if (string.IsNullOrEmpty(text))
        {
            _dialogs.ShowError($"Aucun texte à copier : la zone {zone} est vide.");
            return;
        }

        try
        {
            _clipboard.SetText(text);
            _dialogs.Notify("Texte copié (texte brut).");
        }
        catch (COMException ex)
        {
            _dialogs.ShowError($"Le presse-papiers est inaccessible : {ex.Message}");
        }
    }

    private bool TryValidateTable()
    {
        if (Table.Table.Entries.Count == 0)
        {
            _dialogs.ShowError("La table de conversion est vide : ajoutez des entrées (ou chargez une table).");
            return false;
        }

        var validation = Table.Table.ValidateBijection();
        if (validation.IsValid)
        {
            return true;
        }

        _dialogs.ShowError(validation.Message, "Table invalide : anonymisation impossible");
        return false;
    }

    private string? TryGetClipboardText()
    {
        try
        {
            var text = _clipboard.GetText();
            if (string.IsNullOrEmpty(text))
            {
                _dialogs.ShowError("Le presse-papiers ne contient pas de texte.");
                return null;
            }

            return text;
        }
        catch (COMException ex)
        {
            _dialogs.ShowError($"Le presse-papiers est inaccessible : {ex.Message}");
            return null;
        }
    }

    private void OnTableChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowResultGuide));

        if (_lastAnonymization is null || !Table.IsReversibilityLocked)
        {
            return;
        }

        IsTableModifiedWarningVisible = true;
        if (!_modificationWarned)
        {
            _modificationWarned = true;
            _dialogs.ShowWarning(
                "La table a été modifiée après une anonymisation : le rétablissement du texte déjà anonymisé risque de ne plus être possible ou correct.",
                "Risque de perte de réversibilité");
        }
    }

    private void OnTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableViewModel.IsGenerating))
        {
            OnPropertyChanged(nameof(IsBusy));
            AnonymizeCommand.NotifyCanExecuteChanged();
            RestoreCommand.NotifyCanExecuteChanged();
            GenerateAutomaticallyCommand.NotifyCanExecuteChanged();
            PreviewCommand.NotifyCanExecuteChanged();
        }
    }
}
