namespace TextAnonymizer.Models;

/// <summary>Format du texte du visualiseur ; <see cref="Auto"/> n'est jamais un format détecté, seulement un choix de l'utilisateur.</summary>
public enum ViewerFormat
{
    Auto,
    Json,
    Markdown,
    Html,
    Php
}
