using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using TextAnonymizer.Models.Diff;
using TextAnonymizer.ViewModels;
using TextAnonymizer.Views.Rendering;

namespace TextAnonymizer.Views.Pages;

/// <summary>
/// Page « Comparateur de fichiers » : affiche le <see cref="DiffDocument"/> calculé par le ViewModel dans les éditeurs,
/// synchronise leur défilement, suit la différence courante et reçoit les fichiers déposés. Aucun calcul de diff ici.
/// </summary>
public partial class ComparePage : Page
{
    /// <summary>Sous cette largeur, A et B s'empilent (comme le Moteur de rendu).</summary>
    private const double StackBreakpoint = 900;

    private readonly DiffEditorDecorator _left;
    private readonly DiffEditorDecorator _right;
    private readonly DiffEditorDecorator _unified;
    private bool? _stacked;
    private bool _syncing;

    public ComparePage()
    {
        InitializeComponent();
        _left = new DiffEditorDecorator(DiffA, DiffEditorDecorator.Numbers.A);
        _right = new DiffEditorDecorator(DiffB, DiffEditorDecorator.Numbers.B);
        _unified = new DiffEditorDecorator(DiffUnified, DiffEditorDecorator.Numbers.Both);

        DiffA.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(DiffA, DiffB);
        DiffB.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll(DiffB, DiffA);
        DataContextChanged += OnDataContextChanged;
    }

    private CompareViewModel? ViewModel => DataContext as CompareViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CompareViewModel old)
        {
            old.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is CompareViewModel current)
        {
            current.PropertyChanged += OnViewModelPropertyChanged;
            ApplyColors(current.IsDarkTheme);
            Render(current);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(CompareViewModel.Outcome):
                Render(viewModel);
                break;
            case nameof(CompareViewModel.CurrentBlockIndex):
                ShowCurrentBlock(viewModel, scroll: true);
                break;
            case nameof(CompareViewModel.SelectedDisplayMode):
            case nameof(CompareViewModel.IsEditing):
                // Les éditeurs viennent d'apparaître : on attend leur mise en page avant de défiler.
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ShowCurrentBlock(viewModel, scroll: true));
                break;
            case nameof(CompareViewModel.IsScrollSynced) when viewModel.IsScrollSynced:
                SyncScroll(DiffA, DiffB);
                break;
            case nameof(CompareViewModel.IsDarkTheme):
                ApplyColors(viewModel.IsDarkTheme);
                break;
        }
    }

    private void Render(CompareViewModel viewModel)
    {
        var document = viewModel.Document;
        _left.Show(document.Left);
        _right.Show(document.Right);
        _unified.Show(document.Unified);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ShowCurrentBlock(viewModel, scroll: true));
    }

    private void ShowCurrentBlock(CompareViewModel viewModel, bool scroll)
    {
        var index = viewModel.CurrentBlockIndex;
        var document = viewModel.Document;
        DiffViewBlock? Block(DiffView view) => index >= 0 && index < view.Blocks.Count ? view.Blocks[index] : null;

        _syncing = true;
        try
        {
            _left.SetCurrentBlock(Block(document.Left), scroll);
            _right.SetCurrentBlock(Block(document.Right), scroll);
            _unified.SetCurrentBlock(Block(document.Unified), scroll);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ApplyColors(bool dark)
    {
        var colors = DiffColors.For(dark);
        _left.SetColors(colors);
        _right.SetColors(colors);
        _unified.SetColors(colors);
    }

    /// <summary>
    /// Les deux vues ayant exactement les mêmes rangées, un même décalage montre les mêmes lignes.
    /// On lit le décalage effectivement appliqué par la vue (celui du <c>ScrollViewer</c> peut être en retard)
    /// et on ignore l'écho quand la cible est déjà à la bonne position.
    /// </summary>
    private void SyncScroll(TextEditor source, TextEditor target)
    {
        if (_syncing || ViewModel is not { IsScrollSynced: true })
        {
            return;
        }

        var from = source.TextArea.TextView;
        var to = target.TextArea.TextView;
        if (Math.Abs(from.VerticalOffset - to.VerticalOffset) < 0.5 && Math.Abs(from.HorizontalOffset - to.HorizontalOffset) < 0.5)
        {
            return;
        }

        _syncing = true;
        try
        {
            target.ScrollToVerticalOffset(from.VerticalOffset);
            target.ScrollToHorizontalOffset(from.HorizontalOffset);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnPaneDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void OnPaneDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } viewModel || e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        e.Handled = true;
        var pane = (string)((FrameworkElement)sender).Tag == "B" ? viewModel.B : viewModel.A;
        await viewModel.DropFilesAsync(pane, paths);
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
            Place(CardA, 0, 0, new Thickness(0, 0, 0, 6));
            Place(CardB, 1, 0, new Thickness(0, 6, 0, 0));
            Grid.SetRowSpan(UnifiedCard, 2);
            Grid.SetColumnSpan(UnifiedCard, 1);
        }
        else
        {
            Panes.ColumnDefinitions.Add(new ColumnDefinition());
            Panes.ColumnDefinitions.Add(new ColumnDefinition());
            Place(CardA, 0, 0, new Thickness(0, 0, 6, 0));
            Place(CardB, 0, 1, new Thickness(6, 0, 0, 0));
            Grid.SetRowSpan(UnifiedCard, 1);
            Grid.SetColumnSpan(UnifiedCard, 2);
        }
    }

    private static void Place(FrameworkElement card, int row, int column, Thickness margin)
    {
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        card.Margin = margin;
    }
}
