# Paramètres de Copilot Studio Explorer

La page **Paramètres** permet de modifier le nom affiché de l’environnement, l’URL Dataverse, les identifiants publics du tenant et de l’application Entra, ainsi que le délai des requêtes Dataverse. L’URL doit être une URL racine HTTPS Dynamics prise en charge et le compte doit disposer des droits nécessaires.

Elle permet également de choisir la période initiale des transcriptions, la période et la taille de l’échantillon d’incidents, la région de canal par défaut des scénarios, la largeur de la barre latérale, le journal des erreurs, le modèle local par défaut, le nombre initial de questions, le contexte, le nombre maximal de tokens, le délai et les threads CPU du moteur local.

Les paramètres sont enregistrés dans `%LOCALAPPDATA%/CopilotStudioExplorer/settings.json`. Ils ne contiennent aucun secret ni jeton. Les nouvelles valeurs prennent effet au prochain démarrage ; la session en cours conserve sa configuration. Si le fichier est invalide, la connexion est bloquée jusqu’à la correction et au redémarrage.

Les favoris et les données de gouvernance sont isolés par compte et par environnement. Les préférences existantes de l’environnement de formation sont conservées. Les modèles GGUF restent dans leurs dossiers locaux et ne sont pas intégrés au fichier de paramètres.

La sélection de température et des autres paramètres d’échantillonnage du modèle n’est pas disponible dans la version actuelle de la DLL. Les limites de sécurité du téléchargeur et certains plafonds d’affichage restent fixes. Les périodes utilisées par les analyses d’organisation ne sont pas modifiées par la période initiale des transcriptions.
