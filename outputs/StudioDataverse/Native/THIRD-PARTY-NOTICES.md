# Composants tiers

lamazero.dll embarque, en statique, les composants suivants. Ils ne sont pas modifiés : ils sont téléchargés au tag indiqué dans `CMakeLists.txt` (`LAMAZERO_LLAMA_TAG`).

| Composant | Licence | Source |
|---|---|---|
| [llama.cpp](https://github.com/ggml-org/llama.cpp) | MIT | Copyright (c) 2023-2026 The ggml authors |
| [ggml](https://github.com/ggml-org/ggml) (inclus dans llama.cpp) | MIT | Copyright (c) 2023-2026 The ggml authors |

Le texte de la licence MIT de ces projets est reproduit dans leur dépôt (fichier `LICENSE`) et doit accompagner toute redistribution de la DLL compilée.

## Modèles

lamazero ne contient **aucun modèle**. Chaque modèle (Gemma, Qwen, MiMo, etc.) est soumis à sa propre licence : vérifiez-la avant de le distribuer avec votre application.

## Runtime Microsoft

La DLL dépend du [Visual C++ Redistributable 2015-2022](https://aka.ms/vs/17/release/vc_redist.x64.exe) (`vcruntime140`, `msvcp140`, `vcomp140`).
