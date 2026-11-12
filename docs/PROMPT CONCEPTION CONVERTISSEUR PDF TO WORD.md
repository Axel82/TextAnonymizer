# Rôle et expertise

Agis comme un architecte logiciel senior spécialisé en C#, .NET 8, WPF, MVVM, traitement documentaire, PDF et génération de documents Microsoft Word.

Tu dois intervenir sur une application Windows existante développée avec :

- C#
- .NET 8
- WPF
- Architecture MVVM
- CommunityToolkit.Mvvm
- Dependency Injection
- UglyToad.PdfPig
- DocumentFormat.OpenXml (Open XML SDK)

# 1. Contexte du projet

L'application s'appelle PdfToWord.

Son objectif est de permettre à un utilisateur de sélectionner un document PDF et de le convertir en document Word (.docx) modifiable.

L'application possède déjà :

- Une interface WPF moderne.
- Un MainViewModel.
- Une architecture basée sur les services et interfaces.
- Une injection de dépendances.
- Un service IPdfConverterService.
- Une première implémentation PdfConverterService.
- Un service de sélection et d'enregistrement des fichiers.

La première version du moteur extrait le texte des pages PDF et le réinjecte dans un document Word.

Cette approche est insuffisante, car elle ne conserve pas correctement la mise en page originale.

# 2. Objectif de la V2

Développer un véritable moteur de conversion PDF vers Word permettant de préserver au maximum la structure et la présentation du document original.

Le résultat doit être un document Word réellement modifiable, et non une simple image du PDF.

# 3. Fonctionnalités à développer

## 3.1 Analyse de la structure du PDF

Utiliser UglyToad.PdfPig pour analyser chaque page.

Récupérer notamment :

- Les dimensions des pages.
- Les coordonnées X et Y des caractères.
- Les coordonnées des mots.
- Les polices utilisées.
- Les tailles de police.
- Les styles disponibles.
- Les espaces entre les mots.
- Les lignes de texte.
- Les blocs de texte.
- Les images présentes dans le document.

Développer un système permettant de regrouper les caractères en mots, les mots en lignes et les lignes en paragraphes.

L'analyse doit tenir compte de l'ordre de lecture et de la disposition spatiale du document.

## 3.2 Reconstruction de la mise en page

Reproduire autant que possible :

- Les marges.
- Les espacements.
- Les alignements.
- Les paragraphes.
- Les titres et sous-titres.
- Les colonnes.
- Les listes à puces.
- Les listes numérotées.
- Les sauts de page.
- Les en-têtes et pieds de page.
- Les différentes tailles de police.
- Le gras et l'italique.
- Les couleurs du texte.

Le moteur doit utiliser les coordonnées du PDF pour reconstruire la position des éléments dans Word.

Éviter de générer systématiquement un paragraphe par ligne si plusieurs lignes appartiennent au même paragraphe.

## 3.3 Gestion des images

Détecter et extraire les images du PDF.

Les réinsérer dans le document Word en respectant autant que possible :

- Leur position.
- Leur taille.
- Leur proportion.
- Leur alignement.
- Leur ordre d'apparition.

Utiliser Open XML SDK pour insérer les images dans le document DOCX.

Ne pas déformer les images.

## 3.4 Détection et reconstruction des tableaux

Identifier les structures tabulaires à partir :

- Des alignements horizontaux.
- Des coordonnées des cellules.
- Des espacements.
- Des bordures lorsqu'elles sont disponibles.
- Des regroupements de textes.

Convertir les tableaux détectés en véritables tableaux Word modifiables.

Conserver autant que possible les colonnes, les lignes, les cellules fusionnées et les bordures.

## 3.5 Gestion des pages

Chaque page PDF doit être analysée indépendamment.

Respecter :

- Le format de page.
- L'orientation portrait ou paysage.
- Les dimensions.
- Les marges.
- Les sauts de page.

