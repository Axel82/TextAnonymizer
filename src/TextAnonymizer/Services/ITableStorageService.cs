using TextAnonymizer.Models;

namespace TextAnonymizer.Services;

/// <summary>Erreur de lecture/écriture d'une table, avec un message destiné à l'utilisateur.</summary>
public sealed class TableStorageException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface ITableStorageService
{
    /// <summary>Emplacement par défaut de la table chiffrée.</summary>
    string DefaultPath { get; }

    /// <summary>Charge une table chiffrée (AES-GCM, clé protégée par DPAPI).</summary>
    ConversionTable Load(string path);

    /// <summary>Enregistre la table chiffrée.</summary>
    void Save(ConversionTable table, string path);

    /// <summary>Importe un fichier JSON en clair.</summary>
    ConversionTable Import(string path);

    /// <summary>Exporte la table en JSON en clair.</summary>
    void Export(ConversionTable table, string path);
}
