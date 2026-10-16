# Feuille de route UMLForge Next

La feuille de route vise à faire converger un atelier UML spécialisé, un environnement C# Roslyn et un moteur de synchronisation fiable. Les étapes sont séquencées par la fiabilité, pas seulement par le nombre de fonctions.

## P0 — Valider la base avant publication

- [ ] Exécuter `dotnet restore`, `dotnet build -c Release` et `dotnet test` sur Windows avec .NET 10.
- [ ] Corriger chaque erreur et avertissement bloquant.
- [ ] Tester l'ouverture d'un `.sln` multi-projets avec références réelles.
- [ ] Tester les sauvegardes, l'annulation et les conflits de synchronisation.
- [ ] Tester le débogueur sur une application .NET minimale avec PDB et adaptateur identifié.
- [ ] Vérifier la création d'un paquet Release et documenter les prérequis exacts.

## P1 — Métamodèle et conformité UML

- [x] Introduire un numéro de version de schéma et une version UML cible dans le modèle JSON ; les migrations rétrocompatibles complètes restent à faire.
- [ ] Compléter les métaclasses, propriétés, opérations, paramètres, généralisations, associations, compositions, dépendances, ports, connecteurs, fragments et contraintes.
- [ ] Étendre les GUID et références croisées à chaque métaclasse UML normative (les familles principales sont présentes, la couverture reste partielle).
- [ ] Formaliser chaque règle par un ID, sa portée, son sévérité, un message et des fixtures positives/négatives.
- [ ] Implémenter les règles détaillées par diagramme et les contraintes de relations admissibles.
- [x] Intégrer un évaluateur OCL sûr pour un sous-ensemble documenté ; [ ] compléter la grammaire et la bibliothèque standard OCL.
- [ ] Créer des éditeurs visuels dédiés aux 14 familles de diagrammes.
- [ ] Ajouter zoom/pan, alignement, distribution, grille, couches, swimlanes, connecteurs orthogonaux, ancrages, undo/redo et raccourcis.
- [ ] Vérifier import/export et round-trip des modèles.
- [ ] Évaluer séparément les profils et extensions comme SysML/BPMN au lieu de les assimiler à UML de base.

## P2 — Expérience IDE C# / Roslyn

- [ ] Rendre la complétion incrémentale, filtrable et stable pendant la frappe.
- [x] Ajouter une première signature help, des suggestions d’imports automatiques et des actions de code de base ; [ ] compléter snippets, documentation contextuelle et expérience de complétion.
- [x] Ajouter un panneau de diagnostics par fichier et navigation vers la ligne/colonne ; [ ] ajouter squiggles, ampoules et filtres avancés dans l’éditeur.
- [x] Ajouter des actions de code sûres de base avec confirmation ; [ ] intégrer les CodeFixProvider/CodeRefactoringProvider Roslyn avec aperçu.
- [ ] Ajouter extractions de méthode/interface, encapsulation, safe delete et conversions courantes.
- [ ] Ajouter hiérarchie d'appels, trouver implémentations, naviguer vers métadonnées et indexation.
- [ ] Synchroniser l'état du document non sauvegardé avec le workspace et éviter les mises à jour concurrentes.
- [ ] Ajouter une architecture de services de langage testée et une gestion robuste de l'annulation.

## P3 — Débogueur graphique

- [ ] Vérifier `initialize`, `launch`, `configurationDone`, `stopped`, `continued` et `terminated` avec un adaptateur ciblé.
- [ ] Poser/retirer les breakpoints depuis la marge et refléter les breakpoints validés par l'adaptateur.
- [ ] Ajouter conditions, hit conditions, logpoints et état activé/désactivé par breakpoint.
- [ ] Créer des panneaux séparés Autos, Locals, Watch, Call Stack, Threads, Modules et Exceptions.
- [ ] Ajouter expansion récursive des variables et évaluation asynchrone contrôlée.
- [ ] Ajouter gestion des exceptions, attach à un processus et configurations de lancement.
- [ ] Tester les sources mappées, PDB manquants, chemins Windows et arrêts multiples.

## P4 — Synchronisation UML ↔ code

- [x] Étendre le mapping GUID UML ↔ symboles Roslyn aux classes, propriétés, opérations, interfaces, énumérations/littéraux et éléments compatibles ; [ ] couvrir toutes les métaclasses et adopter une clé de symbole versionnée.
- [ ] Distinguer nom, signature, namespace, type, chemin et identifiant de l'élément.
- [ ] Gérer les classes `partial`, génériques, overloads, attributs et membres générés.
- [ ] Fusionner les changements indépendants au niveau syntaxique et sémantique lorsque c'est sûr.
- [ ] Résoudre les renommages de symboles et de fichiers avec preuve d'identité, pas seulement par heuristique de nom.
- [ ] Prévisualiser toutes les suppressions, déplacements et changements de namespace.
- [ ] Rendre l'application transactionnelle avec manifeste, backup, rollback et tests de panne.
- [ ] Préserver commentaires, directives, trivia, ordre des attributs et conventions de formatage.
- [ ] Ajouter des tests d'édition concurrente et de modification d'un fichier entre aperçu et application.

## P5 — Expérience de produit et publication

- [ ] Profils de workspace, onglets multi-fichiers et restauration de session.
- [ ] Undo/redo global pour les modifications de modèle et d'éditeur.
- [ ] Sauvegarde automatique configurable et récupération après crash.
- [ ] Journal d'activité et rapport d'analyse/export.
- [ ] Tests UI, tests d'installation et mesures de performance sur gros projets.
- [ ] Matrice de parité publique, fonction par fonction, sans revendiquer l'équivalence non vérifiée.
- [ ] Versionner les schémas de données et publier les notes de version.

## Définition de terminé

Une fonction n'est considérée comme terminée que si elle dispose d'une implémentation, d'un test reproductible, d'une gestion d'erreur, d'une documentation, et d'une vérification sous Windows. Un élément simplement catalogué ou dessiné dans l'interface ne compte pas comme une fonctionnalité UML ou IDE complète.


## Gel fonctionnel de la version de référence

À partir de cette consolidation, priorité à la compilation et aux tests Windows, aux corrections de régression, aux scénarios manuels de synchronisation et à la préparation de la publication GitHub. Les nouvelles fonctionnalités sont reportées après validation de cette base.
