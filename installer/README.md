# Installeur Windows

L’installeur Inno Setup 6 installe Copilot Studio Explorer pour Windows x64. Il propose une installation pour l’utilisateur courant, un raccourci dans le menu Démarrer, un raccourci de bureau facultatif et une désinstallation Windows. Les écrans de l’installeur sont disponibles en français et en anglais ; l’application reste en français.

Après publication de l’application depuis la racine du dépôt :

```powershell
dotnet publish outputs/StudioDataverse/StudioDataverse.csproj -c Release -p:Platform=x64 -o outputs/Application
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer/CopilotStudioExplorer.iss
```

Par défaut, la sortie est créée dans `installer/output/`. Pour utiliser un autre dossier de publication, transmettre `/DSourceDir=C:\chemin\vers\Application` à ISCC. Fermer manuellement toute instance installée avant une mise à jour ou une désinstallation. Les données locales de l’utilisateur ne sont pas supprimées à la désinstallation.
