# TextAnonymizer

Anonymiseur de texte **réversible** (WPF, .NET 8, MVVM) : une table de conversion bijective remplace des mots exacts par des valeurs fictives, et permet de rétablir le texte d'origine. Interface Fluent (Windows 11), thème clair/sombre.

## Compiler, tester, lancer

Prérequis : Windows, SDK .NET 8 ou supérieur.

```powershell
dotnet build TextAnonymizer.sln
dotnet test  TextAnonymizer.sln
dotnet run --project src/TextAnonymizer
```

Le fichier `nuget.config` limite la restauration à nuget.org.

## Paquets NuGet

| Paquet | Rôle |
|---|---|
| `WPF-UI` (Wpf.Ui) 4.3.0 | `FluentWindow`, `NavigationView`, `Snackbar`, `ContentDialog`, `ProgressRing`, thèmes clair/sombre |
| `MahApps.Metro.IconPacks.Material` 6.2.1 | icônes vectorielles des boutons |
| `AvalonEdit` 6.3.1.120 | zones Source et Résultat : numéros de ligne, recherche (Ctrl+F), colorisation des segments |
| `Markdig` 1.5.1 | analyse Markdown du Moteur de rendu |
| `CommunityToolkit.Mvvm` 8.4.0 | `ObservableObject`, commandes |
| `System.Security.Cryptography.ProtectedData` | protection DPAPI de la clé AES |

Tests : `xunit`, `Microsoft.NET.Test.Sdk`.

## Utilisation

Navigation latérale : **Anonymisation**, **Table de conversion**, **Moteur de rendu**, **Comparateur de fichiers**, **Paramètres**. Le badge de la barre de titre indique l'état de la table (« Table chargée : N entrées », vert si la bijection est valide, rouge sinon).

