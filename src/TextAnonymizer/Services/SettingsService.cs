using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TextAnonymizer.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public SettingsService()
        : this(Path.Combine(AppContext.BaseDirectory, "Data"))
    {
    }

    /// <param name="dataDirectory">Dossier contenant <c>settings.json</c>.</param>
    public SettingsService(string dataDirectory)
    {
        _path = Path.Combine(dataDirectory, "settings.json");
        Current = Read();
    }

    public AppSettings Current { get; }

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private AppSettings Read()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }
}
