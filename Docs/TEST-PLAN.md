# Plan de test UMLForge Next

## 1. Vérification automatique sur Windows

```powershell
dotnet restore UMLForge/UMLForge.sln
dotnet build UMLForge/UMLForge.sln -c Release
dotnet test UMLForge/UMLForge.sln -c Release --logger "console;verbosity=normal"
```

Le workflow `.github/workflows/windows-build.yml` exécute les mêmes étapes sur un runner Windows. Toute erreur de compilation doit être corrigée avant d'interpréter les tests comme un résultat valide.

## 2. Tests UML

- Noms de classes et namespaces valides/invalides.
- Types dupliqués dans un même namespace.
- Multiplicités valides (`1`, `0..1`, `*`, `0..*`, `2..5`) et invalides (`4..2`, `-1`, chaîne vide).
- Relations vers des éléments inexistants.
- Cycles de généralisation et auto-héritage.
- Propriétés/opérations dupliquées et paramètres mal formés.
- Stéréotypes sans métaclasse, paquetages avec références orphelines et contraintes vides.
- Présence des éléments requis par famille de diagramme.
- Références d'acteurs/cas d'utilisation, transitions/états, flux/nœuds d'activité et messages/lignes de vie.
- OCL : opérateurs logiques, comparaisons, propriétés `self`, collections, expressions invalides et contraintes attachées à une instance.
- Export PlantUML/JSON puis réouverture sans perte de propriétés essentielles.

## 3. Tests Roslyn / éditeur

- Charger une solution mono-projet et multi-projets.
- Charger un projet non restauré et présenter les erreurs de workspace clairement.
- Complétion dans un contexte valide, sans résultat et avec références manquantes.
- Signature help pour appels surchargés et constructeurs.
- Suggestions d'import avec types provenant de projets source, du framework et de packages NuGet.
- Actions de code : formatage, tri des imports, ajout d'import et nettoyage des `using` inutilisés.
- Diagnostics par fichier, clic sur diagnostic et navigation à la bonne ligne/colonne.
- Insérer une suggestion par clavier et souris sans remplacer le mauvais texte.
- Navigation vers une définition dans le même fichier et un autre fichier.
- Recherche de références avec overloads et types `partial`.
- Renommage d'une propriété/méthode/type et confirmation de la sauvegarde multi-fichiers.
- Tri `using` avec alias, directives globales et commentaires attachés.
- Fermeture de workspace, annulation et réouverture d'une solution.

## 4. Synchronisation

- Aucun changement ; changement généré uniquement ; changement manuel uniquement.
- Modifications indépendantes de membres distincts ; modification simultanée du même membre.
- Ajout/suppression/renommage de type et de fichier.
- Fichiers `partial`, namespace déplacé, overloads et types génériques.
- Fichier modifié entre aperçu et application : aucune écriture partielle ne doit être appliquée.
- Accès refusé, disque plein, exception pendant l'écriture et restauration depuis backup.
- Chemin de sortie hors racine, chemin relatif malveillant et noms de fichiers inhabituels.

## 5. Débogueur DAP

- Lancement réussi, adaptateur manquant, DLL inexistante, PDB manquant.
- Breakpoint simple, condition valide/invalide, ligne sans séquence exécutable.
- Continue, pause, next, step-in, step-out et arrêt.
- Stack trace, scopes, variables, évaluation Watch et threads.
- Adaptateur qui refuse une commande ou n'implémente pas une capacité optionnelle.
- Processus qui se termine, pipe fermé et délai d'attente dépassé.

## 6. Test manuel d'interface

- Redimensionnement de toutes les fenêtres, thème, focus clavier, tab order et erreurs visibles.
- Grand projet avec plusieurs centaines de fichiers.
- Fichiers longs, caractères Unicode, chemins avec espaces et accents.
- Annulation de dialogue et fermeture avec modifications non enregistrées.
- Test de récupération de sauvegardes et comparaison du fichier original.

## État actuel

Des tests unitaires de fusion et de validation UML sont fournis. Le plan ci-dessus est plus large que la suite automatisée existante. Les tests de l'interface, de DAP et de restauration multi-fichiers restent à effectuer sur Windows.
