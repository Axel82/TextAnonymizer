# Développement d'une fonctionnalité : Comparateur de fichiers (File Diff)

## Objectif

Ajouter à l'application existante un outil permettant de comparer deux fichiers ou deux contenus textuels afin d'identifier rapidement toutes les différences.

Cette fonctionnalité doit être ergonomique, performante et adaptée à une utilisation quotidienne par des développeurs et utilisateurs techniques.

## 1. Interface utilisateur

Créer une nouvelle fonctionnalité nommée « Comparateur de fichiers » accessible depuis le menu principal de l'application.

L'interface doit comporter :

- Un panneau pour le fichier ou texte A (version originale).
- Un panneau pour le fichier ou texte B (nouvelle version).
- Un bouton pour charger un fichier dans chaque panneau.
- La possibilité de coller directement du texte.
- La possibilité de glisser-déposer des fichiers.
- Un bouton « Comparer ».
- Un bouton « Inverser A et B ».
- Un bouton « Effacer ».
- Un bouton « Synchroniser le défilement ».

Prévoir une interface claire, moderne et cohérente avec le design actuel de l'application.

## 2. Modes de comparaison

Proposer deux modes d'affichage :

**Mode côte à côte (Side by Side)**
- Afficher les deux contenus simultanément.
- Aligner les lignes correspondantes.
- Surligner les différences.
- Afficher les numéros de lignes.

**Mode unifié (Unified Diff)**
- Afficher les deux contenus dans une seule vue.
- Préfixer les ajouts par +.
- Préfixer les suppressions par -.
- Identifier clairement les modifications.

## 3. Identification des différences

Utiliser un véritable algorithme de comparaison de texte (diff algorithm).

Identifier visuellement :

- Les lignes ajoutées.
- Les lignes supprimées.
- Les lignes modifiées.
- Les blocs de différences.
- Les lignes identiques.

Pour les lignes modifiées, proposer si possible une comparaison au niveau des mots ou des caractères afin d'identifier précisément les changements.

Utiliser une coloration distincte :
- Vert : ajout.
- Rouge : suppression.
- Jaune ou orange : modification.

## 4. Options de comparaison

Ajouter les options suivantes :

- Ignorer les différences de casse.
- Ignorer les espaces en début et fin de ligne.
- Ignorer les espaces multiples.
- Ignorer les tabulations.
- Ignorer les lignes vides.
- Ignorer les différences de fins de ligne Windows/Linux.
- Comparaison sensible ou non aux espaces.

Les options doivent pouvoir être activées indépendamment.

## 5. Statistiques

Après comparaison, afficher un résumé :

- Nombre de lignes identiques.
- Nombre de lignes ajoutées.
- Nombre de lignes supprimées.
- Nombre de lignes modifiées.
- Pourcentage de similarité.
- Nombre total de différences.

## 6. Navigation

Ajouter des commandes permettant de :

- Passer à la différence suivante.
- Revenir à la différence précédente.
- Afficher le numéro de la différence actuelle.
- Mettre en évidence le bloc actuellement sélectionné.

Exemple : Différence 3 / 12.

## 7. Comparaison intelligente selon le format

Lorsque cela est possible, proposer des modes spécifiques :

- Texte brut : comparaison ligne par ligne.
- JSON : comparaison structurelle des propriétés et valeurs.
- XML : comparaison des éléments et attributs.
- Markdown : comparaison du contenu et des sections.

La détection du format peut être automatique, mais l'utilisateur doit pouvoir choisir manuellement le mode.

Attention : pour JSON et XML, distinguer les changements structurels des simples différences de présentation (indentation, espaces, ordre des propriétés lorsque cela est pertinent).

## 8. Exportation

Permettre d'exporter le résultat :

- Format TXT.
- Format HTML avec conservation des couleurs.
- Format DIFF / PATCH si compatible.
- Copie du résultat dans le presse-papiers.

## 9. Performances et robustesse

- Prévoir la comparaison de fichiers volumineux.
- Éviter de bloquer l'interface pendant les calculs.
- Gérer les fichiers vides.
- Gérer les fichiers identiques.
- Signaler clairement les erreurs de lecture.
- Détecter les fichiers binaires et éviter de les traiter comme du texte.
- Respecter l'encodage des fichiers lorsque cela est possible.
- Ne jamais modifier les fichiers originaux.

## 10. Contraintes de développement

Avant toute modification :

1. Analyser l'architecture existante.
2. Identifier les composants réutilisables.
3. Respecter les conventions de développement actuelles.
4. Réutiliser les composants graphiques existants.
5. Ne pas modifier les fonctionnalités déjà présentes.
6. Éviter d'ajouter des dépendances inutiles.

La fonctionnalité doit être intégrée proprement dans l'application existante.

## 11. Tests attendus

Prévoir des tests pour :

- Deux fichiers identiques.
- Un fichier avec des lignes ajoutées.
- Un fichier avec des lignes supprimées.
- Des lignes modifiées.
- Des contenus très volumineux.
- Des fichiers avec différents encodages.
- Les options d'ignorance des différences.
- Les différents modes d'affichage.
- Les exports.

## Résultat attendu

Une fonctionnalité complète de comparaison de fichiers, professionnelle, rapide et simple à utiliser, comparable aux outils de diff modernes utilisés par les développeurs.

Ne pas se limiter à une simple comparaison de chaînes de caractères : l'objectif est de proposer un véritable outil d'analyse des différences.