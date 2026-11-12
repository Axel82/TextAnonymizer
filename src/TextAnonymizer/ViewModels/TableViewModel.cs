using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TextAnonymizer.Models;
using TextAnonymizer.Services;

namespace TextAnonymizer.ViewModels;

/// <summary>CRUD, validation, import/export, persistance et aperçu de la table de conversion.</summary>
public sealed partial class TableViewModel : ObservableObject
{
    private const string EncryptedFilter = "Table chiffrée (*.enc)|*.enc|Tous les fichiers (*.*)|*.*";
    private const CompareOptions FilterOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private readonly ITableStorageService _storage;
    private readonly IGeneratorService _generator;
    private readonly IConversionService _conversion;
    private readonly IDialogService _dialogs;

    public TableViewModel(
        ConversionTable table,
        ITableStorageService storage,
        IGeneratorService generator,
        IConversionService conversion,
        IDialogService dialogs)
    {
        Table = table;
        _storage = storage;
        _generator = generator;
        _conversion = conversion;
        _dialogs = dialogs;
        _currentPath = storage.DefaultPath;

        EntriesView = CollectionViewSource.GetDefaultView(Table.Entries);
        EntriesView.Filter = MatchesFilter;
        Table.Entries.CollectionChanged += OnEntriesChanged;
        RefreshState();
    }

    public ConversionTable Table { get; }

    /// <summary>Vue filtrable/triable des entrées, liée à la grille.</summary>
    public ICollectionView EntriesView { get; }

    public IReadOnlyList<EntityType> EntityTypes { get; } = Enum.GetValues<EntityType>();

    public IReadOnlyList<MatchingStrategy> Strategies { get; } = Enum.GetValues<MatchingStrategy>();

    /// <summary>Levé à chaque modification de la table (entrées ajoutées/supprimées/éditées, stratégie).</summary>
    public event EventHandler? TableChanged;

    /// <summary>Levé après l'ajout d'une entrée vide, pour que la vue place le curseur dans sa zone « Original ».</summary>
    public event EventHandler<ConversionEntry>? EntryAdded;

    /// <summary>Levé pour signaler le résultat d'une action à l'utilisateur (snackbar).</summary>
    public event EventHandler<string>? StatusChanged;

    [ObservableProperty]
    private ConversionEntry? _selectedEntry;

    /// <summary>Option « génération automatique » (affiche « Générer automatiquement » et active les propositions de l'aperçu).</summary>
    [ObservableProperty]
    private bool _autoGenerateEnabled;

    /// <summary>Texte de recherche : filtre instantané sur les colonnes Original et Remplaçant.</summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>Une génération est en cours (animation + boutons désactivés).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private bool _isGenerating;

    [ObservableProperty]
    private string _currentPath;

    [ObservableProperty]
    private bool _isBijectionValid = true;

    /// <summary>Doublons / champs vides détectés, un par ligne ; vide si la table est valide.</summary>
    [ObservableProperty]
    private string _validationSummary = string.Empty;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _tableSummary = string.Empty;

    /// <summary>
    /// Vrai tant qu'une anonymisation dépend de la table actuelle :
    /// toute modification demandée par une commande doit alors être confirmée.
    /// </summary>
    public bool IsReversibilityLocked { get; set; }

    public MatchingStrategy Strategy
    {
        get => Table.Strategy;
        set
        {
            if (Table.Strategy == value)
            {
                return;
            }

            if (!ConfirmModification())
            {
                OnPropertyChanged();
                return;
            }

            Table.Strategy = value;
            OnPropertyChanged();
            RaiseTableChanged();
            SetStatus($"Stratégie de correspondance : {value}.");
        }
    }

    partial void OnFilterTextChanged(string value) => EntriesView.Refresh();

    /// <summary>Charge la table par défaut si elle existe ; une erreur est signalée sans bloquer le démarrage.</summary>
    public void TryLoadDefault()
    {
        if (!File.Exists(_storage.DefaultPath))
        {
            return;
        }

        try
        {
            ReplaceContent(_storage.Load(_storage.DefaultPath));
            SetStatus($"Table chargée ({Table.Entries.Count} entrée(s)).");
        }
        catch (TableStorageException ex)
        {
            _dialogs.ShowError(ex.Message, "Chargement de la table");
        }
    }

    [RelayCommand]
    private void AddEntry()
    {
        if (!ConfirmModification())
        {
            return;
        }

        FilterText = string.Empty;
        var entry = new ConversionEntry();
        Table.Entries.Add(entry);
        SelectedEntry = entry;
        EntryAdded?.Invoke(this, entry);
        SetStatus("Entrée ajoutée : renseignez le mot original et son type.");
    }