Ne pas fusionner incorrectement le contenu de pages différentes.

# 4. Architecture technique attendue

Conserver strictement l'architecture MVVM existante.

Ne pas mettre de logique métier dans les Views ou les ViewModels.

Faire évoluer les services avec une architecture modulaire.

Structure proposée :

Services/
    Interfaces/
        IPdfConverterService.cs
        IPdfAnalyzerService.cs
        ILayoutReconstructionService.cs
        IImageExtractionService.cs
        ITableDetectionService.cs
        IWordDocumentService.cs

    Implementations/
        PdfConverterService.cs
        PdfAnalyzerService.cs
        LayoutReconstructionService.cs
        ImageExtractionService.cs
        TableDetectionService.cs
        WordDocumentService.cs

Models/
    PdfDocumentModel.cs
    PdfPageModel.cs
    PdfTextBlock.cs
    PdfTextLine.cs
    PdfWord.cs
    PdfImage.cs
    PdfTable.cs
    PdfTableCell.cs
    PdfFontInfo.cs
    ConversionResult.cs

# 5. Qualité du code

Respecter les principes suivants :

- SOLID.
- Clean Code.
- Separation of Concerns.
- Dependency Injection.
- Async/Await.
- CancellationToken.
- Gestion centralisée des exceptions.
- Logging.
- Code fortement typé.
- Méthodes courtes et spécialisées.
- XML Documentation pour les classes et méthodes publiques.

Éviter les dépendances inutiles.

Privilégier les bibliothèques gratuites et open source.

Ne pas utiliser de bibliothèque commerciale.

Ne pas remplacer l'architecture actuelle sans justification technique.

# 6. Performance et robustesse

Le moteur doit pouvoir traiter des PDF de plusieurs centaines de pages.

Prévoir :

- Le traitement asynchrone.
- L'annulation d'une conversion.
- La libération correcte des ressources.
- Une gestion mémoire maîtrisée.
- La détection des PDF protégés.
- La gestion des pages vides.
- La gestion des caractères spéciaux.
- La gestion des polices non disponibles sur Windows.
- La gestion des erreurs d'extraction.
- La suppression des fichiers temporaires.

Une erreur sur une page ne doit pas nécessairement empêcher le traitement des autres pages.

# 7. Résultat attendu

Je veux obtenir :

1. Un moteur PDF vers Word V2 opérationnel.
2. Une architecture de services modulaire.
3. Une extraction précise des éléments PDF.
4. Une reconstruction fidèle de la mise en page.
5. Une insertion correcte des images.
6. Une reconstruction des tableaux.
7. Un document DOCX réellement modifiable.
8. Une intégration complète avec le ViewModel existant.

# 8. Méthode de développement obligatoire

Ne génère pas tout le projet en une seule fois.

Travaille étape par étape.

Pour chaque étape :

1. Analyse l'architecture existante.
2. Identifie les fichiers à créer ou modifier.
3. Explique les choix techniques.
4. Fournis le code C# complet de chaque fichier.
5. Fournis les éventuelles modifications XAML.
6. Indique les packages NuGet nécessaires.
7. Vérifie la compatibilité .NET 8.
8. Vérifie les namespaces et les dépendances.
9. Explique comment tester la fonctionnalité.
10. Attends ma validation avant de passer à l'étape suivante.

Ne fournis jamais de pseudo-code lorsqu'un code C# opérationnel est attendu.

Ne supprime aucune fonctionnalité existante sans justification.

# 9. Première tâche

Commence par analyser le moteur actuel PdfConverterService.

Développe ensuite la première brique de la V2 :

**Un moteur d'analyse géométrique du PDF capable de détecter les caractères, les mots, les lignes et les paragraphes à partir de leurs coordonnées réelles.**

Cette brique doit constituer la fondation de toute la reconstruction de la mise en page Word.

Fournis tous les fichiers nécessaires et leur contenu complet.