1. *Table de conversion* : ajouter des entrées (original, remplaçant, type). Laisser le remplaçant vide pour le faire générer.
2. *Paramètres* : activer la génération automatique ; le bouton **Générer automatiquement** apparaît alors sur la page Anonymisation. Un aperçu modifiable s'ouvre (**Tout valider**, **Valider la sélection** = lignes cochées, **Annuler**).
3. *Anonymisation* : coller le texte dans **Source**, **Anonymiser**. Le **Résultat** surligne chaque segment remplacé (couleur par type, légende sous la zone, tooltip = valeur d'origine).
4. **Rétablir** remet le texte d'origine dans **Source**. Un texte anonymisé collé dans **Résultat** est rétabli via la table.
5. Chaque action est confirmée par un toast (snackbar).

### Moteur de rendu

Collez du texte dans l'éditeur de gauche : le format est détecté (JSON, Markdown, HTML, PHP) et son rendu s'affiche à droite, 300 ms après la dernière frappe. Le sélecteur **Format** permet de le forcer. Tout est traité localement.

- **JSON** : arbre repliable avec types, onglet « Formaté », erreurs expliquées avec ligne et colonne.
- **Markdown** : rendu complet (tableaux, blocs de code colorés, liens, images, listes de tâches).
- **HTML** : aperçu avec scripts, gestionnaires d'événements, cadres et objets supprimés, navigation bloquée ; onglet « Code source ».
- **PHP** : jamais exécuté ; onglet « Code » coloré, onglet « Aperçu » si le fichier contient du HTML.
- Boutons Coller, Copier, Effacer, Télécharger et bascule du thème clair/sombre.

### Comparateur de fichiers

Deux panneaux éditables, **A (original)** et **B (nouvelle version)** : saisir, coller, ouvrir un fichier ou le glisser-déposer (deux fichiers déposés d'un coup remplissent A et B et lancent la comparaison). **Comparer** (Ctrl+Entrée ou F5) passe en vue résultat ; **Modifier** revient à la saisie sans perdre le texte.

- **Algorithme** : Myers O(ND) en espace linéaire (sans dépendance), puis appariement des lignes proches en « modifiées », avec le détail au mot et, pour un mot remplacé, au caractère. Calcul en arrière-plan, annulé si une nouvelle comparaison est lancée.
- **Affichage côte à côte** (lignes alignées, lignes de remplissage, vrais numéros de A et de B) ou **unifié** (`-` / `+`, deux colonnes de numéros). Vert = ajout, rouge = suppression, jaune orangé = modification. **Synchroniser le défilement** est une bascule.
- **Navigation** : flèches ou F7 / Maj+F7 (Alt+↓ / Alt+↑), « Différence 3 / 12 », bloc courant encadré.
- **Options** indépendantes : casse, espaces de début et fin, espaces multiples, tabulations (= un espace), lignes vides, fins de ligne CRLF/LF (cochée par défaut), insensible aux espaces. Elles changent seulement la comparaison : le texte affiché et exporté reste l'original. En vue résultat, chaque option relance la comparaison.
- **Statistiques** : identiques, ajoutées, supprimées, modifiées, similarité (2 × identiques ÷ (lignes de A + lignes de B)), nombre de différences (blocs).
- **Mode** Auto / Texte brut / JSON / XML / Markdown (Auto : extension du fichier, sinon contenu). En JSON et XML, le panneau **Changements structurels** (replié par défaut, résumé visible) liste les propriétés, éléments et attributs ajoutés, supprimés ou modifiés, par chemin (`$.client.nom`, `/config/item[2]/@id`). L'indentation, les espaces, l'écriture des nombres et des échappements, l'ordre des attributs XML et (option cochée par défaut) l'ordre des propriétés JSON n'y comptent pas. **Comparer la forme normalisée** applique aussi cela à la vue ligne à ligne. En Markdown, comparaison par section (titres).
- **Exports** : texte (.txt), HTML autonome en couleurs (dans le mode d'affichage courant), patch unifié (.patch / .diff, applicable par `patch` ou `git apply`), copie dans le presse-papiers.
- **Fichiers** : ouverts en lecture seule et jamais modifiés (un export vers l'un d'eux est refusé). Encodage détecté (BOM UTF-8/16/32, UTF-16 sans BOM, UTF-8, sinon Windows-1252) et affiché avec le type de fin de ligne. Fichiers binaires refusés, limite de 200 Mo par fichier, erreurs de lecture expliquées.

Règles de matching :
- Mots entiers uniquement (« Paris » ne correspond pas à « Parisien ») ; la correspondance la plus longue l'emporte.
- Stratégie de la table : `Strict`, ignorer la casse, ignorer casse + accents.
- Avec les stratégies insensibles, **Rétablir** restitue le texte source à l'identique tant que le Résultat n'a pas été modifié ; sinon il inverse la table.
- Une table invalide (doublon d'original ou de remplaçant, champ vide) bloque l'anonymisation et affiche la liste des problèmes.
- Modifier la table après une anonymisation demande confirmation (commandes) ou déclenche un avertissement et un bandeau (édition dans la grille).

## Raccourcis clavier

| Raccourci | Action |
|---|---|
| Ctrl+O | Charger une table |
| Ctrl+S | Enregistrer la table (chiffrée) |
| Ctrl+Entrée | Anonymiser |
| Ctrl+R | Rétablir |
| Ctrl+D | Copier le résultat (texte brut) |
| Ctrl+F | Rechercher dans la zone de texte active |
| Ctrl+Entrée ou F5 | Comparateur : comparer |
| F7 / Maj+F7 | Comparateur : différence suivante / précédente |

Toutes les copies (boutons et Ctrl+C dans les éditeurs) placent du **texte brut** dans le presse-papiers.

## Où sont stockées les données ?

Dans le dossier de l'application, sous `Data\` :

| Fichier | Contenu |
|---|---|
| `table.enc` | table JSON chiffrée en AES-256-GCM |
| `table.key` | clé AES, protégée par DPAPI (portée utilisateur Windows courant) |
| `settings.json` | thème et génération automatique (non sensible, en clair) |

Avec `dotnet run`, c'est `src\TextAnonymizer\bin\<Config>\net8.0-windows\Data\`. La stratégie de matching est enregistrée dans la table elle-même.

- **Importer** ajoute à la table les entrées d'un fichier chiffré (refusé si cela créerait des doublons) ; **Exporter** écrit une copie chiffrée. Ces fichiers ne se déchiffrent qu'avec le même `table.key`, c'est-à-dire le même compte Windows et le même dossier de données.
- Sans `table.key` ou avec un autre compte Windows, une table est perdue : conservez une copie de `Data\`.
- Le dossier de l'application doit être accessible en écriture.

## Arborescence

```
TextAnonymizer.sln
nuget.config
src/TextAnonymizer/
├── App.xaml(.cs)                  thèmes WPF UI, composition des services
├── Themes/AppResources.xaml       convertisseurs et styles partagés (aussi chargés par les tests d'interface)
├── Models/                        FormatDetector, JsonInspector, HtmlSanitizer, ViewerAnalysis (Moteur de rendu),
│                                  ConversionEntry, ConversionTable, EntityType, MatchingStrategy,
│   │                              ReplacementResult, validation de bijection, normalisation
│   └── Diff/                      comparateur : MyersDiff, LineDiffer, IntraLineDiff, DiffDocument, DiffExporter,
│                                  StructuralDiff (JSON, XML, Markdown), TextFileReader, DiffComparison
├── Services/                      Conversion, Generator, TableStorage (AES+DPAPI), Clipboard,
│                                  Dialog (snackbar + ContentDialog), Settings, Theme
├── ViewModels/                    MainViewModel, TableViewModel, PreviewViewModel, SettingsViewModel, ViewerViewModel,
│                                  CompareViewModel, ComparePane
├── Converters/                    BoolToVisibility, EntityTypeToBrush, EnumToBool, EntityColors
└── Views/
    ├── MainWindow.xaml            FluentWindow + NavigationView + SnackbarPresenter + ContentDialogHost
    ├── PageProvider.cs            pages du NavigationView
    ├── Pages/                     AnonymizationPage, TablePage, ViewerPage, ComparePage, SettingsPage
    ├── Rendering/                 MarkdownFlowDocumentBuilder, JsonTreeBuilder, SyntaxPalette,
    │                              DiffEditorDecorator (fonds, surlignage, bloc courant, numéros), DiffColors
    ├── SourceView.xaml            éditeur AvalonEdit (source)
    ├── ResultView.xaml            éditeur AvalonEdit (résultat) + légende + état vide
    ├── PreviewDialogContent.xaml  contenu du ContentDialog d'aperçu
    └── Behaviors/AvalonEditBehavior.cs   texte lié, colorisation par segment, tooltip, copie en texte brut
tests/TextAnonymizer.Tests/        xUnit : conversion, génération, bijection, stockage chiffré, paramètres, aperçu,
                                   moteur de rendu, comparateur ; WpfTestHost = une Application partagée par les tests d'interface
```

Le surlignage lit les segments (index, longueur, entrée) du `ReplacementResult` calculé par `ConversionService` ; la vue ne recalcule rien. Aucun code-behind ne contient de logique métier.
