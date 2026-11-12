# Visualiseur de texte

## Objectif

Développe un « visualiseur de texte » : l'utilisateur colle du texte brut dans une zone de saisie, et l'application affiche automatiquement le rendu correspondant, en détectant dynamiquement le format.

## Intégration au menu

Ajoute un **item dédié dans le menu principal** de l'application, nommé « Visualiseur de texte » (ou « Aperçu de texte »), qui ouvre la fonctionnalité. L'item doit :

- Suivre la structure et le style du menu existant (icône, position logique, accessibilité clavier, aria-label si pertinent) ;
- Être accessible depuis toutes les vues où le menu est visible ;
- Marquer l'item comme actif/selectionné quand la fonctionnalité est ouverte ;
- Comporter un raccourci clavier si le menu en prévoit pour les autres items.

## Formats supportés et rendus attendus

1. **JSON** — arborescence interactive (nœuds repliables/dépliables), coloration syntaxique, formatage/pretty-print automatique, affichage des types (string, nombre, booléen, null), message d'erreur explicite avec position en cas d'échec de parsing.
2. **Markdown** — rendu complet (titres, listes, tableaux, blocs de code colorés, liens, images, gras/italique). Utiliser une bibliothèque éprouvée (ex. `marked` + `highlight.js` ou `react-markdown` + `rehype-highlight`).
3. **HTML** — aperçu rendu en direct dans un **iframe sandboxé** (isolation sécurité), plus un onglet « code source » avec coloration syntaxique.
4. **PHP** — pas d'exécution : uniquement coloration syntaxique et rendu lisible du code (onglets « Code » / « Aperçu » si le fichier contient aussi de l'HTML).

## Comportements clés

- **Détection automatique** du format au collage, avec possibilité de forcer manuellement le format via un sélecteur (Auto / JSON / Markdown / HTML / PHP).
- **Rendu en dynamique** : mise à jour en temps réel (debounce ~300 ms) à chaque frappe ou modification.
- **Deux panneaux côte à côte** : éditeur (coloration syntaxique, numérotation des lignes) à gauche, rendu à droite ; disposition empilée en responsive sur mobile.
- **Boutons utilitaires** : copier, effacer, télécharger le contenu, bascule mode sombre/clair.

## Contraintes techniques

- HTML/CSS/JS vanilla ou React (une seule page, sans backend).
- Tout le traitement en local côté client — aucun envoi de données à un serveur.
- Échapper correctement le contenu HTML/JSX pour éviter toute injection (XSS) ; pour le JSON, valider avec `try/catch` avant le rendu.

## Critères d'acceptation

- Le menu contient un item « Visualiseur de texte » fonctionnel, conforme au style du menu existant.
- Coller un JSON invalide n'affiche pas d'erreur brute mais un message compréhensible.
- Un document Markdown avec tableau et bloc de code se rend fidèlement.
- Un fichier HTML contenant `<script>` s'affiche dans l'iframe sans exécuter le script côté page principale.