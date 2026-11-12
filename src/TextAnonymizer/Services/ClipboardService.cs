using System.Windows;

namespace TextAnonymizer.Services;

public sealed class ClipboardService : IClipboardService
{
    public string? GetText() =>
        Clipboard.ContainsText(TextDataFormat.UnicodeText) ? Clipboard.GetText(TextDataFormat.UnicodeText) : null;

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Clipboard.SetText(text, TextDataFormat.UnicodeText);
    }
}
