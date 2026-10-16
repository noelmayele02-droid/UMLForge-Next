# Politique de sécurité

## Signaler une vulnérabilité

Ne publiez pas publiquement de détails exploitables sur une vulnérabilité non corrigée. Contactez le mainteneur du dépôt par un canal privé configuré dans les paramètres GitHub du projet. Tant qu'aucun canal privé n'est publié, évitez d'envoyer des données confidentielles ou des projets propriétaires dans une issue publique.

## Principes de sécurité du projet

- Les changements UML ↔ C# doivent être prévisualisés et les conflits ne doivent pas être écrasés silencieusement.
- Les chemins de sortie doivent rester dans le projet autorisé.
- Les opérations multi-fichiers doivent sauvegarder les contenus avant écriture.
- Les adaptateurs DAP sont des exécutables externes : n'exécutez qu'un adaptateur que vous avez choisi et auquel vous faites confiance.
- Les projets et scripts ouverts peuvent exécuter du code arbitraire via les commandes de build/test/run. Vérifiez la provenance avant de les lancer.
