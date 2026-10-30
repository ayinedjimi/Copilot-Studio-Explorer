# Copilot Studio Explorer

Auteur : **Ayi NEDJIMI Consultants**.

# Copilot Studio · Démonstration Dataverse

Application Windows native **WinUI 3 / C#**, avec un accueil reproduit à partir de la capture fournie : navigation latérale, bandeau, trois cartes et autres méthodes de génération. Les illustrations sont recréées en SVG. Ce démonstrateur indépendant n’est pas un produit Microsoft. Le panneau des appels s’ouvre avec **Appels Dataverse** en bas à gauche ; la connexion est dans **Liste des agents**.

## Écrans de référence et composant de carte

- **Accueil** : page de départ avec trois cartes et illustrations SVG.
- **Vue d’ensemble** : bandeau illustré, indicateurs, actions et quatre graphiques, d’après la capture Agent 365. Les indicateurs non fournis sont représentés par un tiret.
- **Tous les agents** : registre compact, filtres en pastilles et dans les colonnes, menu à côté du nom, colonnes de plateforme/risque/utilisation/partage/date. Les données d’utilisation proviennent des échantillons chargés depuis les fiches ; les champs de risque, de partage ou d’usage non mesurés restent indisponibles.
- **Analyses** : les quatre graphiques sont aussi disponibles dans un écran dédié.
- **Carte d’agent** : véritable **React Flow (@xyflow/react)** embarqué dans WinUI 3 via **WebView2**, avec filtres flottants, groupes circulaires, zoom, déplacement, sélection et mini-carte. Les nœuds sont déplaçables pour l’exploration, sans modification Dataverse. Les positions ne sont pas sauvegardées. Aucun graphe de dépendances n’est inventé.

La carte ne télécharge pas de scripts depuis un CDN. Les fichiers HTML/CSS/JS sont dans `Assets/AgentMap`, avec les notices de licences. Le Runtime Microsoft Edge WebView2 doit être installé ; un message explicite est affiché s’il manque. L’authentification reste gérée par MSAL dans le navigateur système : aucun jeton n’est envoyé au composant web. Le pont natif n’accepte que l’origine locale et des identifiants d’agents déjà chargés.

Source frontend : `MapWeb/`. Pour reconstruire la carte : `npm ci --ignore-scripts`, puis `npm run build` et `npm test`. Le projet C# copie ensuite les fichiers produits dans le dossier de l’exécutable. React Flow : https://reactflow.dev/ ; intégration WebView2 WinUI : https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/winui3-windows-app-sdk

Les éléments visuels ont été recréés d’après les captures. La compilation ne constitue pas une validation au pixel près ; l’application n’est pas lancée automatiquement et le bureau n’est pas contrôlé.

## Démarrer

Avec Windows 10 2004 minimum ou Windows 11 et le SDK .NET 8 ou supérieur :

```powershell
dotnet build StudioDataverse.csproj -p:Platform=x64
dotnet run --project StudioDataverse.csproj -p:Platform=x64
```

## Connexion Entra à configurer

L’application est préconfigurée pour :

- Organisation : https://org86c36b58.crm17.dynamics.com
- Web API : https://org86c36b58.crm17.dynamics.com/api/data/v9.2/
- Environnement : 96e7335b-3367-efa8-a70f-ea65680c1a5c
- Tenant : 8ebfeed3-cf5d-4d41-b2ed-35f5369dda1c
- Client public : ff6816da-a957-4e33-ba4a-23b15d60b774
- Scope délégué (client Windows public) : https://org86c36b58.crm17.dynamics.com/user_impersonation

Dans l’inscription Entra de cette application, ajouter la plateforme **Applications mobiles et de bureau**, avec la redirection **http://localhost** pour le navigateur système. Ajouter l’autorisation déléguée **Dynamics CRM / user_impersonation** et accorder le consentement requis par les règles du tenant. Le compte connecté doit disposer des droits Dataverse nécessaires sur les agents et leurs composants. Les rôles Dataverse continuent de s’appliquer.

