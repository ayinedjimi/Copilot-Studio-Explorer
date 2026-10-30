# État des modules — revue du code

Cette revue décrit le périmètre constaté dans le code. Elle ne remplace pas une validation dans un environnement réel ; les droits Dataverse et les données accessibles influencent les résultats.

| Module | État et limites constatées |
| --- | --- |
| Audit de sécurité | Les contrôles statiques existent. DLP, consentements Entra, droits effectifs des connaissances et protections d’exécution ne sont pas reliés aux sources administratives et restent non vérifiables. |
| Dépendances | Graphe de références déclarées, pas de traçage effectif des appels en exécution. Les objets inaccessibles sont signalés ; une référence ne prouve pas qu’une connexion fonctionne. |
| Historique et évolution des risques | Instantanés locaux ; pas de sauvegarde centralisée ni de surveillance automatique. L’évolution compare les constats enregistrés localement. |
| Incidents | Analyse d’échantillons de transcriptions et de runs accessibles, avec limites de période et de volume ; pas de connexion aux journaux externes. |
| Gouvernance et dossiers | Annotations et dossiers locaux, partage manuel par export/import ; pas de collaboration en temps réel ni de modification du propriétaire Dataverse. |
| Rapports | HTML et CSV disponibles ; aperçu et PDF dépendent de WebView2 et nécessitent une validation sur le poste. Les résultats portent sur les agents analysés et incidents chargés. |
| Solutions | Les 300 premières solutions filtrées et les 500 premiers composants de la solution ouverte sont affichés. Les objets non reconnus sont présentés par identifiant ; les dépendances externes ou indirectes restent à vérifier. |
| Environnements | Comparaison directe limitée aux environnements du même tenant. Les variables référencées sont comparées par nom et type, sans lecture de leurs valeurs. Les sources inaccessibles sont exclues. |
| Recherche globale | Recherche seulement dans les agents déjà analysés et leurs données masquées ; 300 premiers résultats affichés. Ce n’est pas une recherche globale sur toutes les tables Dataverse. |
| Conversations avancées | Échantillon borné à 50 enregistrements par chargement. Le masquage est heuristique et ne garantit pas l’anonymisation complète. Pas d’analyse exhaustive des conversations. |
| Règles personnalisées | Règles textuelles locales sur quatre champs ; pas de moteur de règles avancé ni de partage automatique. Les contrôles standard restent séparés. |
| Diagnostic | Vérifie la session avec WhoAmI et présente des indications sur les erreurs courantes ; ne vérifie pas tous les rôles ni les droits de chaque table. |
| Santé | Tableau limité aux agents analysés et aux incidents déjà chargés ; pas de mesure exhaustive de l’environnement ni de collecte en continu. |
| Scénarios | Test manuel via un point d’accès fourni, ou réponse saisie ; comparaison textuelle simple et première réponse reçue. Pas de campagne automatisée ni d’évaluation sémantique. Un test peut déclencher les actions de l’agent. |

Les corrections récentes des vues Dépendances et Rapports ont été compilées et testées localement, mais leur stabilité en interface sur les agents concernés et l’export PDF restent à confirmer.
