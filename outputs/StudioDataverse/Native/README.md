# Moteur local

`lamazero.dll` provient de [lamazero v2.0.0](https://github.com/ayinedjimi/lamazero/releases/tag/v2.0.0), licence MIT. SHA-256 : `913bedd86a42038a7ea8705dff5e8b1f69db96836eb4f83454e4ff67d99936ee`. Les notices des dépendances sont fournies par ce projet.

Le modèle GGUF n’est pas enregistré dans Git. La distribution locale de cette application utilise le fichier `models/model.gguf`, issu du modèle [Qwen2.5-0.5B-Instruct-GGUF](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF), sous licence Apache-2.0, fichier `qwen2.5-0.5b-instruct-q4_k_m.gguf`. SHA-256 : `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db`.

Le moteur nécessite Windows x64, un processeur AVX2 et le runtime Visual C++ 2015–2022. La génération est exécutée à la demande sur le poste. La sortie doit être relue : elle n’atteste pas que les questions ou leurs réponses sont correctes.