La connexion utilise MSAL et le navigateur Microsoft, sans secret client. Les jetons restent dans le cache mémoire de MSAL pour la durée du processus ; ils ne sont ni affichés ni écrits dans le journal. Aucun mot de passe n’est demandé dans l’application.

## Parcours de démonstration

1. Cliquer sur **Se connecter** en haut de la barre latérale gauche, puis terminer la connexion et le consentement dans le navigateur. Le compte s’affiche en bas de la barre latérale ; son menu permet de changer de compte ou de se déconnecter. La connexion est annulable et expire après trois minutes.
2. Ouvrir **Liste des agents**. Les agents sont chargés par pages de 100 ; **Charger davantage** apparaît si Dataverse retourne une page suivante. Les filtres et tris portent sur les agents déjà chargés. Le panneau **Appels Dataverse** affiche les GET réels.
3. Sélectionner un agent de démonstration. Noter son nom original.
4. Changer son nom puis cliquer sur **Enregistrer le nom dans Dataverse**. Le PATCH réel et le GET de vérification apparaissent à droite. Une ETag évite d’écraser une modification concurrente si Dataverse la prend en charge pour cette table ; un HTTP 412 nécessite de recharger les agents.
5. Actualiser Copilot Studio dans le même environnement et vérifier le nom. Restaurer ensuite le nom d’origine avec le même parcours.
6. Cliquer sur un agent pour ouvrir le panneau de droite. L’onglet **Aperçu** présente les colonnes retournées par Dataverse et leurs libellés formatés. Les longs contenus sont abrégés dans l’aperçu et disponibles intégralement dans **JSON**. L’image Base64 est omise de l’aperçu. Les champs dépendent des droits du compte et des données de l’environnement.
7. Cliquer sur **Voir les composants** : les 100 premiers composants sont présentés dans l’onglet **Composants**, avec une recherche locale. Déplier un composant charge ses informations complètes. Les identifiants et le JSON de l’agent sont copiables.

## Recherche, filtres et export

Les filtres et tris sont intégrés aux en-têtes du tableau : **Nom** pour le tri alphabétique ou la création récente, **État**, **Langue**, **Publication**, **Solution** pour les filtres, et **Modification** pour la période et l’ordre chronologique. Un indicateur coloré signale un filtre actif. Le défilement horizontal conserve toutes les colonnes sur les petites fenêtres. Le menu contextuel est également accessible avec le bouton **…** sur chaque ligne.

Les icônes PNG des agents sont chargées depuis le champ Dataverse `iconbase64`. En cas d’image absente, invalide ou non décodable, l’initiale est affichée. Les icônes ne nécessitent aucun téléchargement externe.

Un clic droit sur une ligne ouvre son menu contextuel. **Voir les transcriptions** est toujours le premier élément et récupère les conversations de cet agent, indépendamment de la ligne précédemment sélectionnée. Le menu propose aussi les détails, les favoris, la copie de l’identifiant et du schéma ainsi que le choix d’une référence de comparaison.

- Recherche multi-mots dans le nom, le schéma et l’identifiant.
- Filtres combinables : état actif/inactif, présence d’une date de publication, modifications sur 7/30/90 jours, langue et solution managée/non managée.
- Le filtre **Déjà publiés** signifie qu’une date `publishedon` est renseignée ; il ne garantit pas que les derniers changements ont été publiés.
- Tris : modification récente/ancienne, nom croissant/décroissant, création récente.
- Réinitialisation des filtres et compteurs des résultats, des actifs et des agents ayant une date de publication.
- Export CSV des résultats filtrés et triés, via un sélecteur de fichier Windows. Format UTF-8 avec BOM et séparateur point-virgule ; les cellules pouvant être interprétées comme des formules sont protégées.

Les erreurs d’accès et réponses HTTP sont visibles. Le journal contient des données réelles de l’environnement et reste en mémoire (40 entrées maximum). Il ne capture pas le trafic du site Copilot Studio : il montre uniquement les appels de cette application. Aucun mode de démonstration ne fabrique de réponse.

