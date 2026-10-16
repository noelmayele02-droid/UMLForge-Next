# Environnement C# — Roslyn et éditeur

## Fonctions intégrées

- AvalonEdit, coloration syntaxique, numéros de ligne, recherche/remplacement et formatage.
- `MSBuildWorkspace` pour ouvrir une solution ou un projet et charger ses références.
- Diagnostics de compilation par projet/fichier/ligne/colonne.
- Complétion Roslyn affichée dans une popup de l'éditeur avec navigation clavier et insertion.
- Renommage de symboles par `Renamer` sur plusieurs documents, après confirmation et sauvegarde.
- Navigation vers une définition source (`F12`) et recherche de références.
- Tri des directives `using` avec confirmation avant application.

## Parcours conseillé

1. Ouvrir un dossier qui contient un `.sln` ou `.csproj`.
2. Lancer **Analyser solution** et vérifier que MSBuild a chargé les projets et références.
3. Ouvrir un fichier `.cs` qui appartient à cette solution.
4. Utiliser `Ctrl+Espace` pour la complétion, `F12` pour la définition, ou **Références** pour rechercher les usages.
5. Avant un renommage multi-fichiers, examiner la liste des fichiers touchés et confirmer l'écriture.

## Limites actuelles

La complétion s'appuie sur le service Roslyn mais n'offre pas encore toutes les fonctions de l'IDE Visual Studio : completion filtering en continu, signature help, imports automatiques, snippets, quick-fixes, actions de code exhaustives, hiérarchie d'appels, navigation externe, tests d'intégration et diagnostics dessinés directement dans la marge ne sont pas complets. Le tri des `using` est une action syntaxique initiale et doit être testé sur les directives globales, alias et commentaires complexes.

Le service doit évoluer avec des tests de workspace multi-projets, fichiers `partial`, symboles externes, erreurs de compilation et modifications non sauvegardées. Toujours conserver les sauvegardes et confirmer les modifications de plusieurs fichiers.
