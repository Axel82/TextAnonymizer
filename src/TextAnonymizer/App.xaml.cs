using System.Windows;
using TextAnonymizer.Models;
using TextAnonymizer.Services;
using TextAnonymizer.ViewModels;
using TextAnonymizer.Views;
using TextAnonymizer.Views.Pages;
using Wpf.Ui;

namespace TextAnonymizer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = new SettingsService();
        var theme = new Services.ThemeService(settings);
        theme.ApplyPersisted();

        var snackbar = new SnackbarService();
        var contentDialogs = new ContentDialogService();
        IDialogService dialogs = new DialogService(snackbar, contentDialogs);

        var conversion = new ConversionService();
        var table = new TableViewModel(
            new ConversionTable(),
            new TableStorageService(),
            new GeneratorService(),
            conversion,
            dialogs);
        var main = new MainViewModel(conversion, new ClipboardService(), dialogs, table);
        var settingsViewModel = new SettingsViewModel(theme, settings, table, dialogs);

        var viewer = new ViewerViewModel(new ClipboardService(), dialogs, theme);
        var compare = new CompareViewModel(new ClipboardService(), dialogs, theme);

        var pages = new PageProvider(type => type switch
        {
            _ when type == typeof(AnonymizationPage) => new AnonymizationPage { DataContext = main },
            _ when type == typeof(TablePage) => new TablePage { DataContext = table },
            _ when type == typeof(ViewerPage) => new ViewerPage { DataContext = viewer },
            _ when type == typeof(ComparePage) => new ComparePage { DataContext = compare },
            _ when type == typeof(SettingsPage) => new SettingsPage { DataContext = settingsViewModel },
            _ => null
        });

        var window = new MainWindow(main, snackbar, contentDialogs, pages);
        MainWindow = window;
        window.Show();

        table.TryLoadDefault();
    }
}