    [RelayCommand]
    private void RemoveEntry()
    {
        if (SelectedEntry is null)
        {
            _dialogs.ShowError("Sélectionnez d'abord une entrée à supprimer.");
            return;
        }

        var label = string.IsNullOrWhiteSpace(SelectedEntry.Original) ? "cette entrée vide" : $"« {SelectedEntry.Original} »";
        if (!_dialogs.Confirm($"Supprimer {label} de la table ?", "Supprimer l'entrée") || !ConfirmModification())
        {
            return;
        }

        Table.Entries.Remove(SelectedEntry);
        SelectedEntry = null;
        SetStatus("Entrée supprimée.");
    }

    [RelayCommand]
    private void Validate()
    {
        if (IsBijectionValid)
        {
            SetStatus($"Bijection valide ({Table.Entries.Count} entrée(s)).");
        }
        else
        {
            _dialogs.ShowError(ValidationSummary, "Table invalide");
        }
    }

    [RelayCommand]
    private void Load()
    {
        var path = _dialogs.PickOpenFile("Charger la table", EncryptedFilter, Path.GetDirectoryName(CurrentPath));
        if (path is null || !ConfirmModification())
        {
            return;
        }

        try
        {
            var loaded = _storage.Load(path);
            if (!EnsureNoDuplicates(loaded, "Chargement refusé"))
            {
                return;
            }

            ReplaceContent(loaded);
            CurrentPath = path;
            SetStatus($"Table chargée : {Table.Entries.Count} entrée(s).");
        }
        catch (TableStorageException ex)
        {
            _dialogs.ShowError(ex.Message, "Chargement de la table");
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (!EnsureNoDuplicates(Table, "Enregistrement refusé"))
        {
            return;
        }

        try
        {
            _storage.Save(Table, CurrentPath);
            SetStatus("Table enregistrée (chiffrée).");
        }
        catch (TableStorageException ex)
        {
            _dialogs.ShowError(ex.Message, "Enregistrement de la table");
        }
    }

    /// <summary>Ajoute à la table les entrées d'un fichier chiffré, sans remplacer l'existant.</summary>
    [RelayCommand]
    private void Import()
    {
        var path = _dialogs.PickOpenFile("Importer une table chiffrée", EncryptedFilter, Path.GetDirectoryName(CurrentPath));
        if (path is null)
        {
            return;
        }

        try
        {
            var imported = _storage.Load(path);
            var merged = new ConversionTable { Strategy = Table.Strategy };
            foreach (var entry in Table.Entries.Concat(imported.Entries))
            {
                merged.Entries.Add(entry);
            }

            if (!EnsureNoDuplicates(merged, "Import refusé : la table obtenue serait invalide") || !ConfirmModification())
            {
                return;
            }

            foreach (var entry in imported.Entries)
            {
                Table.Entries.Add(entry);
            }

            SetStatus($"{imported.Entries.Count} entrée(s) importée(s).");
        }
        catch (TableStorageException ex)
        {
            _dialogs.ShowError(ex.Message, "Import de la table");
        }
    }

    /// <summary>Enregistre une copie chiffrée de la table dans le fichier choisi (la table courante ne change pas de fichier).</summary>
    [RelayCommand]
    private void Export()
    {
        if (!EnsureNoDuplicates(Table, "Export refusé"))
        {
            return;
        }

        var path = _dialogs.PickSaveFile("Exporter la table (chiffrée)", EncryptedFilter, fileName: "table-export.enc");
        if (path is null)
        {
            return;
        }

        try
        {
            _storage.Save(Table, path);
            SetStatus("Table exportée (chiffrée).");
        }
        catch (TableStorageException ex)
        {
            _dialogs.ShowError(ex.Message, "Export de la table");
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync() => await ShowGenerationPreviewAsync();

    private bool CanGenerate() => !IsGenerating;

    /// <summary>Propose des valeurs pour les entrées sans remplaçant, puis laisse l'utilisateur les modifier et les valider.</summary>
    public async Task<bool> ShowGenerationPreviewAsync()
    {
        if (!EnsureNoDuplicates(Table, "Génération impossible"))
        {
            return false;
        }

        // La génération travaille sur une copie : la grille reste éditable pendant l'attente.
        var snapshot = new ConversionTable { Strategy = Table.Strategy };
        var originals = new Dictionary<ConversionEntry, ConversionEntry>();
        foreach (var entry in Table.Entries)
        {
            var copy = new ConversionEntry
            {
                Original = entry.Original,
                Replacement = entry.Replacement,
                EntityType = entry.EntityType,
                IsAutoGenerated = entry.IsAutoGenerated
            };
            snapshot.Entries.Add(copy);
            originals[copy] = entry;
        }

        IReadOnlyList<GeneratedValue> proposals;
        IsGenerating = true;
        try
        {
            proposals = await Task.Run(() => _generator.GenerateMissing(snapshot));
        }
        finally
        {
            IsGenerating = false;
        }

        if (proposals.Count == 0)
        {
            _dialogs.Notify(
                "Aucune entrée à générer : renseignez un mot original et un type, en laissant le remplaçant vide.",
                NotificationKind.Warning);
            return false;
        }

        if (!ConfirmModification())
        {
            return false;
        }

        var items = proposals.Select(p => new PreviewItem(originals[p.Entry], p.Value, 0, isGenerated: true));
        var preview = new PreviewViewModel(
            "Aperçu de la génération automatique",
            "Modifiez les valeurs proposées si besoin, puis validez pour les appliquer à la table.",
            items,
            Table);
        return Apply(preview, "Valeurs générées appliquées à la table.");
    }

    /// <summary>
    /// Affiche tous les remplacements de la table (avec leur nombre d'occurrences dans <paramref name="sourceText"/>) ;
    /// les remplaçants vides sont complétés par une proposition si la génération automatique est activée.
    /// </summary>
    public bool ShowPreview(string sourceText)
    {
        if (Table.Entries.Count == 0)
        {
            _dialogs.ShowError("La table est vide : ajoutez des entrées avant de lancer l'aperçu.");
            return false;
        }

        if (!EnsureNoDuplicates(Table, "Table invalide"))
        {
            return false;
        }

        var proposals = AutoGenerateEnabled
            ? _generator.GenerateMissing(Table).ToDictionary(p => p.Entry, p => p.Value)
            : [];
        var counts = _conversion.CountOccurrences(sourceText, Table);

        var items = Table.Entries.Select(e => proposals.TryGetValue(e, out var value)
            ? new PreviewItem(e, value, counts.GetValueOrDefault(e), isGenerated: true)
            : new PreviewItem(e, e.Replacement, counts.GetValueOrDefault(e), isGenerated: false));
        var preview = new PreviewViewModel(
            "Aperçu des remplacements",
            "Modifiez les remplaçants ligne par ligne, puis validez pour mettre à jour la table (ou annulez pour ne rien changer).",
            items,
            Table);

        if (!ConfirmModification())
        {
            return false;
        }

        return Apply(preview, "Aperçu validé : table mise à jour.");
    }

    private bool Apply(PreviewViewModel preview, string statusOnAccept)
    {
        if (!_dialogs.ShowPreview(preview))
        {
            _dialogs.Notify("Aperçu annulé : la table n'a pas été modifiée.", NotificationKind.Info);
            return false;
        }

        SetStatus(statusOnAccept);
        RaiseTableChanged();
        return true;
    }

    /// <summary>Demande confirmation avant de modifier une table dont dépend une anonymisation en cours.</summary>
    private bool ConfirmModification() =>
        !IsReversibilityLocked
        || _dialogs.Confirm(
            "Un texte a été anonymisé avec la table actuelle : la modifier peut rendre le rétablissement impossible ou incorrect.\n\nModifier quand même ?",
            "Perte de réversibilité");

    private bool EnsureNoDuplicates(ConversionTable table, string title)
    {
        var result = table.ValidateBijection(allowEmptyReplacements: true);
        if (result.IsValid)
        {
            return true;
        }

        _dialogs.ShowError(result.Message, title);
        return false;
    }

    private void ReplaceContent(ConversionTable source)
    {
        Table.Entries.Clear();
        foreach (var entry in source.Entries)
        {
            Table.Entries.Add(entry);
        }

        Table.Strategy = source.Strategy;
        OnPropertyChanged(nameof(Strategy));
        RaiseTableChanged();
    }

    private bool MatchesFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(FilterText) || item is not ConversionEntry entry)
        {
            return true;
        }

        var compare = CultureInfo.CurrentCulture.CompareInfo;
        return compare.IndexOf(entry.Original, FilterText, FilterOptions) >= 0
               || compare.IndexOf(entry.Replacement, FilterText, FilterOptions) >= 0;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ConversionEntry entry in e.OldItems)
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ConversionEntry entry in e.NewItems)
            {
                entry.PropertyChanged += OnEntryPropertyChanged;
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var entry in Table.Entries)
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
                entry.PropertyChanged += OnEntryPropertyChanged;
            }
        }

        RaiseTableChanged();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseTableChanged();

    private void RaiseTableChanged()
    {
        RefreshState();
        TableChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshState()
    {
        var validation = Table.ValidateBijection();
        IsBijectionValid = validation.IsValid;
        ValidationSummary = validation.Message;
        IsEmpty = Table.Entries.Count == 0;
        TableSummary = IsEmpty
            ? "Aucune table chargée"
            : $"Table chargée : {Table.Entries.Count} {(Table.Entries.Count > 1 ? "entrées" : "entrée")}";
    }

    private void SetStatus(string message) => StatusChanged?.Invoke(this, message);
}
