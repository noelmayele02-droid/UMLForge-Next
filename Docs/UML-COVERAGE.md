# UMLForge Next — Couverture UML et règles

## Périmètre

Le catalogue central expose les 14 familles de diagrammes UML 2.x utilisées par les outils généralistes. Le catalogue n'est pas à lui seul le métamodèle UML : il décrit la palette, les relations attendues et les contrôles de cohérence activés par UMLForge.

## Familles disponibles

| Type | Concepts déclarés | Contrôles actuellement disponibles | Limites majeures |
|---|---|---|---|
| Class | Classes, interfaces, énumérations, paquetages, notes | Identifiants, relations, multiplicités et règles projet sur types/membres | Associations qualifiées, templates UML complets, propriétés/opérations dans la notation graphique |
| UseCase | Acteurs, cas d'utilisation, frontière, include, extend | Présence acteur/cas, références valides, auto-include suspect | Règles complètes include/extend, extension points et notation graphique |
| Sequence | Lifelines, messages, retours, création/destruction | Présence lifelines/messages, message décrit | Fragments combinés, occurrence specifications, gates et ordre temporel formel |
| Activity | Actions, décisions, merge, fork/join, objets, signaux | Nœuds initial/final, gardes de décisions | Pins, partitions, exception handlers et flux typés complets |
| Component | Composants, interfaces, ports, artefacts | Présence composant, liens et types | Interfaces fournies/requises graphiques, connecteurs d'assemblage complets |
| Deployment | Nœuds, appareils, environnements, artefacts | Présence nœud, références valides | Spécifications d'exécution et règles de déploiement complètes |
| State | États, transitions et pseudo-états | Présence état initial et état, gardes | Régions orthogonales, entry/exit/do, history et sémantique complète |
| Package | Paquetages, imports, merge, nesting | Références, métadonnées du modèle | Graphe de dépendances avancé, règles de fusion et de visibilité |
| Object | Objets et liens | Présence d'objets et liens valides | Slots, valeurs d'instance et typage complet |
| Communication | Participants et messages | Présence de messages, avertissement de numérotation | Ordonnancement complet et représentation graphique de la numérotation |
| Timing | Lifelines, états invariants, contraintes/observations de temps | Présence de lifeline, conseils sur contraintes temporelles | Axes temporels, notations et validation temporelle exacte |
| CompositeStructure | Classes, parts, ports, connecteurs, collaborations | Présence d'éléments structurants et avertissements | Connecteurs typés, rôles et délégation complets |
| InteractionOverview | Flux, décisions et références d'interaction | Nœud initial, interaction use, sorties de décision | Sémantique complète des fragments et des références |
| Profile | Profils, stéréotypes, métaclasses, extensions | Présence de stéréotype, métaclasse étendue | Profil UML conforme, extension formelle, valeurs taguées typées et contraintes |

## Modèle sémantique du projet

Le modèle inclut désormais des types pour :

- paquetages, propriétés, opérations, paramètres, contraintes, stéréotypes et valeurs taguées ;
- interfaces, énumérations/littéraux, acteurs et cas d'utilisation ;
- composants, interfaces fournies/requises, nœuds de déploiement et artefacts ;
- signaux, machines à états, états, transitions ;
- activités, nœuds et flux ;
- interactions, lignes de vie, messages et fragments combinés ;
- types primitifs et catalogue navigable de grandes familles de métaclasses UML 2.5.1.

Le catalogue de métaclasses est un manifeste de couverture et de travail restant ; il ne signifie pas que chaque classe du standard dispose déjà d'une représentation complète.

## Règles projet actuelles

Les identifiants `UMLPxxxx` couvrent notamment les noms de types, doublons dans un namespace, combinaisons abstract/sealed, propriétés/opérations dupliquées, types et noms invalides, multiplicités, visibilités, directions de paramètres, relations orphelines, cycles de généralisation, paquetages, stéréotypes et contraintes vides.

Les règles `UMLxxxx` sont les contrôles de diagramme : noms et identifiants, types d'éléments, relations, gardes, multiplicités, présence des éléments essentiels et contrôles propres à certaines familles. Les contrôles projet couvrent aussi plusieurs références inter-éléments dans les cas d'utilisation, machines à états, activités et interactions.

## Limites et garanties

- Les contrôles sont des règles de cohérence UMLForge, **pas une certification OMG**.
- Un évaluateur OCL sûr est intégré pour un sous-ensemble documenté (littéraux, propriétés de `self`, comparaisons, logique, arithmétique et opérations de base sur collections). Il ne couvre pas l'ensemble de la norme OCL.
- Le catalogue ne remplace pas les métaclasses UML 2.5.1, leurs associations, contraintes et sémantiques normatives.
- Les éditeurs graphiques spécialisés, la sérialisation XMI complète, les profils formels et les règles OMG exhaustives restent à implémenter.
- Avant une déclaration de conformité, il faudra définir un métamodèle versionné, des fixtures de référence et une suite de tests traçable règle par règle.


## Interprétation de la couverture

Le catalogue est un inventaire de travail élargi destiné à rendre visibles les familles de métaclasses. Une entrée dans le catalogue ne signifie pas que la métaclasse possède une représentation complète, toutes ses relations normatives, ses notations, sa sérialisation XMI et ses règles de validation. Les statuts doivent être lus comme un manifeste de couverture, pas comme une déclaration de conformité OMG.
