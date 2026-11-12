using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextAnonymizer.Models;

namespace TextAnonymizer.Services;

/// <summary>
/// Persistance JSON. Le fichier chiffré a le format : magic « TAEN » | version | nonce (12) | tag (16) | données (AES-256-GCM).
/// La clé AES est générée une fois puis stockée à côté de la table, protégée par DPAPI (compte Windows courant).
/// </summary>
public sealed class TableStorageService : ITableStorageService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const byte FormatVersion = 1;
    private static readonly byte[] Magic = "TAEN"u8.ToArray();
    private static readonly int HeaderSize = Magic.Length + 1 + NonceSize + TagSize;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _keyPath;

    public TableStorageService()
        : this(Path.Combine(AppContext.BaseDirectory, "Data"))
    {
    }

    /// <param name="dataDirectory">Dossier contenant <c>table.enc</c> (par défaut) et <c>table.key</c>.</param>
    public TableStorageService(string dataDirectory)
    {
        DefaultPath = Path.Combine(dataDirectory, "table.enc");
        _keyPath = Path.Combine(dataDirectory, "table.key");
    }

    public string DefaultPath { get; }

    public ConversionTable Load(string path)
    {
        byte[] file;
        try
        {
            file = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TableStorageException($"Impossible de lire le fichier « {path} » : {ex.Message}", ex);
        }

        if (file.Length < HeaderSize || !file.AsSpan(0, Magic.Length).SequenceEqual(Magic) || file[Magic.Length] != FormatVersion)
        {
            throw new TableStorageException("Ce fichier n'est pas une table chiffrée valide (utilisez « Importer » pour un fichier JSON en clair).");
        }

        var offset = Magic.Length + 1;
        var nonce = file.AsSpan(offset, NonceSize);
        var tag = file.AsSpan(offset + NonceSize, TagSize);
        var cipher = file.AsSpan(HeaderSize);
        var plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(GetKey(createIfMissing: false), TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (CryptographicException ex)
        {
            throw new TableStorageException(
                "Impossible de déchiffrer la table : fichier altéré, ou chiffré avec une autre clé / un autre compte Windows.", ex);
        }

        return Deserialize(plain);
    }

    public void Save(ConversionTable table, string path)
    {
        ArgumentNullException.ThrowIfNull(table);

        var plain = JsonSerializer.SerializeToUtf8Bytes(ToDto(table), JsonOptions);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];

        using (var aes = new AesGcm(GetKey(createIfMissing: true), TagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        var output = new byte[HeaderSize + cipher.Length];
        Magic.CopyTo(output, 0);
        output[Magic.Length] = FormatVersion;
        nonce.CopyTo(output, Magic.Length + 1);
        tag.CopyTo(output, Magic.Length + 1 + NonceSize);
        cipher.CopyTo(output, HeaderSize);

        WriteAtomically(path, output);
    }

    public ConversionTable Import(string path)
    {
        try
        {
            return Deserialize(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TableStorageException($"Impossible de lire le fichier « {path} » : {ex.Message}", ex);
        }
    }

    public void Export(ConversionTable table, string path)
    {
        ArgumentNullException.ThrowIfNull(table);
        WriteAtomically(path, JsonSerializer.SerializeToUtf8Bytes(ToDto(table), JsonOptions));
    }

    private byte[] GetKey(bool createIfMissing)
    {
        try
        {
            if (File.Exists(_keyPath))
            {
                return ProtectedData.Unprotect(File.ReadAllBytes(_keyPath), null, DataProtectionScope.CurrentUser);
            }

            if (!createIfMissing)
            {
                throw new TableStorageException($"Clé de chiffrement introuvable (« {_keyPath} ») : la table ne peut pas être déchiffrée.");
            }

            var key = RandomNumberGenerator.GetBytes(KeySize);
            WriteAtomically(_keyPath, ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser));
            return key;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new TableStorageException($"Clé de chiffrement inaccessible : {ex.Message}", ex);
        }
    }

    private static void WriteAtomically(string path, byte[] content)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = path + ".tmp";
            File.WriteAllBytes(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TableStorageException($"Impossible d'écrire le fichier « {path} » : {ex.Message}", ex);
        }
    }

    private static ConversionTable Deserialize(ReadOnlySpan<byte> json)
    {
        TableDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<TableDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new TableStorageException($"Le contenu de la table est invalide : {ex.Message}", ex);
        }

        if (dto?.Entries is null)
        {
            throw new TableStorageException("Le contenu de la table est invalide : aucune liste d'entrées.");
        }

        var table = new ConversionTable { Strategy = dto.Strategy };
        foreach (var e in dto.Entries)
        {
            table.Entries.Add(new ConversionEntry
            {
                Original = e.Original ?? string.Empty,
                Replacement = e.Replacement ?? string.Empty,
                EntityType = e.EntityType,
                IsAutoGenerated = e.IsAutoGenerated
            });
        }

        return table;
    }

    private static TableDto ToDto(ConversionTable table) => new()
    {
        Strategy = table.Strategy,
        Entries = table.Entries
            .Select(e => new EntryDto
            {
                Original = e.Original,
                Replacement = e.Replacement,
                EntityType = e.EntityType,
                IsAutoGenerated = e.IsAutoGenerated
            })
            .ToList()
    };

    private sealed class TableDto
    {
        public int Version { get; set; } = 1;

        public MatchingStrategy Strategy { get; set; }

        public List<EntryDto>? Entries { get; set; }
    }

    private sealed class EntryDto
    {
        public string? Original { get; set; }

        public string? Replacement { get; set; }

        public EntityType EntityType { get; set; }

        public bool IsAutoGenerated { get; set; }
    }
}
