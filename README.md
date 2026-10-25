# Wizard Royale

Jogo de arena 2D em Unity: um jogador local e três bots, magos flutuantes, habilidades coletáveis, dash, repulsão, recuperação de borda e respawn.

## Abrir o projeto

Requisitos: Git LFS e Unity **6000.6.6f1**.

```sh
git lfs install
git clone https://github.com/ygorgondim/unity_game.git
cd unity_game
git lfs pull
```

No Unity Hub, adicione a pasta **UnityProject**. Abra `Assets/Scenes/SampleScene.unity`. A cena usa um bootstrap que cria o jogo em runtime.

Controles: WASD/setas para mover, mouse para mirar, clique esquerdo/J para atacar e Espaço/Shift para dash. Há suporte a controle. Passe pelos orbes para trocar de habilidade.

## Estrutura

- `UnityProject/Assets`, `Packages` e `ProjectSettings`: projeto Unity e configurações compartilhadas.
- `UnityProject/README.md`: mecânicas, assets e detalhes de implementação.
- `src`: fontes do protótipo anterior.
- `output`: fontes de arte, prompts e scripts de importação, incluindo versões históricas preservadas.

A arena ativa é a Arena Pantanosa, com fundo, plataforma, objetos sólidos e vegetação separados. Imagens usam Git LFS. Caches, builds, logs e preferências locais não são versionados. Arquivos `.meta` devem acompanhar os assets.

## Web e verificações

Este repositório contém o projeto-fonte, não uma versão publicada para navegador. Uma versão Unity Web/WebGL ainda precisa ser configurada, gerada e validada separadamente. Os controles atuais priorizam teclado, mouse e controle; não há interface de toque dedicada.

Testes e builds somente quando explicitamente solicitados. O envio inicial ao GitHub não executa testes, Play Mode ou builds.
