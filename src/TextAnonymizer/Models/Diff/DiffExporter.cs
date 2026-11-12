using System.Globalization;
using System.Net;
using System.Text;

namespace TextAnonymizer.Models.Diff;

public enum DiffDisplayMode
{
    SideBySide,
    Unified
}

/// <summary>Exports du résultat : texte lisible, patch au format unifié (<c>diff -u</c>) et HTML autonome en couleurs.</summary>
public static class DiffExporter
{
    public const int PatchContext = 3;

    private const string NoNewline = "\\ No newline at end of file";

    /// <summary>Résumé puis vue unifiée, avec les numéros de ligne de A et de B.</summary>
    public static string ToText(DiffResult result, DiffDocument document, string nameA, string nameB)
    {
        var text = new StringBuilder();
        text.Append("Comparaison de fichiers").Append('\n');
        text.Append("A (original) : ").Append(nameA).Append('\n');
        text.Append("B (nouveau)  : ").Append(nameB).Append('\n');
        text.Append(Summary(result.Statistics)).Append('\n');
        text.Append('\n');

        var view = document.Unified;
        var lines = view.Text.Split('\n');
        for (var i = 0; i < view.Lines.Count; i++)
        {
            var info = view.Lines[i];
            text.Append(Number(info.NumberA)).Append(' ').Append(Number(info.NumberB)).Append(" │ ").Append(lines[i]).Append('\n');
        }

        return text.ToString();
    }

    public static string Summary(DiffStatistics stats) => string.Create(
        CultureInfo.CurrentCulture,
        $"{stats.Differences} différence(s) — identiques : {stats.Identical}, ajoutées : {stats.Added}, " +
        $"supprimées : {stats.Deleted}, modifiées : {stats.Modified} — similarité : {stats.Similarity:P1}");

    private static string Number(int number) => number == 0 ? "      " : number.ToString(CultureInfo.InvariantCulture).PadLeft(6);

    /// <summary>
    /// Patch au format unifié, applicable à A par <c>patch</c> ou <c>git apply</c>. Les lignes que les options
    /// considèrent comme égales sont écrites en contexte avec le texte de A.
    /// </summary>
    public static string ToPatch(DiffResult result, string nameA, string nameB, int context = PatchContext)
    {
        var ops = Operations(result);
        var patch = new StringBuilder();
        if (!ops.Any(o => o.Type != ' '))
        {
            return string.Empty;
        }

        patch.Append("--- ").Append(nameA).Append('\n');
        patch.Append("+++ ").Append(nameB).Append('\n');

        var index = 0;
        while (index < ops.Count)
        {
            var firstChange = ops.FindIndex(index, o => o.Type != ' ');
            if (firstChange < 0)
            {
                break;
            }

            var start = Math.Max(index, firstChange - context);
            var end = firstChange;
            while (true)
            {
                var lastChange = end;
                var next = ops.FindIndex(lastChange + 1, o => o.Type != ' ');
                if (next >= 0 && next - lastChange - 1 <= 2 * context)
                {
                    end = next;
                    continue;
                }

                end = Math.Min(ops.Count, lastChange + 1 + context);
                break;
            }

            WriteHunk(patch, ops, start, end);
            index = end;
        }

        return patch.ToString();
    }

    private readonly record struct Op(char Type, TextLine Line, int LineA, int LineB);

    /// <summary>Opérations dans l'ordre d'un patch : dans chaque groupe de changements, les « - » puis les « + ».</summary>
    private static List<Op> Operations(DiffResult result)
    {
        var ops = new List<Op>(result.Rows.Count);
        var minus = new List<TextLine>();
        var plus = new List<TextLine>();
        int lineA = 1, lineB = 1;

        void Flush()
        {
            foreach (var line in minus)
            {
                ops.Add(new Op('-', line, lineA++, lineB));
            }

            foreach (var line in plus)
            {
                ops.Add(new Op('+', line, lineA, lineB++));
            }

            minus.Clear();
            plus.Clear();
        }

        foreach (var row in result.Rows)
        {
            if (row.Left is not null && row.Right is not null && row.Kind is DiffRowKind.Equal or DiffRowKind.Ignored)
            {
                Flush();
                ops.Add(new Op(' ', row.Left, lineA++, lineB++));
                continue;
            }

            if (row.Left is not null)
            {
                minus.Add(row.Left);
            }

            if (row.Right is not null)
            {
                plus.Add(row.Right);
            }
        }

        Flush();
        return ops;
    }

    private static void WriteHunk(StringBuilder patch, List<Op> ops, int start, int end)
    {
        var countA = 0;
        var countB = 0;
        for (var i = start; i < end; i++)
        {
            countA += ops[i].Type == '+' ? 0 : 1;
            countB += ops[i].Type == '-' ? 0 : 1;
        }

        var startA = countA == 0 ? ops[start].LineA - 1 : ops[start].LineA;
        var startB = countB == 0 ? ops[start].LineB - 1 : ops[start].LineB;
        patch.Append(CultureInfo.InvariantCulture, $"@@ -{Range(startA, countA)} +{Range(startB, countB)} @@\n");
        for (var i = start; i < end; i++)
        {
            patch.Append(ops[i].Type).Append(ops[i].Line.Content).Append('\n');
            if (ops[i].Line.Terminator.Length == 0)
            {
                patch.Append(NoNewline).Append('\n');
            }
        }
    }

    private static string Range(int start, int count) =>
        count == 1 ? start.ToString(CultureInfo.InvariantCulture) : string.Create(CultureInfo.InvariantCulture, $"{start},{count}");

