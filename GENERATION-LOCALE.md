# Génération locale et conversation

Les modules « Jeux de questions » et « Chat local » utilisent la DLL `lamazero.dll` et un modèle GGUF local. Le modèle de la distribution de test est un petit modèle compatible ChatML ; ce n’est pas un modèle GPT. Il occupe environ 491 Mo et sa qualité doit être vérifiée, notamment pour les questions en français.

- La génération des questions utilise les informations déjà analysées de l’agent sélectionné. Il faut donc lancer son analyse avant de générer.
- Le chat fonctionne sans connexion au service Dataverse. Le contexte de l’agent n’est inclus que si la case correspondante est cochée et si l’agent a déjà été analysé.
- Les échanges du chat restent en mémoire pour la session et sont effacés à la déconnexion ou par « Nouvelle conversation ». La génération s’effectue sur le processeur du poste, avec arrêt disponible.
- Les réponses sont des propositions à relire ; elles ne constituent ni des faits vérifiés ni des résultats de tests.
- Les modules nécessitent Windows x64, AVX2 et le runtime Visual C++ indiqué dans [la documentation du moteur](outputs/StudioDataverse/Native/README.md). L’interface du chat utilise WebView2 et le composant [Chat UI Kit React](https://github.com/chatscope/chat-ui-kit-react), sous licence MIT.
- Les modèles GGUF ne sont pas inclus dans le dépôt Git. L’application détecte les modèles disponibles dans son dossier `models` et dans `%LOCALAPPDATA%/CopilotStudioExplorer/Models`. Ils se choisissent directement dans la liste du chat ou du module de questions. Le format de conversation est choisi selon le modèle.
- Le modèle Gemma 2 2B provient de [cette distribution GGUF](https://huggingface.co/gaianet/gemma-2-2b-it-GGUF), pèse environ 1,7 Go et est soumis aux [conditions Gemma](https://ai.google.dev/gemma/terms). Le fichier et son empreinte sont indiqués dans le dossier `models` de la distribution.
- Le modèle Gemma 4 E2B utilise le fichier GGUF vérifié provenant de [cette distribution](https://huggingface.co/unsloth/gemma-4-E2B-it-GGUF) ; ses conditions et son empreinte accompagnent le fichier dans le dossier local des modèles.
- Le chat propose aussi le téléchargement de fichiers GGUF publics depuis Hugging Face : dépôt et fichier à la racine, choix explicite du format, limite de 8 Go, vérification SHA-256 avant ajout. Les dépôts privés ou soumis à autorisation ne sont pas pris en charge. Vérifiez les conditions applicables à chaque modèle.
- Les modules Analyse IA et Sécurité IA résument localement les données déjà collectées pour l’agent sélectionné ; leurs résultats sont à vérifier et ne remplacent pas les contrôles standard.
