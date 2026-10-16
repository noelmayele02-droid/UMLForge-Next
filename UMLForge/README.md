# UMLForge Next — application Windows

Ce dossier contient le projet WPF de l'application. Pour la présentation complète, les prérequis, la documentation, les tests et les limites actuelles, consultez le [`README.md`](../README.md) à la racine du dépôt.

## Compilation rapide

Depuis ce dossier, avec le SDK .NET 10 installé :

```powershell
dotnet restore UMLForge.sln
dotnet build UMLForge.sln -c Release
dotnet test UMLForge.sln -c Release
```

La compilation et les tests Windows doivent réussir avant toute publication présentée comme stable.
