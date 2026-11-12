using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using TextAnonymizer.Models.Diff;

namespace TextAnonymizer.Views.Rendering;

/// <summary>
/// Transforme un <see cref="TextEditor"/> en lecture seule en vue de différences : fond de chaque ligne selon sa nature,
/// surlignage des parties modifiées, cadre autour du bloc courant et marge affichant les vrais numéros de A et de B
/// (les lignes de remplissage n'en ont pas). L'éditeur reste virtualisé : seules les lignes visibles sont dessinées.
/// </summary>
public sealed class DiffEditorDecorator
{
    /// <summary>Numéros affichés dans la marge.</summary>
    public enum Numbers
    {
        A,
        B,
        Both
    }

    private readonly TextEditor _editor;
    private readonly NumberMargin _margin;
    private DiffView _view = DiffView.Empty;
    private DiffViewBlock? _current;
    private int _maxNumber;

    public DiffEditorDecorator(TextEditor editor, Numbers numbers)
    {
        _editor = editor;
        editor.IsReadOnly = true;
        editor.ShowLineNumbers = false;
        editor.WordWrap = false;
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        SearchPanel.Install(editor);

        _margin = new NumberMargin(this, numbers);
        editor.TextArea.LeftMargins.Insert(0, _margin);
        editor.TextArea.LeftMargins.Insert(1, DottedLineMargin.Create());
        editor.TextArea.TextView.BackgroundRenderers.Add(new LineRenderer(this));
        editor.TextArea.TextView.LineTransformers.Add(new SpanColorizer(this));
    }

    public DiffColors Colors { get; private set; } = DiffColors.Light;

    public void Show(DiffView view)
    {
        _view = view;
        _current = null;
        _maxNumber = 0;
        foreach (var line in view.Lines)
        {
            _maxNumber = Math.Max(_maxNumber, Math.Max(line.NumberA, line.NumberB));
        }

        _editor.Document = new TextDocument(view.Text);
        _margin.InvalidateMeasure();
    }

    public void SetColors(DiffColors colors)
    {
        Colors = colors;
        Redraw();
    }

    /// <summary>Encadre le bloc (ou rien) et le fait défiler au tiers supérieur de la vue.</summary>
    public void SetCurrentBlock(DiffViewBlock? block, bool scroll)
    {
        _current = block;
        Redraw();
        if (scroll && block is { } b)
        {
            ScrollTo(b.FirstLine);
        }
    }

    public void ScrollTo(int line)
    {
        var textView = _editor.TextArea.TextView;
        if (line < 1 || line > _editor.Document.LineCount)
        {
            return;
        }

        textView.EnsureVisualLines();
        var top = textView.GetVisualTopByDocumentLine(line);
        _editor.ScrollToVerticalOffset(Math.Max(0, top - (textView.ActualHeight / 3)));
        _editor.ScrollToHorizontalOffset(0);
    }

    private void Redraw()
    {
        _editor.TextArea.TextView.Redraw();
        _margin.InvalidateVisual();
    }

    private DiffLineInfo? Info(int lineNumber) =>
        lineNumber >= 1 && lineNumber <= _view.Lines.Count ? _view.Lines[lineNumber - 1] : null;

    private sealed class LineRenderer(DiffEditorDecorator owner) : IBackgroundRenderer
    {
        private static readonly Typeface MarkerFace = new(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!textView.VisualLinesValid)
            {
                return;
            }

            var colors = owner.Colors;
            var width = textView.ActualWidth;
            foreach (var visualLine in textView.VisualLines)
            {
                var number = visualLine.FirstDocumentLine.LineNumber;
                if (owner.Info(number) is not { } info)
                {
                    continue;
                }

                var y = visualLine.VisualTop - textView.VerticalOffset;
                if (colors.LineBackground(info) is { } brush)
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(0, y, width, visualLine.Height));
                }

