## Consolidation de référence — 2026-10-16

- Étendu le mapping GUID UML ↔ symboles Roslyn aux classes, propriétés, opérations, interfaces, énumérations/littéraux, signaux/attributs et types primitifs compatibles.
- Ajouté une empreinte structurelle pour la reconnaissance prudente de certains renommages, un statut `Ambiguous` qui évite les associations hasardeuses et une indication pour les types `partial`.
- Étendu le catalogue de métaclasses UML comme manifeste de couverture et précisé explicitement qu’il ne constitue pas une implémentation normative intégrale UML 2.5.1.
- Ajouté un test de mapping des types et membres et un script PowerShell de validation/build/tests, avec option de publication locale après réussite des tests.
- Orienté cette version vers la stabilisation et la validation Windows plutôt que l’ajout de nouvelles fonctionnalités.
- Limite : compilation/tests non exécutables dans l’environnement de préparation ; le résultat doit être confirmé par le workflow Windows avant publication.

# Journal des modifications

Les versions sont décrites selon l'état du code source. Les fonctionnalités non validées sous Windows restent indiquées comme telles.

## Version de travail — 2026-10-16

### Ajouté
- Métamodèle UML enrichi pour interfaces, énumérations, acteurs/cas d'utilisation, composants, déploiement, signaux, machines à états, activités et interactions.
- Catalogue de couverture des principales familles de métaclasses UML 2.5.1.
- Interpréteur OCL sûr pour un sous-ensemble d'expressions, plus évaluation des contraintes attachées aux éléments contre une instance runtime.
- Validateur de modèle/projet : noms, signatures, multiplicités, références, cycles de généralisation, machines à états, activités et interactions.
- Signature help Roslyn pour les invocations et constructeurs.
- Suggestions d'import automatique à partir des types non résolus et des assemblies de référence.
- Actions de code de base : formatage, tri des `using`, ajout d'import et nettoyage de `using` inutilisés.
- Panneau de diagnostics par fichier avec navigation vers la ligne/colonne.
- Mapping GUID des classes UML vers les types Roslyn, avec empreinte du symbole et sauvegarde du modèle avant écriture.
- Documentation OCL/IDE, README visuel, guide de contribution, politique de sécurité et modèles GitHub.
- 18 tests sont définis dans le projet de tests.

### À vérifier avant publication
- Compilation et tests automatisés sous Windows avec .NET 10.
- Test manuel de la popup Roslyn, de la navigation et des actions de code.
- Test réel de l'adaptateur DAP sélectionné.
- Vérification du cycle complet de sauvegarde, conflit et restauration.

### Limites connues
- Pas encore de conformité complète OMG UML 2.5.1 ; l'interpréteur OCL ne couvre qu'un sous-ensemble documenté.
- Les actions de code Roslyn ne remplacent pas l'ensemble des CodeFixProviders/CodeRefactoringProviders de Visual Studio.
- Pas de parité fonctionnelle complète avec Visual Studio Community, StarUML ou Visual Paradigm.
- Pas de validation d'exécution dans l'environnement de préparation faute de SDK .NET.
