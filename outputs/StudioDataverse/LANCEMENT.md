# Lancement et fenêtre visible

Ouvrez le raccourci **Lancer Studio Dataverse.lnk** fourni dans le dossier outputs. Il démarre directement l’application sur votre bureau Windows, avec sa propre icône.

Lorsqu’un agent Codex lance l’application, il doit utiliser un lancement hors du contexte isolé du terminal. Dans ce contexte isolé, un processus peut être actif et déclarer sa fenêtre visible sans être accessible depuis votre bureau. Modifier l’icône ou maximiser la fenêtre ne corrige pas cette séparation des bureaux.

Les consignes permanentes de ce dossier sont dans AGENTS.md, à la racine de l’espace de travail. Après chaque lancement automatique, la présence de la fenêtre sur le bureau doit être vérifiée avant d’affirmer qu’elle est visible.