La réussite de la connexion Microsoft et l’accès aux tables Dataverse sont affichés séparément. Si la lecture des agents échoue (par exemple 403), le compte reste connecté et un message indique le problème d’accès ; le panneau API s’ouvre avec la réponse. Les erreurs de connexion affichent le code AADSTS et l’identifiant de corrélation, sans enregistrer de jeton.

## Favoris, historique et comparaison

- Marquer un agent avec **☆ Favori** dans son panneau, puis choisir **Mes favoris** dans le filtre Vue.
- **Consultés récemment** présente les 30 derniers agents consultés, parmi les agents chargés. La liste est ordonnée par consultation récente.
- Favoris et identifiants de l’historique sont enregistrés localement dans `%LOCALAPPDATA%\StudioDataverse`, dans un fichier séparé par compte. Aucun jeton, nom d’agent ou contenu Dataverse n’est enregistré dans ces préférences.
- Choisir un agent comme **référence**, ouvrir un second agent puis cliquer sur **Comparer**. Le dialogue relit les deux fiches Dataverse et affiche les valeurs côte à côte, avec une option limitée aux différences. Les images Base64 sont exclues et les valeurs longues sont abrégées à 600 caractères dans le comparateur.
- **Copier la fiche** prépare un résumé de l’agent dans le presse-papiers.
- Raccourcis : Ctrl+F recherche, Ctrl+R actualisation, Ctrl+D ajout/retrait du favori sélectionné.

## Page Analyses

L’entrée **Analyses** reprend les quatre cartes de la capture : histogramme par créateurs, barres par plateforme, utilisateurs actifs dans le temps, classement des agents par utilisateurs identifiés.

- Créateurs : cinq premiers noms retournés par Dataverse via `_createdby_value`, sur les agents chargés ; les catégories organisation/partenaires/Microsoft ne sont pas inférées.
- Plateformes : cette source ne contient que Copilot Studio, sans inventaire Microsoft 365/Agent 365 externe.
- Activité : bouton **Actualiser les analyses**, lecture des 100 derniers enregistrements de transcription sur 30 jours. Identifiants `from.id` distincts par jour, dans le fuseau Europe/Paris ; les identifiants manquants ne sont pas comptés. Le graphique laisse des lacunes pour les jours sans observation. Le classement affiche les cinq agents avec le plus d’identifiants distincts dans cet échantillon.
- Disposition sur quatre cartes côte à côte pour les grandes fenêtres, deux par ligne sinon. Données de conversation effacées à la déconnexion et au changement de compte. Aucun chargement externe ni chiffre fictif.

## Carte et onglets d’agent

Le module **Agent Map** propose une carte des agents chargés, inspirée de la capture fournie : filtres dans une carte flottante à gauche, outils en haut à droite et groupes circulaires avec icônes. Recherche, filtres repliables d’état/solution/publication, regroupement par plateforme/état/langue/solution, zoom, taille réelle et recentrage automatique. Le panneau de filtres est masquable. Les icônes ouvrent la fiche ; le clic droit propose les actions de l’agent. Jusqu’à 100 agents sont représentés par groupe, avec le nombre affiché explicitement. Les indicateurs de risque et d’absence de propriétaire apparaissent comme indisponibles (tiret). Le compteur de solutions non managées correspond à `ismanaged=false` dans les agents chargés, et ne représente pas le statut Agent 365 « non géré ». Les liens entre agents, plateformes externes et licences ne sont pas supposés. Le registre affiche le total communiqué par Dataverse quand il est retourné et distingue les compteurs sur les données chargées.

Le panneau droit comporte **Général**, **Composants**, **Utilisation**, **Erreurs**, **Performances** et **JSON**.

