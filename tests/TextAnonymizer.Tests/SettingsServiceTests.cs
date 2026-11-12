using TextAnonymizer.Services;

namespace TextAnonymizer.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TextAnonymizerSettings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Defaults_WhenFileIsMissing()
    {
        var settings = new SettingsService(_directory).Current;

        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.False(settings.AutoGenerateEnabled);
    }

    [Fact]
    public void SavedSettings_ArePersistedAcrossInstances()
    {
        var first = new SettingsService(_directory);
        first.Current.Theme = AppTheme.Dark;
        first.Current.AutoGenerateEnabled = true;

        Assert.True(first.Save());
        var reloaded = new SettingsService(_directory).Current;

        Assert.Equal(AppTheme.Dark, reloaded.Theme);
        Assert.True(reloaded.AutoGenerateEnabled);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ not json");

        var settings = new SettingsService(_directory).Current;

        Assert.Equal(AppTheme.Light, settings.Theme);
    }
}
