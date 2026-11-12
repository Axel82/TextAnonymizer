using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TextAnonymizer.Models;
using TextAnonymizer.ViewModels;

namespace TextAnonymizer.Views.Pages;

public partial class TablePage : Page
{
    /// <summary>Colonnes parcourues par Tab : Original, Remplaçant, Type (les autres sont en lecture seule).</summary>
    private const int LastNavigableColumn = 2;

    public TablePage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TableViewModel old)
        {
            old.EntryAdded -= OnEntryAdded;
        }

        if (e.NewValue is TableViewModel current)
        {
            current.EntryAdded += OnEntryAdded;
        }
    }

    /// <summary>Place le curseur dans la zone « Original » de la ligne qui vient d'être ajoutée.</summary>
    private void OnEntryAdded(object? sender, ConversionEntry entry) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => FocusCell(entry, 0));

    /// <summary>Tab / Maj+Tab : parcourt les cellules de gauche à droite puis de haut en bas (et inversement).</summary>
    private void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || (Keyboard.Modifiers & ModifierKeys.Control) != 0
            || FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject) is not { } cell
            || cell.DataContext is not ConversionEntry entry)
        {
            return;
        }

        var backwards = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var row = EntriesGrid.Items.IndexOf(entry);
        var column = cell.Column.DisplayIndex;
        if (row < 0 || column > LastNavigableColumn)
        {
            return;
        }

        column += backwards ? -1 : 1;
        if (column < 0)
        {
            column = LastNavigableColumn;
            row--;
        }
        else if (column > LastNavigableColumn)
        {
            column = 0;
            row++;
        }

        // Aux extrémités de la grille, Tab quitte la grille comme d'habitude.
        if (row < 0 || row >= EntriesGrid.Items.Count)
        {
            return;
        }

        e.Handled = true;
        EntriesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        FocusCell(EntriesGrid.Items[row], column);
    }

    private void FocusCell(object item, int columnIndex)
    {
        var column = EntriesGrid.Columns[columnIndex];
        EntriesGrid.SelectedItem = item;
        EntriesGrid.ScrollIntoView(item, column);
        EntriesGrid.UpdateLayout();
        EntriesGrid.CurrentCell = new DataGridCellInfo(item, column);

        if (columnIndex < LastNavigableColumn)
        {
            EntriesGrid.Focus();
            EntriesGrid.BeginEdit();
            return;
        }

        if (EntriesGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row
            && FindDescendant<ComboBox>(row) is { } combo)
        {
            combo.Focus();
        }
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var node = start; node is not null; node = node is Visual or System.Windows.Media.Media3D.Visual3D
                 ? VisualTreeHelper.GetParent(node)
                 : LogicalTreeHelper.GetParent(node))
        {
            if (node is T match)
            {
                return match;
            }
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
