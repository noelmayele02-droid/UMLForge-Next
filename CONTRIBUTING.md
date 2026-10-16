# Contribuer à UMLForge Next

Merci de contribuer à un atelier de conception et de développement plus cohérent. Les contributions doivent rester vérifiables et préserver le code de l'utilisateur.

## Avant de commencer

- Lire `README.md`, `Docs/ARCHITECTURE.md` et le document de domaine concerné.
- Ouvrir une issue pour une fonctionnalité importante ou un changement de format de données.
- Ne pas annoncer la conformité UML ou la parité avec un IDE sans tests reproductibles.

## Règles de contribution

1. Une modification fonctionnelle doit inclure des tests lorsque c'est raisonnablement possible.
2. Toute écriture multi-fichiers doit prévoir aperçu, sauvegarde, contrôle de concurrence et traitement d'échec.
3. Les règles UML doivent avoir un identifiant stable, un message clair et au moins un test positif/négatif.
4. Les appels Roslyn doivent gérer les projets non restaurés, documents sans chemin et annulations.
5. Le code DAP doit tolérer les fonctionnalités non prises en charge par l'adaptateur.
6. Les messages et la documentation utilisateur sont rédigés en français clair, sans exagérer la couverture.

## Vérification locale

```powershell
dotnet restore UMLForge/UMLForge.sln
dotnet build UMLForge/UMLForge.sln -c Release
dotnet test UMLForge.Tests/UMLForge.Tests.csproj -c Release
```

Les modifications de l'interface WPF doivent être vérifiées sur Windows avec le SDK .NET cible.
