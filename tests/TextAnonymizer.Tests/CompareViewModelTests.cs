using System.IO;
using System.Text;
using TextAnonymizer.Models.Diff;
using TextAnonymizer.Services;
using TextAnonymizer.ViewModels;

namespace TextAnonymizer.Tests;

public sealed class CompareViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TextAnonymizerTests", Guid.NewGuid().ToString("N"));
    private readonly FakeDialogs _dialogs = new();
    private readonly WpfTestHost.FakeClipboard _clipboard = new();
    private readonly CompareViewModel _viewModel;

    public CompareViewModelTests()
    {
        Directory.CreateDirectory(_directory);
        _viewModel = new CompareViewModel(_clipboard, _dialogs, new WpfTestHost.FakeTheme());
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        Directory.Delete(_directory, true);
    }

    private sealed class FakeDialogs : IDialogService
    {
        public List<(string Message, NotificationKind Kind)> Notifications { get; } = [];

        public List<string> Errors { get; } = [];

        public string? SavePath { get; set; }

        public string? OpenPath { get; set; }

        public void Notify(string message, NotificationKind kind = NotificationKind.Success) => Notifications.Add((message, kind));

        public void ShowInfo(string message, string title = "Information")
        {
        }

        public void ShowError(string message, string title = "Erreur") => Errors.Add(message);

        public void ShowWarning(string message, string title = "Attention")
        {
        }

        public bool Confirm(string message, string title = "Confirmation") => true;

        public string? PickOpenFile(string title, string filter, string? initialDirectory = null) => OpenPath;

        public string? PickSaveFile(string title, string filter, string? initialDirectory = null, string? fileName = null) => SavePath;

        public bool ShowPreview(PreviewViewModel preview) => false;
    }

    private string WriteFile(string name, string content, Encoding? encoding = null)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
        return path;
    }

    [Fact]
    public async Task Compare_SwitchesToResult_AndSelectsFirstDifference()
    {
        _viewModel.A.SetText("a\nb\nc\nd");
        _viewModel.B.SetText("a\nB\nc\nd\ne");

        await _viewModel.CompareCommand.ExecuteAsync(null);

        Assert.False(_viewModel.IsEditing);
        Assert.True(_viewModel.ShowSideBySide);
        Assert.Equal(2, _viewModel.Result.Blocks.Count);
        Assert.Equal(0, _viewModel.CurrentBlockIndex);
        Assert.Equal("Différence 1 / 2", _viewModel.NavigationLabel);
        Assert.False(_viewModel.IsBusy);
    }

    [Fact]
    public async Task Navigation_MovesAndWraps()
    {
        _viewModel.A.SetText("1\n2\n3\n4\n5\n6\n7\n8\n9");
        _viewModel.B.SetText("1\nX\n3\n4\nY\n6\n7\nZ\n9");
        await _viewModel.CompareCommand.ExecuteAsync(null);

        _viewModel.NextDifferenceCommand.Execute(null);
        _viewModel.NextDifferenceCommand.Execute(null);
        Assert.Equal("Différence 3 / 3", _viewModel.NavigationLabel);

        _viewModel.NextDifferenceCommand.Execute(null);
        Assert.Equal(0, _viewModel.CurrentBlockIndex);

        _viewModel.PreviousDifferenceCommand.Execute(null);
        Assert.Equal(2, _viewModel.CurrentBlockIndex);
        Assert.Same(_viewModel.Result.Blocks[2], _viewModel.CurrentBlock);
    }

    [Fact]
    public async Task IdenticalContents_AreReported_AndNavigationIsDisabled()
    {
        _viewModel.A.SetText("même\ntexte");
        _viewModel.B.SetText("même\ntexte");

        await _viewModel.CompareCommand.ExecuteAsync(null);

        Assert.Equal("Aucune différence", _viewModel.NavigationLabel);
        Assert.False(_viewModel.NextDifferenceCommand.CanExecute(null));
        Assert.Contains(_dialogs.Notifications, n => n.Message.Contains("identiques"));
        Assert.StartsWith("100", _viewModel.SimilarityLabel);
        Assert.EndsWith("%", _viewModel.SimilarityLabel);
    }

    [Fact]
    public async Task EmptyContents_AreHandled()
    {
        await _viewModel.CompareCommand.ExecuteAsync(null);

        Assert.True(_viewModel.HasResult);
        Assert.True(_viewModel.Result.IsIdentical);
        Assert.Contains(_dialogs.Notifications, n => n.Message.Contains("vides"));
    }

    [Fact]
    public async Task ChangingAnOption_InResultMode_RecomputesTheComparison()
    {
        _viewModel.A.SetText("Bonjour");
        _viewModel.B.SetText("BONJOUR");
        await _viewModel.CompareCommand.ExecuteAsync(null);
        Assert.False(_viewModel.Result.IsIdentical);

        _viewModel.IgnoreCase = true;
        await WaitUntil(() => _viewModel.Result.IsIdentical);

        Assert.True(_viewModel.Result.Options.IgnoreCase);
    }

    [Fact]
    public async Task Edit_ReturnsToInput_KeepingTexts()
    {
        _viewModel.A.SetText("a");
        _viewModel.B.SetText("b");
        await _viewModel.CompareCommand.ExecuteAsync(null);

        _viewModel.EditCommand.Execute(null);

        Assert.True(_viewModel.IsEditing);
        Assert.False(_viewModel.ShowSideBySide);
        Assert.Equal("a", _viewModel.A.Text);
    }

    [Fact]
    public async Task Swap_ExchangesContentsAndFiles()
    {
        var path = WriteFile("v1.txt", "un\n");
        await _viewModel.LoadFileAsync(_viewModel.A, path);
        _viewModel.B.SetText("deux\n");

        await _viewModel.SwapCommand.ExecuteAsync(null);

        Assert.Equal("deux\n", _viewModel.A.Text);
        Assert.Null(_viewModel.A.File);
        Assert.Equal("un\n", _viewModel.B.Text);
        Assert.Equal("v1.txt", _viewModel.B.DisplayName);
    }

    [Fact]
    public async Task Clear_EmptiesBothPanes_AndReturnsToInput()
    {
        _viewModel.A.SetText("a");
        _viewModel.B.SetText("b");
        await _viewModel.CompareCommand.ExecuteAsync(null);

        _viewModel.ClearCommand.Execute(null);

        Assert.True(_viewModel.A.IsEmpty);
        Assert.True(_viewModel.B.IsEmpty);
        Assert.True(_viewModel.IsEditing);
        Assert.False(_viewModel.HasResult);
        Assert.False(_viewModel.ExportTextCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadFile_ShowsEncodingAndLineEnding_AndMarksLaterEdits()
    {
        var path = WriteFile("notes.txt", "Élève\r\nfin\r\n", new UTF8Encoding(true));

        await _viewModel.LoadFileAsync(_viewModel.A, path);

        Assert.Equal("Élève\r\nfin\r\n", _viewModel.A.Text);
        Assert.Equal("notes.txt · UTF-8 (BOM) · CRLF (Windows)", _viewModel.A.Description);

        _viewModel.A.Text += "ajout";
        Assert.EndsWith("· modifié", _viewModel.A.Description);
    }

    [Fact]
    public async Task LoadFile_RejectsBinaryFiles_WithAReadableError()
    {
        var path = Path.Combine(_directory, "data.bin");
        File.WriteAllBytes(path, [1, 2, 0, 3, 4]);

        await _viewModel.LoadFileAsync(_viewModel.A, path);

        Assert.True(_viewModel.A.IsEmpty);
        Assert.Contains(_dialogs.Errors, e => e.Contains("binaire"));
    }

    [Fact]
    public async Task DroppingTwoFiles_FillsBothPanes_AndCompares()
    {
        var a = WriteFile("a.txt", "x\ny\n");
        var b = WriteFile("b.txt", "x\nz\n");

        await _viewModel.DropFilesAsync(_viewModel.A, [a, b]);

        Assert.Equal("a.txt", _viewModel.A.DisplayName);
        Assert.Equal("b.txt", _viewModel.B.DisplayName);
        Assert.False(_viewModel.IsEditing);
        Assert.Single(_viewModel.Result.Blocks);
    }

    [Fact]
    public async Task Exports_WriteTheChosenFile_AndNeverTheOriginals()
    {
        var original = WriteFile("original.txt", "a\nb\n");
        await _viewModel.LoadFileAsync(_viewModel.A, original);
        _viewModel.B.SetText("a\nc\n");
        await _viewModel.CompareCommand.ExecuteAsync(null);

        _dialogs.SavePath = Path.Combine(_directory, "sortie.patch");
        await _viewModel.ExportPatchCommand.ExecuteAsync(null);
        Assert.Contains("-b\n+c\n", File.ReadAllText(_dialogs.SavePath));

        _dialogs.SavePath = Path.Combine(_directory, "sortie.html");
        await _viewModel.ExportHtmlCommand.ExecuteAsync(null);
        Assert.StartsWith("<!DOCTYPE html>", File.ReadAllText(_dialogs.SavePath));

        _dialogs.SavePath = Path.Combine(_directory, "sortie.txt");
        await _viewModel.ExportTextCommand.ExecuteAsync(null);
        Assert.Contains("original.txt", File.ReadAllText(_dialogs.SavePath));

        _dialogs.SavePath = original;
        await _viewModel.ExportTextCommand.ExecuteAsync(null);
        Assert.Equal("a\nb\n", File.ReadAllText(original));
        Assert.Contains(_dialogs.Errors, e => e.Contains("jamais modifiés"));
    }

    [Fact]
    public async Task CopyResult_PutsTheTextExportInTheClipboard()
    {
        _viewModel.A.SetText("a");
        _viewModel.B.SetText("b");
        await _viewModel.CompareCommand.ExecuteAsync(null);

        _viewModel.CopyResultCommand.Execute(null);

        Assert.Contains("- a", _clipboard.Text);
        Assert.Contains("+ b", _clipboard.Text);
    }

    [Fact]
    public async Task Paste_FillsThePane()
    {
        _clipboard.Text = "collé";

        await _viewModel.PasteCommand.ExecuteAsync(_viewModel.B);

        Assert.Equal("collé", _viewModel.B.Text);
        Assert.Equal("Texte saisi", _viewModel.B.Description);
    }

    [Fact]
    public async Task JsonFormat_IsDetected_AndStructuralChangesListed()
    {
        _viewModel.A.SetText("{\"a\": 1, \"b\": 2}");
        _viewModel.B.SetText("{\"b\": 2, \"a\": 3}");

        await _viewModel.CompareCommand.ExecuteAsync(null);

        Assert.Equal("Format détecté : JSON", _viewModel.FormatLabel);
        Assert.True(_viewModel.ShowStructural);
        var change = Assert.Single(_viewModel.Structural!.Changes);
        Assert.Equal("$.a", change.Path);
    }

    [Fact]
    public async Task UnifiedMode_IsAvailable()
    {
        _viewModel.A.SetText("a");
        _viewModel.B.SetText("b");
        await _viewModel.CompareCommand.ExecuteAsync(null);

        _viewModel.SelectedDisplayMode = _viewModel.DisplayModes[1];

        Assert.True(_viewModel.ShowUnified);
        Assert.False(_viewModel.ShowSideBySide);
        Assert.Equal("- a\n+ b", _viewModel.Document.Unified.Text);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(25);
        }

        Assert.True(condition());
    }
}
