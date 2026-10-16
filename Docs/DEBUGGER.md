# Débogueur graphique — DAP

## Préparer une session

1. Installer un adaptateur DAP compatible .NET (l'adaptateur n'est pas fourni dans le dépôt).
2. Compiler le projet cible en configuration Debug pour générer la DLL et les fichiers PDB.
3. Dans **Débogueur DAP**, choisir l'exécutable de l'adaptateur, la DLL, un fichier source et les points d'arrêt.
4. Démarrer la session et vérifier les messages de l'adaptateur dans le journal.

## Format des points d'arrêt

Séparer les entrées par des virgules :

- `12` — arrêt à la ligne 12 ;
- `25?count > 3` — arrêt conditionnel si `count > 3` ;
- `40?user.IsAdmin` — arrêt conditionnel si l'expression est vraie.

Les conditions sont envoyées à l'adaptateur via le champ DAP `condition`. Leur syntaxe, leur prise en charge et leur validation dépendent de l'adaptateur.

## Fenêtres/actions disponibles

- Continuer, pause, pas suivant, entrer et sortir d'une fonction.
- Pile d'appels.
- Variables locales/Autos exposées par les scopes de l'adaptateur.
- Liste des threads.
- Évaluation d'une expression Watch dans la frame active.
- Journal des messages DAP.

## Limites

Ce n'est pas encore un débogueur de parité Visual Studio. Les points d'arrêt sont saisis par ligne, pas encore posés dans la marge de l'éditeur. Les points d'arrêt par nombre de passages et les logpoints ne sont pas exposés dans l'interface. L'inspection de threads dépend de l'adaptateur et l'évaluation Watch nécessite généralement une session en pause. Attach à un processus, gestion visuelle des exceptions, Hot Reload, mémoire, décompilation et multi-target ne sont pas implémentés intégralement.

Les tests doivent être faits sur Windows avec une version précise de l'adaptateur, un projet .NET réel, les PDB correspondants et des scénarios de lancement/arrêt répétés.
