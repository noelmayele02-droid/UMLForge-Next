# Capacités IDE C# présentes

## Roslyn

- Workspace MSBuild pour solutions/projets .NET.
- Complétion sémantique via `CompletionService` et popup clavier/souris dans AvalonEdit.
- Signature help pour les invocations et créations d'objets lorsque Roslyn résout les symboles.
- Suggestions d'import à partir des types manquants CS0246 et des espaces de noms de symboles source/référencés.
- Actions de code de base : formatage, tri des imports, ajout d'un `using`, suppression proposée des imports inutilisés.
- Diagnostics de compilation par fichier, navigables vers la ligne/colonne.
- Renommage sémantique multi-documents, définition et références.

## Précautions

Les imports automatiques reposent sur les diagnostics et les symboles disponibles dans le projet restauré. Les `using` inutilisés sont déterminés par une analyse syntaxique/sémantique pragmatique ; vérifiez le diff avant de sauvegarder. Les actions disponibles ne sont pas l'intégralité des `CodeFixProvider`/`CodeRefactoringProvider` de Visual Studio.

La coloration des diagnostics est actuellement assurée par un panneau de diagnostics navigable et non par la totalité des squiggles, ampoules, marges d'erreur et code fixes intégrés de Visual Studio.
