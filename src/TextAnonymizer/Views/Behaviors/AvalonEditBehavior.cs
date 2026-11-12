using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using TextAnonymizer.Converters;
using TextAnonymizer.Models;

namespace TextAnonymizer.Views.Behaviors;

/// <summary>
/// Rend <see cref="TextEditor"/> utilisable en MVVM :
/// <c>Text</c> (liaison bidirectionnelle) pour la zone Source, <c>Result</c> (texte + segments surlignés) pour la zone Résultat.
/// Les segments proviennent du <see cref="ReplacementResult"/> calculé par le service : la vue ne recalcule rien.
/// </summary>
public static class AvalonEditBehavior
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(AvalonEditBehavior),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty ResultProperty = DependencyProperty.RegisterAttached(
        "Result",
        typeof(ReplacementResult),
        typeof(AvalonEditBehavior),
        new PropertyMetadata(null, OnResultChanged));

    private static readonly DependencyProperty AdapterProperty = DependencyProperty.RegisterAttached(
        "Adapter",
        typeof(EditorAdapter),
        typeof(AvalonEditBehavior));

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);

    public static ReplacementResult? GetResult(DependencyObject element) =>
        (ReplacementResult?)element.GetValue(ResultProperty);

    public static void SetResult(DependencyObject element, ReplacementResult? value) =>
        element.SetValue(ResultProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
        {
            return;
        }

        var adapter = GetAdapter(editor);
        var text = e.NewValue as string ?? string.Empty;
        if (editor.Text != text)
        {
            adapter.IsUpdating = true;
            editor.Text = text;
            adapter.IsUpdating = false;
        }
    }

    private static void OnResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
        {
            return;
        }

        var adapter = GetAdapter(editor);
        var result = e.NewValue as ReplacementResult;
        adapter.Segments = result?.Segments ?? [];
        editor.Text = result?.Text ?? string.Empty;
        editor.TextArea.TextView.Redraw();
    }

    private static EditorAdapter GetAdapter(TextEditor editor)
    {
        if (editor.GetValue(AdapterProperty) is EditorAdapter existing)
        {
            return existing;
        }

        var adapter = new EditorAdapter(editor);
        editor.SetValue(AdapterProperty, adapter);
        return adapter;
    }

    /// <summary>Branche recherche, copie en texte brut, colorisation et tooltip sur un éditeur.</summary>
    private sealed class EditorAdapter
    {
        private readonly TextEditor _editor;
        private readonly ToolTip _tooltip = new();

        public EditorAdapter(TextEditor editor)
        {
            _editor = editor;
            SearchPanel.Install(editor);
            DataObject.AddCopyingHandler(editor.TextArea, OnCopying);

            editor.TextChanged += (_, _) =>
            {
                if (!IsUpdating)
                {
                    SetText(editor, editor.Text);
                }
            };

            editor.TextArea.TextView.LineTransformers.Add(new SegmentColorizer(this));
            editor.MouseHover += OnMouseHover;
            editor.MouseHoverStopped += (_, _) => _tooltip.IsOpen = false;
        }

        public bool IsUpdating { get; set; }

        public IReadOnlyList<ReplacementSegment> Segments { get; set; } = [];

        /// <summary>Copie du texte brut uniquement (pas de RTF/HTML).</summary>
        private void OnCopying(object sender, DataObjectCopyingEventArgs e)
        {
            if (e.IsDragDrop)
            {
                return;
            }

            e.CancelCommand();
            var text = _editor.TextArea.Selection.GetText();
            if (text.Length > 0)
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
            }
        }

        private void OnMouseHover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
            if (position is null || Segments.Count == 0)
            {
                return;
            }

            var offset = _editor.Document.GetOffset(position.Value.Location);
            var segment = Segments.FirstOrDefault(s => offset >= s.Index && offset < s.Index + s.Length);
            if (segment is null)
            {
                return;
            }

            _tooltip.PlacementTarget = _editor;
            _tooltip.Content = $"Valeur d'origine : {segment.MatchedText}\nType : {segment.Entry.EntityType}";
            _tooltip.IsOpen = true;
        }
    }

    /// <summary>Applique le fond de chaque segment remplacé (couleur par type d'entité).</summary>
    private sealed class SegmentColorizer(EditorAdapter adapter) : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
        {
            foreach (var segment in adapter.Segments)
            {
                var start = Math.Max(segment.Index, line.Offset);
                var end = Math.Min(segment.Index + segment.Length, line.EndOffset);
                if (start >= end)
                {
                    continue;
                }

                var brush = EntityColors.BrushFor(segment.Entry.EntityType);
                ChangeLinePart(start, end, element =>
                {
                    element.TextRunProperties.SetBackgroundBrush(brush);
                    element.TextRunProperties.SetForegroundBrush(Brushes.Black);
                });
            }
        }
    }
}
