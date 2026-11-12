# PROMPT CONCEPTION #02 — UI/UX & Habillage visuel

## Rôle

Tu es un développeur C# senior, expert WPF (.NET 8), MVVM et design d'interfaces desktop. Tu maîtrises la bibliothèque **WPF UI (lepoe)**, **MahApps.Metro IconPacks** et **AvalonEdit**.

## Mission

Reprendre l'application **TextAnonymizer** produite suite au prompt précédent (voir ci-dessous) et lui appliquer une couche UI/UX complète, moderne et cohérente, **sans toucher à la logique métier** (Services et Models doivent rester inchangés et leurs tests verts).

## Rappel du contexte

TextAnonymizer est un anonymiseur de texte réversible en WPF (.NET 8, MVVM) : trois zones (Source, Résultat, Table de conversion), matching par mots exacts, génération automatique de valeurs fictives par type étiqueté, table JSON chiffrée (AES + DPAPI), réversibilité avec validation de bijection, aperçu avant application.

## Stack UI imposée

- **WPF UI (lepoe.wpf / Wpf.Ui)** : thème Fluent Windows 11, `FluentWindow`, `NavigationView`, `Snackbar`, `ContentDialog`, `ProgressRing`, thème clair/sombre avec bascule.
- **MahApps.Metro IconPacks** : icônes vectorielles pour tous les boutons.
- **AvalonEdit** : remplace les `RichTextBox` des zones Source et Résultat (recherche intégrée, numéros de ligne, mise en surbrillance de segments via `ColorizeSegment`).

## Exigences UX

### 1. Fenêtre et navigation
- `FluentWindow` avec `NavigationView` en colonne latérale : pages « Anonymisation », « Table de conversion », « Paramètres ».
- Bascule thème clair/sombre (persistée dans les paramètres utilisateur).
- Titre de fenêtre + état courant visible (ex: badge « Table chargée : 12 entrées »).

### 2. Page « Anonymisation » (trois zones)
- **Source** et **Résultat** : `AvalonEdit` côte à côte, avec boutons d'outil dédiés :
  - Coller (Ctrl+V via bouton), Vider, Copier (texte brut uniquement), Copier + toast.
- **Surlignage** : chaque segment remplacé est surligné dans le Résultat avec une **couleur par type d'entité** (Nom, Ville, Email, Tel, Autre) ; tooltip au survol affichant la valeur d'origine.
- **Légende** des couleurs par type, affichée sous le Résultat.
- Barre d'actions : `Anonymiser` (bouton accent), `Rétablir`, `Générer automatiquement` (visible si l'option est activée), `Aperçu`.
- Après une action : **Snackbar** de confirmation (« 14 remplacements appliqués », « Texte copié », « Texte original rétabli »).
- Pendant la génération automatique : `ProgressRing` et désactivation des boutons.

### 3. Page « Table de conversion »
- Grille éditable des entrées : colonnes Original, Remplaçant, Type (combobox), Auto (badge), statut.
- Recherche/filtre instantané, tri par colonne.
- Suppression avec confirmation via `ContentDialog`.
- Import/Export JSON (l'export chiffre, l'import déchiffre ; message d'erreur clair si fichier invalide).
- Badge de validation : « Bijection valide » (vert) ou liste des doublons détectés (rouge) — jamais d'anonymisation possible avec une table invalide.

### 4. Page « Paramètres »
- Stratégie de matching (Strict / Ignorer casse / Ignorer casse + accents) — sélecteur radio.
- Activation/désactivation de la génération automatique.
- Emplacement du fichier de table chiffrée (affiché, non éditable en clair).
- Thème clair/sombre.

### 5. Aperçu avant application
- `ContentDialog` listant les remplacements proposés (original → remplaçant, type), modifiable ligne par ligne, boutons « Tout valider », « Valider la sélection », « Annuler ».

### 6. Accessibilité et ergonomie
- Raccourcis clavier : Ctrl+O (charger table), Ctrl+S (enregistrer), Ctrl+Enter (Anonymiser), Ctrl+R (Rétablir), Ctrl+D (Copier résultat).
- Tooltips sur tous les boutons icônes.
- États vides explicites : si aucune table chargée, la zone Résultat affiche un guide (« Chargez ou créez une table pour commencer »).
- Avertissement `ContentDialog` si l'utilisateur modifie la table après une anonymisation (risque de perte de réversibilité).

## Contraintes techniques

- **MVVM strict** : aucun style de logique métier dans le code-behind ; ViewModels inchangés dans leur responsabilité, seule la couche visuelle évolue.
- Adapter les ViewModels existants au strict minimum (nouvelles propriétés d'affichage : thème, légende, états) via `CommunityToolkit.Mvvm`.
- Le surlignage AvalonEdit se base sur les **segments (index, longueur, entrée)** déjà retournés par `ConversionService` — ne pas recalculer dans la vue.
- `dotnet build` sans avertissement ; les tests unitaires existants restent verts sans modification.
- Nettoyer tout style/contrôle standard devenu inutile après migration vers WPF UI.

## Critères d'acceptation

1. L'app démarre avec le look Fluent Windows 11, thème clair ou sombre selon paramètre persisté.
2. Les segments remplacés sont surlignés par type de couleur avec tooltips d'origine, et la légende est visible.
3. Snackbar après chaque action utilisateur ; aucun changement visuel sans feedback.
4. Table invalide → anonymisation bloquée avec message listant les doublons.
5. Tous les raccourcis clavier fonctionnels ; copy/paste toujours en texte brut.
6. Aucune régression fonctionnelle par rapport au prompt précédent (tests verts).
7. `dotnet build` propre, avertissement zéro.

## Livrables attendus

1. Fichiers XAML mis à jour (FluentWindow, 3 pages, styles WPF UI).
2. `ThemeService` (clair/sombre, persistance) et mapping couleurs de surlignage par type.
3. Intégration IconPacks sur tous les boutons.
4. Migration des zones texte vers AvalonEdit avec `ColorizeSegment` par type d'entité.
5. README mis à jour : nouveaux paquets NuGet (Wpf.Ui, MahApps.Metro.IconPacks, AvalonEdit), raccourcis clavier, capture de l'arborescence finale.

Procède dans cet ordre : NuGet et ThemeService → FluentWindow/NavigationView → page Anonymisation (AvalonEdit + surlignage) → page Table → page Paramètres → ContentDialog/Snackbar/états vides → nettoyage.