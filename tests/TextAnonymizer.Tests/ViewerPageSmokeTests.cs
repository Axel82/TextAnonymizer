using System.Windows;
using System.Windows.Controls;
using TextAnonymizer.Models;
using TextAnonymizer.Services;
using TextAnonymizer.ViewModels;
using TextAnonymizer.Views.Pages;
using Wpf.Ui.Controls;

namespace TextAnonymizer.Tests;

/// <summary>Instancie la vraie page avec les vraies ressources : détecte les erreurs XAML qui n'apparaissent qu'à l'exécution.</summary>
public class ViewerPageSmokeTests
{
    private sealed class FakeClipboard : IClipboardService
    {
        public string? GetText() => null;

        public void SetText(string text)
        {
        }
    }

    private sealed class FakeTheme : IThemeService
    {
        public AppTheme Current => AppTheme.Light;

        public event EventHandler? ThemeChanged
        {
            add { }
            remove { }
        }

        public void ApplyPersisted()
        {
        }

        public void SetTheme(AppTheme theme)
        {
        }
    }

    [Fact]
    public void NavigationSymbol_Exists() => Assert.True(Enum.IsDefined(typeof(SymbolRegular), "DocumentText24"));

    private static readonly string[] Samples =
    [
        "{\"a\": [1, 2, {\"b\": null}]}",
        "{\"a\": 1,}",
        "# Titre\n\n| A | B |\n|---|---|\n| 1 | 2 |\n",
        "<h1>Bonjour</h1><script>alert(1)</script>",
        "<?php $x = 1; ?><p>salut</p>",
        ""
    ];

    // Une seule instance d'Application par processus : partagée par tous les tests d'interface (WpfTestHost).
    [Fact]
    public void Page_RendersEveryFormatWithoutError()
    {
        WpfTestHost.Run(() =>
        {
            var viewModel = new ViewerViewModel(new FakeClipboard(), null!, new FakeTheme());
            var page = new ViewerPage { DataContext = viewModel };
            var nav = new Wpf.Ui.Controls.NavigationView { PaneDisplayMode = Wpf.Ui.Controls.NavigationViewPaneDisplayMode.Left };
            nav.SetPageProviderService(new TextAnonymizer.Views.PageProvider(_ => page));
            nav.MenuItems.Add(new Wpf.Ui.Controls.NavigationViewItem { Content = "v", TargetPageType = typeof(ViewerPage) });
            var window = new Window { Content = nav, Width = 1100, Height = 700 };
            window.Show();
            nav.Navigate(typeof(ViewerPage));
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            window.UpdateLayout();

            foreach (var text in Samples)
            {
                viewModel.Text = text;
                viewModel.Refresh();
                page.UpdateLayout();

                var tabs = (TabControl)page.FindName("Tabs");
                Assert.Equal(text.Length == 0, tabs.Visibility == Visibility.Collapsed);
                if (text.Length > 0)
                {
                    Assert.IsType<TabItem>(tabs.SelectedItem);
                }
            }

            viewModel.Text = string.Join("\n", Enumerable.Range(0, 300).Select(i => new string('x', 300) + i));
            viewModel.Refresh();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            window.UpdateLayout();
            // Dans le NavigationView, la page doit être bornée à la fenêtre : les zones défilent d'elles-mêmes.
            var editor = (ICSharpCode.AvalonEdit.TextEditor)page.FindName("Editor");
            var scroller = (ScrollViewer)editor.Template.FindName("PART_ScrollViewer", editor);
            Assert.True(page.ActualHeight <= window.ActualHeight);
            Assert.True(editor.ActualHeight < page.ActualHeight);
            Assert.Equal(Visibility.Visible, scroller.ComputedVerticalScrollBarVisibility);
            Assert.Equal(Visibility.Visible, scroller.ComputedHorizontalScrollBarVisibility);

            window.Close();
            viewModel.Dispose();
        });
    }
}
