# Changelog

Historique des changements de TextAnonymizer, du plus récent au plus ancien.
Chaque version correspond à un prompt de conception (voir `docs/`).

## [0.4.0] — Prompt Comparateur de fichiers #01

Nouvelle page **Comparateur de fichiers** dans le menu latéral, entre « Moteur de rendu » et « Paramètres ». Aucune nouvelle dépendance : l'algorithme de diff, la lecture des encodages et les comparaisons JSON / XML / Markdown sont écrits dans le projet ou reposent sur .NET 8. Les fonctionnalités existantes ne changent pas.

### Ajouté
- **Saisie** : panneaux A (original) et B (nouvelle version) éditables, boutons Ouvrir et Coller par panneau, glisser-déposer (un ou deux fichiers). **Comparer** (Ctrl+Entrée, F5) passe en vue résultat ; **Modifier** revient à la saisie ; **Inverser A et B** ; **Effacer** ; **Synchroniser le défilement** (bascule).
- **Algorithme** (`Models/Diff`) : Myers O(ND) en espace linéaire, sur une pile explicite, avec retrait du préfixe et du suffixe communs, éléments sans équivalent écartés d'emblée et découpage approché au-delà de 4 096 différences dans une zone (comme GNU diff). Les lignes supprimées et ajoutées d'un même bloc sont appariées en « modifiées » quand elles se ressemblent (au moins 50 %), par programmation dynamique ordonnée, puis détaillées au mot et au caractère.
- **Côte à côte** : lignes alignées avec lignes de remplissage, vrais numéros de A et de B dans une marge dédiée, fond vert / rouge / jaune orangé, parties modifiées surlignées, mention « fin de ligne différente ». **Unifié** : préfixes `-` et `+`, deux colonnes de numéros. Éditeurs AvalonEdit virtualisés, en lecture seule, avec recherche (Ctrl+F). Couleurs claires et sombres.
- **Navigation** : précédente / suivante (boutons, F7 / Maj+F7, Alt+↑ / Alt+↓, avec retour au début), « Différence 3 / 12 », bloc courant encadré et amené au tiers supérieur de la vue.
- **Options indépendantes** : casse, espaces de début et fin, espaces multiples, tabulations, lignes vides (affichées sans couleur, hors statistiques), fins de ligne CRLF/LF (cochée par défaut), insensible aux espaces. Elles n'agissent que sur la clé de comparaison et sur le surlignage. En vue résultat, chaque changement relance la comparaison.
- **Statistiques** : identiques, ajoutées, supprimées, modifiées, similarité, nombre de différences (blocs).
- **Formats** : sélecteur Auto / Texte brut / JSON / XML / Markdown (Auto : extension, sinon contenu ; texte brut si A et B divergent). JSON : changements par chemin (ajout, suppression, valeur, type, ordre si demandé) ; `42` = `42.0`, `"\u0061"` = `"a"` ; « Ignorer l'ordre des propriétés » coché par défaut ; erreurs reprises de `JsonInspector`. XML : éléments, attributs (ordre ignoré), textes ; commentaires ignorés ; DTD ignorée et aucune ressource externe résolue. Markdown : sections par titres (hors blocs de code). Option « Comparer la forme normalisée » pour la vue ligne à ligne.
- **Exports** : texte, HTML autonome en couleurs (mode courant), patch unifié standard (`@@ -a,b +c,d @@`, 3 lignes de contexte, `\ No newline at end of file`), copie dans le presse-papiers. Un export vers l'un des fichiers comparés est refusé. Un avertissement s'affiche quand des options d'ignorance ou la forme normalisée rendent le patch inexact.
- **Fichiers** : lecture seule partagée (`FileShare.ReadWrite | Delete`), jamais écrits. Encodage détecté et affiché (BOM UTF-8/16/32, UTF-16 sans BOM, UTF-8 strict, sinon Windows-1252) avec le type de fin de ligne ; « modifié » si le texte est retouché après chargement. Fichiers binaires refusés ; limite de 200 Mo ; erreurs de lecture en français. Lecture et calcul hors du fil de l'interface (`ProgressRing`).
- Tests (98 → 192) : Myers comparé à une plus longue sous-suite commune de référence sur des séquences aléatoires ; identiques, vides, ajouts, suppressions, modifications, appariement ; 100 000 lignes ; 50 000 lignes toutes différentes ; annulation ; chaque option et leur indépendance ; encodages (7 variantes), binaire, fichier absent, fichier ouvert ailleurs et non modifié ; patch réappliqué à A pour retrouver B ; HTML échappé ; JSON, XML (dont entité externe) et Markdown ; ViewModel (navigation, inversion, effacement, glisser-déposer, exports, refus d'écrire un original) ; test de fumée de la page (deux modes, défilement synchronisé, thème sombre, empilement sous 900 px).

### Modifié
- Les convertisseurs et styles de `App.xaml` sont déplacés, sans changement, dans `Themes/AppResources.xaml`, qu'`App.xaml` fusionne.
- Tests d'interface : une seule `Application` partagée (`WpfTestHost`), chargée avec les vraies ressources, sur un fil STA arrêté proprement. `ViewerPageSmokeTests` l'utilise désormais. Il instanciait `App`, dont le démarrage ouvrait la vraie fenêtre : le test échouait par intermittence (icône introuvable) et faisait planter l'hôte de test à la fin de la série.

### Limites connues
- Le patch est relatif à A. Avec des options d'ignorance ou la forme normalisée, les lignes jugées égales sont écrites avec le texte de A : appliquer le patch peut ne pas reproduire B à l'identique (un avertissement le signale).
- Au-delà de 4 096 différences dans une même zone, le découpage est approché : le résultat reste un diff valide, mais pas forcément minimal.
- Un texte collé dans un éditeur prend les fins de ligne du document ; seuls les fichiers chargés conservent leurs fins de ligne d'origine.
- Les options ne sont pas mémorisées d'une session à l'autre.

## [0.3.0] — Prompt Code Viewer #01 : Moteur de rendu

Nouvelle page **Moteur de rendu** dans le menu latéral : l'utilisateur colle du texte, le format est détecté et son rendu s'affiche à droite. Le prompt visait une page web ; il est réalisé en WPF natif, sans WebView2 (choix validé), et le traitement reste entièrement local.

### Ajouté
- **Item de menu « Moteur de rendu »** (icône Fluent, tooltip, `AutomationProperties.Name`), entre « Table de conversion » et « Paramètres ». L'état actif est géré par le `NavigationView`.
- **Détection automatique** (`FormatDetector`) : PHP, JSON (y compris JSON invalide, pour afficher l'erreur), HTML, sinon Markdown. Sélecteur Auto / JSON / Markdown / HTML / PHP pour forcer le format.
- **JSON** : arbre repliable (chargement paresseux des nœuds, types affichés, couleurs), onglet « Formaté » (pretty-print coloré), message d'erreur en français avec ligne et colonne (« virgule en trop avant } », « guillemets doubles requis », « fin inattendue »…).
- **Markdown** (Markdig 1.5.1 → `FlowDocument`) : titres, listes, listes de tâches, tableaux alignés, citations, blocs de code colorés, liens, images (http/https et `data:`), gras, italique, barré. Le HTML en ligne est affiché comme du texte.
- **HTML** : onglet « Aperçu » (contrôle `WebBrowser` intégré, contenu neutralisé par `HtmlSanitizer`, navigation bloquée) et onglet « Code source » coloré.
- **PHP** : jamais exécuté ; onglet « Code » coloré et onglet « Aperçu » si le fichier contient du HTML en dehors des blocs PHP.
- Éditeur à gauche (AvalonEdit : numéros de ligne, recherche, coloration selon le format), rendu à droite ; les panneaux s'empilent sous 900 px de large.
- Rendu mis à jour 300 ms après la dernière frappe ; boutons Coller, Copier, Effacer, Télécharger et bascule thème clair/sombre.
- `IThemeService.ThemeChanged` : la page se redessine au changement de thème, et le commutateur de la page Paramètres reste synchronisé.
- Coloration : les couleurs des définitions AvalonEdit sont éclaircies en thème sombre.
- Paquet : `Markdig` 1.5.1.
- Tests : détection de format, erreurs JSON, neutralisation HTML (scripts, `on*`, `javascript:` y compris encodé, iframes, commentaires conditionnels, `expression()`…), séparation PHP/HTML, rendu Markdown (tableau, bloc de code), arbre JSON, et un test de fumée qui charge la vraie page avec les vraies ressources (98 tests au total).

### Limites connues
- Le contrôle `WebBrowser` utilise le moteur Internet Explorer : rendu CSS/JS moderne limité, et pas de sandbox de processus. La protection repose sur la neutralisation du HTML avant affichage (scripts, gestionnaires d'événements, cadres, objets, `<link>`, `<meta>` supprimés) et sur le blocage de toute navigation. Les images distantes d'un aperçu HTML ou d'un Markdown sont chargées depuis le réseau.

## [0.2.1] — Correctif zone Source

### Corrigé
- **Zone Source non reliée au ViewModel** : le texte tapé dans l'éditeur n'arrivait pas à `MainViewModel.SourceText`, si bien que **Anonymiser** répondait « Aucun texte à anonymiser » malgré un texte et une table. La propriété attachée `AvalonEditBehavior.Text` avait pour valeur par défaut `""`, identique à la valeur initiale liée : le callback de changement ne se déclenchait jamais et l'éditeur n'était pas branché. La valeur par défaut est maintenant `null`.

### Ajouté
- `AvalonEditBehaviorTests` : tape dans l'éditeur → le ViewModel est mis à jour ; changement côté ViewModel → l'éditeur est mis à jour. Le premier test échoue sans le correctif.
- `CHANGELOG.md` et `Version.cs` (`AppVersion.Current`, à incrémenter à chaque évolution et à garder identique à la dernière entrée de ce fichier) ; la version est affichée dans Paramètres, section « À propos ».

## [0.2.0] — Prompt #02 : UI/UX et habillage visuel

Couche visuelle complète. `ConversionService`, `GeneratorService`, `TableStorageService` et les Models ne sont pas modifiés ; les 52 tests du #01 restent verts sans modification.

### Ajouté
- **Interface Fluent (WPF UI 4.3.0)** : `FluentWindow` (Mica), `NavigationView` latéral avec trois pages (Anonymisation, Table de conversion, Paramètres), `SnackbarPresenter`, `ContentDialogHost`.
- **Badge d'état** dans la barre de titre : « Table chargée : N entrées » (vert si la bijection est valide, rouge sinon, gris si aucune table).
- **Page Anonymisation** : éditeurs AvalonEdit côte à côte (numéros de ligne, recherche Ctrl+F), boutons Coller / Copier / Vider avec icônes et tooltips, barre d'actions (Anonymiser en accent, Rétablir, Générer automatiquement visible si l'option est activée, Aperçu), `ProgressRing` et désactivation des boutons pendant la génération.
- **Surlignage par type d'entité** dans le Résultat : colorisation AvalonEdit à partir des segments du `ReplacementResult`, tooltip « valeur d'origine », légende sous la zone.
- **État vide** : « Chargez ou créez une table pour commencer » dans la zone Résultat.
- **Page Table de conversion** : grille triable (Original, Remplaçant, Type, Auto, Statut), recherche instantanée insensible à la casse et aux accents, badge de validation (« Bijection valide » ou liste des doublons), suppression avec confirmation.
- **Import / Export chiffrés** : Import ajoute les entrées d'un fichier `.enc` (refusé s'il crée des doublons) ; Export écrit une copie chiffrée.
- **Page Paramètres** : stratégie de matching (boutons radio), génération automatique, thème clair/sombre, emplacement de la table chiffrée en lecture seule.
- **Aperçu en `ContentDialog`** : colonne « Retenu », boutons « Tout valider », « Valider la sélection », « Annuler ».
- **Toasts** après chaque action (anonymisation, rétablissement, copie, collage, vidage, chargement, enregistrement, import/export, changement de thème, etc.).
- **Raccourcis** : Ctrl+O (charger), Ctrl+S (enregistrer), Ctrl+Entrée (anonymiser), Ctrl+R (rétablir), Ctrl+D (copier le résultat).
- **Avertissement `ContentDialog`** quand la table est modifiée après une anonymisation (une seule fois par anonymisation), en plus du bandeau.
- `SettingsService` / `ThemeService` : thème et génération automatique persistés dans `Data\settings.json`.
- `SettingsViewModel`, `PageProvider`, `EnumToBoolConverter`, comportement `AvalonEditBehavior` (texte lié, colorisation, tooltip, copie en texte brut).
- Paquets : `WPF-UI` 4.3.0, `MahApps.Metro.IconPacks.Material` 6.2.1, `AvalonEdit` 6.3.1.120.
- Tests : `SettingsServiceTests`, `PreviewViewModelTests` (8 tests).

### Modifié
- `IDialogService` / `DialogService` : snackbar et `ContentDialog` à la place des `MessageBox` ; les dialogues bloquants attendent leur résultat en pompant la boucle de messages ; une demande pendant qu'un dialogue est ouvert est ignorée.
- `TableViewModel` : filtre, tri, état de validation, résumé de table, génération asynchrone sur une copie de la table.
- `MainViewModel` : état occupé, état vide, commandes Copier la source et Vider le résultat, notifications.
- `PreviewViewModel` : sélection par ligne, commandes « Tout valider », « Valider la sélection » et « Annuler ».
- `BoolToVisibilityConverter` : paramètre `Invert`.
- README : paquets, raccourcis, arborescence.

### Supprimé
- `RichTextBoxHighlighter`, `PreviewWindow`, `TableView` et les styles de boutons globaux du #01.
- Export en JSON en clair et import de JSON de l'interface (le service garde ces méthodes).

### Limites connues
- L'édition directe dans la grille après une anonymisation ne peut pas être bloquée avant coup : elle déclenche un avertissement.
- La stratégie de matching reste enregistrée dans la table, pas dans `settings.json`.
- Le surlignage, les tooltips, le thème sombre, les dialogues et les raccourcis n'ont pas été vérifiés visuellement.

## [0.1.0] — Prompt #01 : conception initiale

### Ajouté
- Application WPF (.NET 8, MVVM, CommunityToolkit.Mvvm) : trois zones Source, Résultat et Table.
- **Models** : `ConversionEntry`, `ConversionTable`, `EntityType`, `MatchingStrategy`, `ReplacementResult` (texte + segments index/longueur/entrée), validation de bijection.
- **Matching** : mots entiers uniquement, correspondance la plus longue prioritaire, stratégies `Strict`, `IgnoreCase`, `IgnoreCaseAndDiacritics`.
- **Réversibilité** : **Rétablir** restitue le texte source à l'identique après une anonymisation ; sinon repli sur l'inversion de la table.
- **Bijection** : rejet des doublons côté original et côté remplaçant, des originaux vides et des remplaçants vides, avec messages explicites.
- **Génération automatique** : valeurs françaises réalistes uniques par type (Nom, Ville, Email cohérent avec un nom, Tel, Autre : `ELEMENT_n`) ; aperçu modifiable avant application.
- **Stockage chiffré** : table JSON en AES-256-GCM, clé protégée par DPAPI, dans `Data\` à côté de l'exécutable.
- Presse-papiers en texte brut uniquement.
- 52 tests xUnit (conversion, génération, bijection, stockage chiffré).
- `README.md`, `nuget.config` (restauration limitée à nuget.org, le flux privé de la machine renvoyait 401).

### Corrigé pendant le développement
- Encodage JSON de l'export : les accents ne sont plus échappés (`è`).