- Général : composants chargés automatiquement avec voyant vert si `statecode=0`, rouge si `statecode=1`, gris si l’état n’est pas renseigné. Il s’agit de l’état Dataverse, pas d’une garantie que l’outil est publié ou correctement configuré.
- Composants : chargement par la relation parent de l’agent, avec recherche dans la relation liée si aucune ligne n’est retournée ; état de chargement, résultat vide et erreur/reprise explicites. Les 100 premiers composants sont affichés.
- Utilisation : analyse des 50 derniers enregistrements de transcription sur 30 jours pour l’agent sélectionné. Messages utilisateur/agent, conversations distinctes par nom et utilisateurs identifiés à partir de `from.id`.
- Erreurs : activités de type error/exception et charges utiles comportant une erreur explicite `DialogErrorDetail`/`exception`. Le simple mot « error » dans un message n’est pas une erreur.
- Performances : médiane et 95e percentile du délai entre un message utilisateur et la première réponse agent suivante, sur les paires horodatées de l’échantillon. Les délais négatifs ou supérieurs à une heure sont exclus. Ce n’est pas la durée interne du modèle. Coûts, crédits, SLA et durées internes sont indiqués comme indisponibles.

## Récupération des transcriptions

L’entrée **Transcriptions** dans la barre latérale récupère les enregistrements réels de la table `conversationtranscripts` avec le compte connecté. Un bouton **Voir les conversations** dans la fiche d’un agent préselectionne cet agent.

- Filtres serveur par agent et intervalle de dates (date de fin incluse). Cliquer sur **Récupérer** après avoir modifié les filtres. Pagination par pages de 100 ; **Charger davantage** suit la page suivante du même filtre.
- La liste charge uniquement les identifiants, noms, dates et métadonnées. Le contenu est récupéré à la sélection d’un enregistrement.
- Le lecteur distingue les utilisateurs des agents à partir du rôle déclaré dans les activités. Un rôle absent apparaît comme **Participant**. La recherche porte sur les messages chargés ; une case permet d’inclure les événements techniques.
- Les cartes/pièces jointes sont signalées ; elles ne sont ni téléchargées ni exécutées. Les activités et leurs pièces jointes restent présentes intégralement dans l’onglet JSON et l’export JSON.
- **Tous les fragments** récupère les enregistrements de même nom, heure de début et agent, suit la pagination (limite 200 fragments), puis les ordonne par `Metadata.BatchId`. Les activités ayant le même ID sont dédoublonnées ; les textes identiques dépourvus d’ID ne sont pas fusionnés.
- **Exporter** propose un JSON intégral contenant les fragments, leurs identifiants, contenus et métadonnées, ou un texte lisible de toutes les activités chargées. Les filtres d’affichage des messages ne tronquent pas l’export. Un format non reconnu reste exportable en JSON.
- Aucune transcription n’est sauvegardée automatiquement sur disque ; les exports sont explicites. Les réponses apparaissent aussi dans le journal API en mémoire. La déconnexion et le changement de compte effacent les transcriptions chargées et le journal.

Si Dataverse répond 403, un administrateur doit vérifier les droits de lecture sur ConversationTranscript et le rôle **Bot Transcript Viewer**. Une liste vide peut aussi provenir de la période, de la rétention, de l’absence de collecte ou du type d’environnement. Les conversations peuvent n’être enregistrées qu’après une période d’inactivité.

Références :

- https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/conversationtranscript?view=dataverse-latest
- https://learn.microsoft.com/microsoft-copilot-studio/analytics-transcripts-powerapps

## Limites fonctionnelles

Une interface alternative peut lire et modifier les données de conception et consulter les transcriptions exposées dans Dataverse. Elle ne prouve pas que toutes les fonctionnalités du site Copilot Studio reposent exclusivement sur Dataverse. La génération à partir de texte, l’exécution des conversations et la publication ne sont pas implémentées.

Références Microsoft :

- https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/bot?view=dataverse-latest
- https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/botcomponent
- https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration
- Référence visuelle : https://learn.microsoft.com/en-us/microsoft-copilot-studio/fundamentals-get-started

