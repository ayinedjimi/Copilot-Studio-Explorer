# Livraison finale

Le raccourci principal pointe désormais vers `../Application/StudioDataverse.exe`. Les dossiers `version-*` et `release` sont d’anciennes compilations ; ils ne sont plus utilisés par ce raccourci et sont exclus de la publication.

Voir `../DEMARRER.md` pour le guide et le périmètre réel. La version finale conserve les fonctions demandées, les écrans de référence recréés et le composant React Flow embarqué. Aucune affirmation de validation visuelle au pixel près n’est faite sans exécution.

Reconstruction depuis les sources :

```powershell
cd MapWeb
npm ci --ignore-scripts
npm run build
npm test
cd ..
dotnet publish StudioDataverse.csproj -c Release -p:Platform=x64 -o ../Application
```

Les tests C# sont dans `../../work/AgentQueryChecks` par rapport à ce projet. Ils ne nécessitent pas de compte Dataverse et ne font aucune requête réseau vers le tenant.
