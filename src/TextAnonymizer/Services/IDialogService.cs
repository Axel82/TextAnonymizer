using TextAnonymizer.ViewModels;

namespace TextAnonymizer.Services;

public enum NotificationKind
{
    Success,
    Info,
    Warning,
    Error
}

/// <summary>Dialogues, notifications et sélecteurs de fichiers, pour garder les ViewModels indépendants des fenêtres.</summary>
public interface IDialogService
{
    /// <summary>Affiche une notification temporaire (snackbar), sans bloquer.</summary>
    void Notify(string message, NotificationKind kind = NotificationKind.Success);

    void ShowInfo(string message, string title = "Information");

    void ShowError(string message, string title = "Erreur");

    /// <summary>Affiche un avertissement sans bloquer l'appelant.</summary>
    void ShowWarning(string message, string title = "Attention");

    bool Confirm(string message, string title = "Confirmation");

    string? PickOpenFile(string title, string filter, string? initialDirectory = null);

    string? PickSaveFile(string title, string filter, string? initialDirectory = null, string? fileName = null);

    /// <summary>Affiche l'aperçu ; renvoie <c>true</c> si l'utilisateur en a validé tout ou partie.</summary>
    bool ShowPreview(PreviewViewModel preview);
}
