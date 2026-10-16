# État réel de UMLForge Next

> Dernière mise à jour : 16 octobre 2026. Le code de cette archive a été modifié dans l'environnement de préparation, mais le SDK .NET n'est pas disponible ici : aucune compilation ni exécution WPF réelle n'a pu être confirmée.

## Ajouts dans cette version

- Modèle sémantique enrichi : paquetages, propriétés, opérations, paramètres, contraintes, stéréotypes, valeurs taguées, interfaces, énumérations, acteurs/cas d'utilisation, composants, déploiement, signaux, machines à états, activités et interactions.
- Catalogue de couverture des grandes familles de métaclasses UML 2.5.1 et validation de références entre plusieurs nouveaux éléments.
- Interpréteur OCL sécurisé pour un sous-ensemble documenté, avec évaluation contre un objet runtime via `OclConstraintService`.
- Validateur de projet UMLForge en plus des contrôles de diagrammes : noms/identifiants, propriétés et opérations dupliquées, multiplicités, visibilités, signatures, relations orphelines, cycles de généralisation, paquetages et contraintes.
- Règles complémentaires pour communication, structure composite, vues d'interaction, profils et diagrammes de timing.
- Popup de complétion sémantique Roslyn dans l'éditeur, insertion clavier et souris.
- Navigation `F12` vers la définition, recherche de références et tri des `using` avec confirmation.
- Signature help Roslyn, suggestions d'import automatique, actions de code de base (formatage, tri/imports, nettoyage `using`) et panneau de diagnostics par fichier avec navigation vers la ligne concernée.
- Débogueur DAP : conditions de points d'arrêt au format `ligne?expression`, liste des threads et évaluation Watch dans une frame active.
- README réorganisé, documents dédiés au modèle UML, à l'éditeur, au débogueur et à la synchronisation, guide de contribution, politique de sécurité et modèles d'issues/PR GitHub.
- Tests sur l’évaluation OCL, les contraintes runtime, le catalogue UML et un scénario de mapping de classes/membres/interfaces/énumérations (19 tests sont définis dans cette archive ; ils restent à exécuter sous Windows).

## Fonctions déjà présentes dans l'archive

- Atelier visuel multi-diagrammes et export PlantUML/JSON.
- Workspace Roslyn de solution/projet, diagnostics, renommage symbolique multi-fichiers et formatage.
- Plan de synchronisation, baseline, historique, sauvegardes, contrôle de concurrence et fusion syntaxique prudente à trois voies.
- Passerelle GUID UML ↔ symboles Roslyn pour classes, propriétés, opérations, interfaces, énumérations/littéraux et quelques types/éléments supplémentaires ; empreintes structurelles, gestion des classes partial et reconnaissance prudente de certains renommages. Les correspondances ambiguës restent explicitement non résolues.
- Première interface cliente DAP, pas à pas et consultation des variables/pile.
- Tests unitaires et workflow GitHub Actions Windows.

## Validation effectuée dans l'environnement de préparation

- Revue statique du code modifié.
- Vérification statique : parse XML des XAML/projets, liens README, références d'événements XAML et intégrité de l'archive.
- Vérification à effectuer avant publication : `dotnet restore`, `dotnet build` et `dotnet test` sur Windows.
- Aucun résultat de compilation n'est revendiqué : le binaire `dotnet` n'est pas installé dans cet environnement.

## Limites critiques encore ouvertes

1. Le catalogue de métaclasses ne constitue pas encore une implémentation normative complète d'OMG UML 2.5.1 ; l'OCL intégré ne couvre qu'un sous-ensemble documenté, pas toute la spécification.
2. La complétion Roslyn, signature help, imports automatiques, actions de code de base et diagnostics de fichier sont intégrés, mais la parité avec Visual Studio (CodeFixProviders exhaustifs, analyzers, squiggles dans la marge, hiérarchie d'appels, refactorisations avancées) n'est pas atteinte.
3. Le débogueur dépend d'un adaptateur DAP externe ; toutes les fonctionnalités Visual Studio ne sont pas exposées ni testées.
4. Le merge UML↔C# ne résout pas tous les cas structurels, renommages, namespaces, fichiers partial ou suppressions simultanées.
5. La validation Windows et les tests de bout en bout doivent réussir avant de considérer la version prête à distribuer. Le mapping des membres et les renommages structurels nécessitent encore des tests sur des solutions réelles, notamment en cas de collisions de signatures ou de classes partial réparties sur plusieurs fichiers.
6. Le catalogue de métaclasses et les notations répertoriées servent de manifeste de couverture : ils ne prouvent pas la prise en charge normative de chaque métaclasse, notation et contrainte.

## Critère de publication

Ne publier une version présentée comme stable qu'après une exécution réussie du workflow Windows et des tests manuels sur un projet C# réel. Le README doit conserver cette distinction entre fonctions implémentées, fonctions partielles et objectifs de parité.
