using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using TextAnonymizer.ViewModels;
using TextAnonymizer.Views;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace TextAnonymizer.Services;

/// <summary>
/// Implémentation WPF UI : snackbar pour les notifications, <see cref="ContentDialog"/> pour les messages et l'aperçu.
/// Les dialogues « bloquants » attendent leur résultat en pompant la boucle de messages (comme <c>MessageBox</c>),
/// ce qui évite de rendre asynchrones toutes les commandes des ViewModels.
/// Une demande de dialogue pendant qu'un autre est ouvert est ignorée (réponse par défaut : non).
/// </summary>
public sealed class DialogService(ISnackbarService snackbar, IContentDialogService dialogs) : IDialogService
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(3);

    private bool _isDialogOpen;

    private static Window? Owner => Application.Current?.MainWindow;

    public void Notify(string message, NotificationKind kind = NotificationKind.Success)
    {
        var (title, appearance, symbol) = kind switch
        {
            NotificationKind.Success => ("Terminé", ControlAppearance.Success, SymbolRegular.CheckmarkCircle24),
            NotificationKind.Warning => ("Attention", ControlAppearance.Caution, SymbolRegular.Important24),
            NotificationKind.Error => ("Erreur", ControlAppearance.Danger, SymbolRegular.ErrorCircle24),
            _ => ("Information", ControlAppearance.Info, SymbolRegular.Info24)
        };

        snackbar.Show(title, message, appearance, new SymbolIcon(symbol), ToastDuration);
    }

    public void ShowInfo(string message, string title = "Information") =>
        Wait(ShowMessageAsync(title, message, primaryText: null));

    public void ShowError(string message, string title = "Erreur") =>
        Wait(ShowMessageAsync(title, message, primaryText: null));

    public void ShowWarning(string message, string title = "Attention") =>
        Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => _ = ShowMessageAsync(title, message, primaryText: null));

    public bool Confirm(string message, string title = "Confirmation") =>
        Wait(ShowMessageAsync(title, message, primaryText: "Oui", closeText: "Non")) == ContentDialogResult.Primary;

    public string? PickOpenFile(string title, string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, InitialDirectory = initialDirectory ?? string.Empty };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string title, string filter, string? initialDirectory = null, string? fileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            InitialDirectory = initialDirectory ?? string.Empty,
            FileName = fileName ?? string.Empty,
            OverwritePrompt = true
        };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public bool ShowPreview(PreviewViewModel preview)
    {
        if (_isDialogOpen || dialogs.GetDialogHostEx() is not { } host)
        {
            return false;
        }

        var accepted = false;
        var dialog = new ContentDialog(host)
        {
            Title = preview.Title,
            Content = new PreviewDialogContent { DataContext = preview },
            IsFooterVisible = false,
            DialogMaxWidth = 960,
            DialogMaxHeight = 640
        };
        preview.CloseRequested += (_, isAccepted) =>
        {
            accepted = isAccepted;
            dialog.Hide(isAccepted ? ContentDialogResult.Primary : ContentDialogResult.None);
        };

        Wait(ShowAsync(dialog));
        return accepted;
    }

    private Task<ContentDialogResult> ShowMessageAsync(string title, string message, string? primaryText, string closeText = "Fermer")
    {
        if (_isDialogOpen || dialogs.GetDialogHostEx() is not { } host)
        {
            return Task.FromResult(ContentDialogResult.None);
        }

        var dialog = new ContentDialog(host)
        {
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }
            },
            PrimaryButtonText = primaryText ?? string.Empty,
            CloseButtonText = closeText,
            DialogMaxWidth = 560
        };
        return ShowAsync(dialog);
    }

    private async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        _isDialogOpen = true;
        try
        {
            return await dialogs.ShowAsync(dialog, CancellationToken.None);
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private static T Wait<T>(Task<T> task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }

        return task.GetAwaiter().GetResult();
    }
}
