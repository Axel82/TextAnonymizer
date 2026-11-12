using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using TextAnonymizer.Services;
using TextAnonymizer.ViewModels;
using TextAnonymizer.Views.Pages;
using Wpf.Ui.Controls;

namespace TextAnonymizer.Tests;

/// <summary>Charge la vraie page avec les vraies ressources, dans les deux modes d'affichage et les deux thèmes.</summary>
public class ComparePageSmokeTests
{
    [Fact]
    public void NavigationSymbol_Exists() => Assert.True(Enum.IsDefined(typeof(SymbolRegular), "BranchCompare24"));

    [Fact]
    public void Page_ShowsInputThenBothResultModes()
    {
        WpfTestHost.Run(() =>
        {
            var theme = new WpfTestHost.FakeTheme();
            var viewModel = new CompareViewModel(new WpfTestHost.FakeClipboard(), null!, theme);
            var page = new ComparePage { DataContext = viewModel };
            var nav = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.Left };
            nav.SetPageProviderService(new TextAnonymizer.Views.PageProvider(_ => page));
            nav.MenuItems.Add(new NavigationViewItem { Content = "c", TargetPageType = typeof(ComparePage) });
            var window = new Window { Content = nav, Width = 1200, Height = 760 };
            window.Show();
            nav.Navigate(typeof(ComparePage));
            WpfTestHost.DoEvents();

            var editorA = (TextEditor)page.FindName("EditorA");
            var diffA = (TextEditor)page.FindName("DiffA");
            var diffB = (TextEditor)page.FindName("DiffB");
            var unified = (TextEditor)page.FindName("DiffUnified");
            Assert.True(editorA.IsVisible);
            Assert.False(diffA.IsVisible);

            // Saisie dans l'éditeur → ViewModel (liaison existante réutilisée).
            editorA.Text = string.Join("\n", Enumerable.Range(1, 400).Select(i => $"ligne {i}"));
            Assert.StartsWith("ligne 1", viewModel.A.Text);
            viewModel.B.SetText(string.Join("\n", Enumerable.Range(1, 400).Select(i => i % 50 == 0 ? $"ligne {i} modifiée" : $"ligne {i}")));

            WpfTestHost.Wait(viewModel.RunComparisonAsync(notify: false));
            WpfTestHost.DoEvents();
            page.UpdateLayout();

            Assert.False(editorA.IsVisible);
            Assert.True(diffA.IsVisible);
            Assert.True(diffB.IsVisible);
            Assert.Equal(viewModel.Document.Left.Text, diffA.Text);
            Assert.Equal(diffA.Document.LineCount, diffB.Document.LineCount);
            Assert.True(diffA.IsReadOnly);

            // Navigation : la dernière différence est amenée à l'écran, et B suit A.
            viewModel.PreviousDifferenceCommand.Execute(null);
            WpfTestHost.DoEvents();
            Assert.True(diffA.VerticalOffset > 0);
            Assert.Equal(diffA.VerticalOffset, diffB.VerticalOffset, 1);

            // Défilement synchronisé, puis désynchronisé.
            diffA.ScrollToVerticalOffset(100);
            WpfTestHost.DoEvents();
            Assert.Equal(100, diffB.VerticalOffset, 1);
            viewModel.IsScrollSynced = false;
            diffA.ScrollToVerticalOffset(300);
            WpfTestHost.DoEvents();
            Assert.Equal(100, diffB.VerticalOffset, 1);

            viewModel.SelectedDisplayMode = viewModel.DisplayModes[1];
            WpfTestHost.DoEvents();
            Assert.True(unified.IsVisible);
            Assert.False(diffA.IsVisible);
            Assert.Equal(viewModel.Document.Unified.Text, unified.Text);

            theme.SetTheme(AppTheme.Dark);
            WpfTestHost.DoEvents();

            viewModel.EditCommand.Execute(null);
            WpfTestHost.DoEvents();
            Assert.True(editorA.IsVisible);

            // Panneaux empilés sous 900 px.
            window.Width = 700;
            WpfTestHost.DoEvents();
            var cardB = (Border)page.FindName("CardB");
            Assert.Equal(1, Grid.GetRow(cardB));

            window.Close();
            viewModel.Dispose();
        });
    }
}
