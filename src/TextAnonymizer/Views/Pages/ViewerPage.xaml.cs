using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using ICSharpCode.AvalonEdit;
using TextAnonymizer.Models;
using TextAnonymizer.ViewModels;
using TextAnonymizer.Views.Rendering;

namespace TextAnonymizer.Views.Pages;

/// <summary>
/// Page « Moteur de rendu » : traduit l'analyse du ViewModel en contrôles (arbre, document, navigateur, éditeurs).
/// Aucune détection ni validation ici : tout vient de <see cref="ViewerAnalysis"/>.
/// </summary>
public partial class ViewerPage : Page
{
    /// <summary>Sous cette largeur, les deux panneaux s'empilent (éditeur au-dessus, rendu en dessous).</summary>
    private const double StackBreakpoint = 900;

    private bool? _stacked;
    private bool _navigatingProgrammatically;
    private string? _pendingHtml;
    private string? _shownHtml;

    public ViewerPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ViewerViewModel? ViewModel => DataContext as ViewerViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ViewerViewModel old)
        {
            old.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ViewerViewModel current)
        {
            current.PropertyChanged += OnViewModelPropertyChanged;
            Render(current.Analysis);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        // Le thème change les couleurs calculées à la main : on redessine le rendu courant.
        if (e.PropertyName is nameof(ViewerViewModel.Analysis) or nameof(ViewerViewModel.IsDarkTheme))
        {
            Render(viewModel.Analysis);
        }
    }

    private void Render(ViewerAnalysis analysis)
    {
        var source = SyntaxPalette.ForFormat(analysis.Format);
        Editor.SyntaxHighlighting = null;
        Editor.SyntaxHighlighting = source;

        _pendingHtml = analysis.HtmlPreview;
        _shownHtml = null;

        ResetTabs(analysis);
        if (analysis.IsEmpty)
        {
            return;
        }

        switch (analysis.Format)
        {
            case ViewerFormat.Json:
                RenderJson(analysis);
                break;
            case ViewerFormat.Markdown:
                MarkdownTab.Visibility = Visibility.Visible;
                MarkdownViewer.Document = MarkdownFlowDocumentBuilder.Build(analysis.Text);
                break;
            case ViewerFormat.Html:
            case ViewerFormat.Php:
                ShowCode(analysis.Text, analysis.Format);
                break;
        }

        SelectFirstVisibleTab();
    }

    private void ResetTabs(ViewerAnalysis analysis)
    {
        JsonTree.Items.Clear();
        JsonTab.Visibility = Visibility.Collapsed;
        MarkdownTab.Visibility = Visibility.Collapsed;
        PreviewTab.Visibility = analysis.HtmlPreview is null ? Visibility.Collapsed : Visibility.Visible;
        JsonErrorPanel.Visibility = Visibility.Collapsed;
        JsonTree.Visibility = Visibility.Visible;
        FormattedTab.Visibility = Visibility.Collapsed;
        CodeTab.Visibility = Visibility.Collapsed;
        MarkdownViewer.Document = null;
    }

    private void RenderJson(ViewerAnalysis analysis)
    {
        JsonTab.Visibility = Visibility.Visible;
        if (analysis.Json is { IsValid: true } json)
        {
            JsonTree.Items.Add(JsonTreeBuilder.Build(json.Root));
            ShowCodeIn(FormattedEditor, analysis.FormattedJson ?? string.Empty, ViewerFormat.Json);
            FormattedTab.Visibility = Visibility.Visible;
            return;
        }

        JsonErrorText.Text = analysis.Json?.Error ?? "JSON invalide.";
        JsonErrorPanel.Visibility = Visibility.Visible;
        JsonTree.Visibility = Visibility.Collapsed;
    }

    private void ShowCode(string text, ViewerFormat format)
    {
        ShowCodeIn(CodeEditor, text, format);
        CodeTab.Header = format == ViewerFormat.Php ? "Code" : "Code source";
        CodeTab.Visibility = Visibility.Visible;
    }

    private static void ShowCodeIn(TextEditor editor, string text, ViewerFormat format)
    {
        editor.SyntaxHighlighting = null;
        editor.SyntaxHighlighting = SyntaxPalette.ForFormat(format);
        editor.Text = text;
    }

    /// <summary>Sélectionne le premier onglet visible, sauf si l'onglet courant l'est toujours.</summary>
    private void SelectFirstVisibleTab()
    {
        if (Tabs.SelectedItem is TabItem { Visibility: Visibility.Visible })
        {
            ShowHtmlIfVisible();
            return;
        }

        Tabs.SelectedItem = Tabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Visibility == Visibility.Visible);
        ShowHtmlIfVisible();
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == Tabs)
        {
            ShowHtmlIfVisible();
        }
    }

    // Les onglets non sélectionnés n'ont pas de WebBrowser instancié : on ne navigue que lorsque l'aperçu est visible.
    private void ShowHtmlIfVisible()
    {
        if (!ReferenceEquals(Tabs.SelectedItem, PreviewTab) || _pendingHtml is null || _pendingHtml == _shownHtml)
        {
            return;
        }

        _shownHtml = _pendingHtml;
        _navigatingProgrammatically = true;
        HtmlBrowser.NavigateToString(_pendingHtml);
    }

    // Toute navigation autre que le chargement du contenu (clic sur un lien, redirection, formulaire) est annulée.
    private void OnHtmlNavigating(object sender, NavigatingCancelEventArgs e)
    {
        if (!_navigatingProgrammatically)
        {
            e.Cancel = true;
        }
    }

    private void OnHtmlLoadCompleted(object sender, NavigationEventArgs e) => _navigatingProgrammatically = false;

    private void OnMarkdownLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri.Scheme is "http" or "https" or "mailto")
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private void OnPanesSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < StackBreakpoint;
        if (_stacked == stacked)
        {
            return;
        }

        _stacked = stacked;
        Panes.ColumnDefinitions.Clear();
        Panes.RowDefinitions.Clear();
        if (stacked)
        {
            Panes.RowDefinitions.Add(new RowDefinition());
            Panes.RowDefinitions.Add(new RowDefinition());
            Grid.SetRow(EditorCard, 0);
            Grid.SetColumn(EditorCard, 0);
            Grid.SetRow(RenderCard, 1);
            Grid.SetColumn(RenderCard, 0);
            EditorCard.Margin = new Thickness(0, 0, 0, 6);
            RenderCard.Margin = new Thickness(0, 6, 0, 0);
        }
        else
        {
            Panes.ColumnDefinitions.Add(new ColumnDefinition());
            Panes.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(EditorCard, 0);
            Grid.SetColumn(EditorCard, 0);
            Grid.SetRow(RenderCard, 0);
            Grid.SetColumn(RenderCard, 1);
            EditorCard.Margin = new Thickness(0, 0, 6, 0);
            RenderCard.Margin = new Thickness(6, 0, 0, 0);
        }
    }
}
