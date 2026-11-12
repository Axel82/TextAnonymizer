using CommunityToolkit.Mvvm.ComponentModel;
using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.ViewModels;

/// <summary>Un des deux contenus comparés (A ou B) : texte saisi ou fichier chargé.</summary>
public sealed partial class ComparePane(string letter, string role) : ObservableObject
{
    private bool _isLoading;

    public string Letter { get; } = letter;

    public string Title { get; } = $"{letter} — {role}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(Description))]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description), nameof(DisplayName), nameof(FilePath))]
    private TextFileContent? _file;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    private bool _isModified;

    public bool IsEmpty => Text.Length == 0;

    public string? FilePath => File?.Path;

    /// <summary>Nom utilisé dans les exports : nom du fichier, sinon la lettre du panneau.</summary>
    public string DisplayName => File?.FileName ?? Letter;

    public string Description => File is { } file
        ? $"{file.FileName} · {file.EncodingName} · {LineSplitter.Label(file.LineEnding)}{(IsModified ? " · modifié" : string.Empty)}"
        : IsEmpty ? "Vide" : "Texte saisi";

    partial void OnTextChanged(string value)
    {
        if (!_isLoading && File is not null)
        {
            IsModified = true;
        }
    }

    public void Load(TextFileContent content) => Set(content.Text, content, false);

    public void SetText(string text) => Set(text, null, false);

    public void Clear() => SetText(string.Empty);

    /// <summary>Échange le contenu et l'origine des deux panneaux.</summary>
    public void SwapWith(ComparePane other)
    {
        var (text, file, modified) = (Text, File, IsModified);
        Set(other.Text, other.File, other.IsModified);
        other.Set(text, file, modified);
    }

    private void Set(string text, TextFileContent? file, bool modified)
    {
        _isLoading = true;
        try
        {
            File = file;
            Text = text;
            IsModified = modified;
        }
        finally
        {
            _isLoading = false;
        }
    }
}
