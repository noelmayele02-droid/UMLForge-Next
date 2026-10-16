# Architecture UMLForge Next

## Couches

### Interface WPF

- `MainWindow` : navigation de premier niveau et projet UML.
- `DiagramManagerWindow` : palette, canevas, inspecteur, validation et export des diagrammes.
- `CodeWorkspaceWindow` : explorateur, éditeur AvalonEdit, popup de complétion, navigation Roslyn, commandes .NET et lancement de la synchronisation.
- `GraphicalDebuggerWindow` : interface cliente DAP.
- `SynchronizationReviewWindow` : aperçu des changements, choix par fichier et confirmation.

### Modèle UML

`Models/UmlModel.cs` conserve les diagrammes, classes/relations, paquetages, propriétés/opérations, contraintes/stéréotypes et plusieurs familles sémantiques (interfaces, énumérations, cas d'utilisation, composants, déploiement, machines à états, activités et interactions). Le modèle porte une version de schéma et une collection `CodeMappings` pour associer des GUID UML aux symboles C#.

### Services UML

- `UmlNotationCatalog` : types de diagrammes, éléments et relations annoncés.
- `UmlValidationService` : contrôles de cohérence diagramme.
- `UmlProjectValidationService` : contrôles au niveau du projet et du modèle sémantique.
- `UmlMetamodelCatalog` : manifeste des principales familles de métaclasses UML et de leur statut de couverture.
- `OclInterpreter` / `OclConstraintService` : évaluation d'un sous-ensemble OCL sûr contre un contexte d'instance.
- `UmlCodeMappingService` : mapping des GUID UML vers des symboles Roslyn pour les classes, propriétés, opérations, interfaces, énumérations/littéraux et éléments compatibles, avec empreintes structurelles, chemin source et détection prudente de renommages.
- `PlantUmlExporter` et logique d'export : représentation textuelle du diagramme.
- `CodeGenerator` et `CSharpImportService` : premières transformations modèle/code.

### Services Roslyn

`SolutionSemanticService` ouvre le workspace MSBuild, obtient les diagnostics, propose la complétion et la signature help, suggère des imports, expose des actions de code limitées, résout les définitions/références, renomme les symboles et formate/trie le code. Les changements qui affectent plusieurs fichiers doivent être présentés avant écriture et sauvegardés.

### Synchronisation

`SynchronizationEngine` prépare un plan, détecte certains conflits, contrôle les hashes, sauvegarde les contenus et journalise les opérations. `CSharpThreeWayMergeService` tente une fusion syntaxique prudente de membres directs. Ce n'est pas encore une transaction complète ni un merge sémantique général.

### Débogueur

`DapDebugSession` implémente un sous-ensemble du transport DAP : requêtes, réponses, événements, pile d'appels, scopes/variables, threads et évaluation. La compatibilité et les capacités réelles dépendent de l'adaptateur externe.

## Principes de conception

1. **Aperçu avant mutation** : aucune synchronisation risquée ne doit écrire avant l'affichage du plan.
2. **Identité stable** : utiliser des GUID persistants pour les éléments UML et une table de correspondance vers les symboles Roslyn.
3. **Conflit explicite** : refuser une fusion ambiguë plutôt que choisir arbitrairement une version.
4. **Traçabilité** : sauvegardes, baseline, historique et messages compréhensibles.
5. **Tests par domaine** : règles UML, Roslyn, DAP et synchronisation doivent être testés indépendamment.
6. **Transparence produit** : différencier capacités présentes, partielles et objectifs de parité.

## Limites architecturales

Le métamodèle actuel est extensible mais pas normatif complet ; l’interpréteur OCL ne couvre qu’un sous-ensemble. Le mapping UML↔symboles couvre plusieurs catégories de types et membres, mais pas encore toutes les métaclasses, relations, propriétés et opérations de la norme ; les renommages structurels ambigus restent non résolus. L'éditeur WPF n'expose pas l'ensemble des CodeFixProviders/CodeRefactoringProviders Roslyn, et le débogueur s'appuie sur un adaptateur externe.
