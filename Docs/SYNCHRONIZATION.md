# Synchronisation UML ↔ C#

## Principes de sécurité

1. Préparer un plan sans écrire de fichier.
2. Comparer l'état de référence (baseline), le contenu sur disque et le contenu généré.
3. Détecter les modifications manuelles et les conflits avant application.
4. Présenter les décisions à l'utilisateur ; les conflits ne sont pas acceptés par défaut.
5. Vérifier que les fichiers n'ont pas changé depuis l'aperçu.
6. Créer des sauvegardes et conserver un historique des opérations.

## Fusion à trois voies

Le service de fusion syntaxique peut combiner certains changements indépendants des membres directs d'un type C#. Les modifications concurrentes d'un même membre sont traitées prudemment. Les suppressions générées et certains renommages/déplacements sont signalés comme opérations distinctes, pas déduits silencieusement comme vérité certaine.

## État des identités

Le modèle UML a des GUID persistants pour les classes, diagrammes, éléments, propriétés, opérations, paramètres et autres métadonnées. L'action **Mapper UML ↔ C#** et `UmlCodeMappingService` enregistrent les GUID des classes, propriétés, opérations, interfaces, énumérations/littéraux et quelques types/éléments supplémentaires vers les symboles Roslyn compatibles. Le mapping conserve le projet, le fichier source et une empreinte structurelle, traite les symboles de classes `partial` agrégés par Roslyn et tente une reconnaissance prudente de certains renommages. Les correspondances ambiguës sont signalées plutôt que choisies au hasard. Une sauvegarde du modèle est créée avant la mise à jour. La couverture n’inclut pas toutes les métaclasses et relations UML et ne remplace pas encore un `SymbolKey` versionné pour tous les cas.

Les noms seuls ne sont pas une clé suffisante : namespaces, signatures, types génériques, overloads et fichiers `partial` rendent l'identité sémantique plus complexe.

## Prochaines garanties à ajouter

- Manifeste de correspondance UML ID ↔ clé de symbole Roslyn versionné.
- Fusions atomiques multi-fichiers avec rollback éprouvé.
- Règles explicites pour renommage de type/membre, déplacement de namespace, suppressions et conflits de structure.
- Conservation des commentaires, directives, régions, attributs, formats et trivia syntaxiques.
- Tests de concurrence, interruption, accès refusé, disque plein et restauration.
- Aperçu détaillé de toutes les opérations et export de rapport de synchronisation.

Ne jamais activer un mode d'écrasement global par défaut. Quand la fusion est ambiguë, préférer le refus et la résolution manuelle à une modification destructive.