    /// <summary>Page HTML autonome (styles intégrés), dans le mode d'affichage demandé.</summary>
    public static string ToHtml(DiffResult result, DiffDocument document, DiffDisplayMode mode, string nameA, string nameB)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"fr\">\n<head>\n<meta charset=\"utf-8\">\n");
        html.Append("<title>Comparaison : ").Append(Encode(nameA)).Append(" / ").Append(Encode(nameB)).Append("</title>\n");
        html.Append("""
            <style>
            body { font-family: "Segoe UI", sans-serif; margin: 24px; color: #1f2328; }
            h1 { font-size: 20px; margin: 0 0 8px; }
            p.summary { color: #59636e; margin: 4px 0 16px; }
            table.diff { border-collapse: collapse; width: 100%; font-family: Consolas, "Courier New", monospace; font-size: 13px; }
            table.diff td { padding: 0 8px; vertical-align: top; white-space: pre; }
            td.num { color: #8c959f; text-align: right; user-select: none; width: 1%; border-right: 1px solid #d1d9e0; }
            th { text-align: left; font-family: "Segoe UI", sans-serif; padding: 6px 8px; background: #f6f8fa; border-bottom: 1px solid #d1d9e0; }
            tr.added td.code, td.code.added { background: #e6ffec; }
            tr.deleted td.code, td.code.deleted { background: #ffebe9; }
            tr.modified td.code, td.code.modified { background: #fff8c5; }
            td.code.filler { background: repeating-linear-gradient(45deg, #f6f8fa, #f6f8fa 4px, #eaeef2 4px, #eaeef2 8px); }
            tr.ignored td.code { color: #8c959f; }
            span.del { background: #ff818266; }
            span.add { background: #4ac26b66; }
            span.eol { color: #9a6700; font-style: italic; }
            </style>
            </head>
            <body>
            <h1>Comparaison de fichiers</h1>

            """);
        html.Append("<p>A (original) : <strong>").Append(Encode(nameA)).Append("</strong> — B (nouveau) : <strong>")
            .Append(Encode(nameB)).Append("</strong></p>\n");
        html.Append("<p class=\"summary\">").Append(Encode(Summary(result.Statistics))).Append("</p>\n");

        if (mode == DiffDisplayMode.SideBySide)
        {
            html.Append("<table class=\"diff side\">\n<tr><th colspan=\"2\">A — ").Append(Encode(nameA))
                .Append("</th><th colspan=\"2\">B — ").Append(Encode(nameB)).Append("</th></tr>\n");
            foreach (var row in result.Rows)
            {
                html.Append("<tr>");
                Cell(html, row.Left, row.LeftSpans, row, true);
                Cell(html, row.Right, row.RightSpans, row, false);
                html.Append("</tr>\n");
            }
        }
        else
        {
            html.Append("<table class=\"diff unified\">\n");
            var lines = document.Unified.Text.Split('\n');
            for (var i = 0; i < document.Unified.Lines.Count; i++)
            {
                var info = document.Unified.Lines[i];
                html.Append("<tr class=\"").Append(CssClass(info.Kind)).Append("\"><td class=\"num\">")
                    .Append(NumberCell(info.NumberA)).Append("</td><td class=\"num\">").Append(NumberCell(info.NumberB))
                    .Append("</td><td class=\"code\">").Append(Encode(lines[i][..document.Unified.PrefixLength]));
                Highlighted(html, lines[i][document.Unified.PrefixLength..], info.Spans, info.IsDeletionSide);
                Eol(html, info.OnlyLineEndingDiffers);
                html.Append("</td></tr>\n");
            }
        }

        html.Append("</table>\n</body>\n</html>\n");
        return html.ToString();
    }

    private static void Cell(StringBuilder html, TextLine? line, IReadOnlyList<TextSpan> spans, DiffRow row, bool isLeft)
    {
        if (line is null)
        {
            html.Append("<td class=\"num\"></td><td class=\"code filler\"></td>");
            return;
        }

        html.Append("<td class=\"num\">").Append(line.Number).Append("</td><td class=\"code ")
            .Append(CssClass(row.Kind)).Append("\">");
        Highlighted(html, line.Content, spans, isLeft);
        Eol(html, row.OnlyLineEndingDiffers);
        html.Append("</td>");
    }

    private static void Eol(StringBuilder html, bool onlyLineEnding)
    {
        if (onlyLineEnding)
        {
            html.Append(" <span class=\"eol\">(fin de ligne différente)</span>");
        }
    }

    private static string CssClass(DiffRowKind kind) => kind switch
    {
        DiffRowKind.Added => "added",
        DiffRowKind.Deleted => "deleted",
        DiffRowKind.Modified => "modified",
        DiffRowKind.Ignored => "ignored",
        _ => "equal"
    };

    private static string NumberCell(int number) => number == 0 ? string.Empty : number.ToString(CultureInfo.InvariantCulture);

    private static void Highlighted(StringBuilder html, string content, IReadOnlyList<TextSpan> spans, bool isDeletion)
    {
        var position = 0;
        foreach (var span in spans)
        {
            var start = Math.Clamp(span.Start, position, content.Length);
            var end = Math.Clamp(span.End, start, content.Length);
            html.Append(Encode(content[position..start]));
            html.Append(isDeletion ? "<span class=\"del\">" : "<span class=\"add\">").Append(Encode(content[start..end])).Append("</span>");
            position = end;
        }

        html.Append(Encode(content[position..]));
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
