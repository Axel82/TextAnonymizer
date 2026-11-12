using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using TextAnonymizer.Views.Pages;

namespace TextAnonymizer.Views;

/// <summary>Fenêtre principale : câblage des services d'interface uniquement (aucune logique métier).</summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(
        object viewModel,
        ISnackbarService snackbar,
        IContentDialogService dialogs,
        INavigationViewPageProvider pages)
    {
        DataContext = viewModel;
        InitializeComponent();

        snackbar.SetSnackbarPresenter(SnackbarPresenter);
        dialogs.SetDialogHost(DialogHost);
        RootNavigation.SetPageProviderService(pages);
        Loaded += (_, _) => RootNavigation.Navigate(typeof(AnonymizationPage));
    }
}