                if (info.OnlyLineEndingDiffers && !info.IsFiller)
                {
                    var text = new FormattedText(
                        "  ↵ fin de ligne différente",
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        MarkerFace,
                        textView.DefaultLineHeight * 0.75,
                        colors.Marker,
                        VisualTreeHelper.GetDpi(textView).PixelsPerDip);
                    var x = visualLine.TextLines[0].WidthIncludingTrailingWhitespace - textView.HorizontalOffset;
                    drawingContext.DrawText(text, new Point(x, y + ((visualLine.Height - text.Height) / 2)));
                }
            }

            if (owner._current is { } block && block.LineCount > 0)
            {
                var top = textView.GetVisualTopByDocumentLine(block.FirstLine) - textView.VerticalOffset;
                var last = Math.Min(block.FirstLine + block.LineCount - 1, textView.Document.LineCount);
                var bottom = textView.GetVisualTopByDocumentLine(last) + textView.DefaultLineHeight - textView.VerticalOffset;
                if (bottom >= 0 && top <= textView.ActualHeight)
                {
                    drawingContext.DrawRectangle(null, colors.CurrentBlock, new Rect(1, top + 1, Math.Max(0, textView.ActualWidth - 2), Math.Max(0, bottom - top - 2)));
                }
            }
        }
    }

    private sealed class SpanColorizer(DiffEditorDecorator owner) : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            if (owner.Info(line.LineNumber) is not { Spans.Count: > 0 } info)
            {
                return;
            }

            var brush = owner.Colors.SpanBackground(info);
            var contentStart = line.Offset + owner._view.PrefixLength;
            foreach (var span in info.Spans)
            {
                var start = Math.Min(contentStart + span.Start, line.EndOffset);
                var end = Math.Min(contentStart + span.End, line.EndOffset);
                if (start < end)
                {
                    ChangeLinePart(start, end, element => element.TextRunProperties.SetBackgroundBrush(brush));
                }
            }
        }
    }

    /// <summary>Numéros d'origine (A, B ou les deux) ; rien pour une ligne de remplissage.</summary>
    private sealed class NumberMargin : AbstractMargin
    {
        private const double ColumnGap = 10;
        private readonly DiffEditorDecorator _owner;
        private readonly Numbers _numbers;

        public NumberMargin(DiffEditorDecorator owner, Numbers numbers)
        {
            _owner = owner;
            _numbers = numbers;
        }

        private bool TwoColumns => _numbers == Numbers.Both;

        private Typeface Typeface => new(
            (FontFamily)GetValue(TextBlock.FontFamilyProperty),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        private double FontSize => (double)GetValue(TextBlock.FontSizeProperty);

        private FormattedText Format(string text) => new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface,
            FontSize,
            _owner._editor.LineNumbersForeground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        private double ColumnWidth
        {
            get
            {
                var digits = Math.Max(2, _owner._maxNumber.ToString(CultureInfo.InvariantCulture).Length);
                return Format(new string('9', digits)).Width;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var columns = TwoColumns ? 2 : 1;
            return new Size((ColumnWidth * columns) + (ColumnGap * columns), 0);
        }

        protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
        {
            if (oldTextView is not null)
            {
                oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
            }

            base.OnTextViewChanged(oldTextView, newTextView);
            if (newTextView is not null)
            {
                newTextView.VisualLinesChanged += OnVisualLinesChanged;
            }

            InvalidateVisual();
        }

        private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
            new PointHitTestResult(this, hitTestParameters.HitPoint);

        protected override void OnRender(DrawingContext drawingContext)
        {
            var textView = TextView;
            if (textView is not { VisualLinesValid: true })
            {
                return;
            }

            var columnWidth = ColumnWidth;
            foreach (var visualLine in textView.VisualLines)
            {
                if (_owner.Info(visualLine.FirstDocumentLine.LineNumber) is not { } info)
                {
                    continue;
                }

                var y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.TextTop) - textView.VerticalOffset;
                var first = info.IsFiller ? 0 : _numbers == Numbers.B ? info.NumberB : info.NumberA;
                Draw(drawingContext, first, columnWidth, y);
                if (TwoColumns)
                {
                    Draw(drawingContext, info.NumberB, (columnWidth * 2) + ColumnGap, y);
                }
            }
        }

        private void Draw(DrawingContext drawingContext, int number, double right, double y)
        {
            if (number <= 0)
            {
                return;
            }

            var text = Format(number.ToString(CultureInfo.InvariantCulture));
            drawingContext.DrawText(text, new Point(right - text.Width, y));
        }
    }
}
