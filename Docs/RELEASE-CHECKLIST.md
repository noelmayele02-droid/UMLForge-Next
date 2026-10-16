# Checklist de publication GitHub

## Avant le premier push

- [ ] Lire `README.md` dans l'aperçu GitHub et vérifier le rendu du bandeau SVG et du diagramme Mermaid.
- [ ] Vérifier que le dépôt ne contient aucun secret, clé API, certificat privé, fichier utilisateur, projet client ou résultat de build.
- [ ] Confirmer le choix de licence avant de rendre le dépôt public.
- [ ] Confirmer le nom final du dépôt, sa description et les topics.
- [ ] Installer .NET 10 sous Windows et exécuter `Scripts/build.ps1`.
- [ ] Corriger toutes les erreurs de compilation et tous les tests échoués.
- [ ] Tester l'application et ses principaux parcours manuellement.
- [ ] Vérifier les instructions de lancement, la configuration du DAP et la génération de publication.
- [ ] Relire `Docs/STATUS-REAL.md` pour s'assurer qu'il reflète les tests réellement exécutés.

## Après le push

- [ ] Vérifier que l'action Windows Build and Tests est verte.
- [ ] Consulter les logs et publier les résultats, y compris les échecs connus.
- [ ] Créer les issues des travaux P0/P1 de la roadmap.
- [ ] Ne publier un binaire de release qu'après une compilation reproductible.
- [ ] Ne revendiquer la conformité ou la parité qu'après des tests de couverture traçables.
