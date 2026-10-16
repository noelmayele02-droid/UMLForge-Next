# Checklist de référence avant publication

## Validation Windows locale

Depuis PowerShell à la racine du dépôt :

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Scripts\Validate-Windows.ps1
```

Pour produire le dossier de publication après restore, build et tests réussis :

```powershell
.\Scripts\Validate-Windows.ps1 -Publish
```

Le script s'arrête au premier échec. Ne pas publier un binaire si restore, build ou tests échouent.

## Scénarios manuels obligatoires

- [ ] Démarrer l'application WPF sous Windows 10/11 et vérifier qu'aucune exception n'apparaît au démarrage.
- [ ] Ouvrir/créer un projet, créer les principaux diagrammes, enregistrer, fermer et rouvrir le modèle.
- [ ] Mapper un modèle avec classe, propriété, opération, interface, énumération et littéraux vers une vraie solution C#.
- [ ] Vérifier une classe `partial`, un renommage non ambigu et un cas ambigu qui doit rester non résolu.
- [ ] Lancer une synchronisation avec conflit et vérifier qu'aucun fichier manuel n'est écrasé sans décision explicite.
- [ ] Tester diagnostics, complétion, signature help, imports et actions de code sur une solution .NET réelle.
- [ ] Tester le débogueur avec l'adaptateur DAP documenté pour le projet.
- [ ] Vérifier que les sauvegardes `.bak-*` et l'historique sont créés et utilisables.
- [ ] Contrôler le workflow GitHub Actions Windows : restore, build et tests verts.
- [ ] Revoir README, changelog, version et notes de limitations avant de créer un tag de version.

## Règle de publication

La publication GitHub est bloquée tant que les contrôles automatisés ne passent pas et que les scénarios manuels critiques ne sont pas vérifiés. La réussite d'une compilation ne signifie pas une parité complète avec Visual Studio Community, StarUML ou Visual Paradigm, ni une conformité exhaustive à UML 2.5.1.
