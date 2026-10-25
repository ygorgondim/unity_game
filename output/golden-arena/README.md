# Arena dourada — camadas e área de queda

Arquivos PNG em 2710 × 2320, com a mesma tela e alinhamento:

- `arena.png`: ilha com transparência, incluindo as faces rochosas decorativas e espuma junto à base.
- `ocean.png`: fundo de água completo, reconstruído onde a ilha estava.
- `floor-mask.png`: verde puro = chão; preto = queda (água, rochas e exterior).
- `walkable-preview.png`: conferência visual; verde = seguro, vermelho = queda. Não usar como textura do cenário.
- `composite.png`: composição das duas camadas para conferência.
- `unity-preview.png`: captura da integração no Unity; as marcações dos obstáculos já existentes no projeto continuam visíveis.
- `layout.json`: contorno do piso, origem e escala comuns à máscara e ao jogo. Coordenadas em pixels, origem da imagem no canto superior esquerdo.

No Unity, os recursos estão em `Assets/_Game/Resources/Art/GoldenArena`. A apresentação cria objetos separados para a água e para a ilha, além de um PolygonCollider2D trigger da superfície. A simulação usa diretamente o mesmo contorno do JSON, pois os personagens se movem sem Rigidbody2D. Os pés são a referência da posição segura. Ao sair do piso, a queda começa imediatamente, o personagem passa atrás da ilha e perde uma vida ao terminar a animação; renasce enquanto houver vidas. Rochas não bloqueiam como parede: elas contam como queda. A área caminhável foi traçada com uma pequena margem para dentro da borda irregular, evitando que os pés se apoiem na face rochosa.

O fundo se estende com cópias espelhadas da água para evitar emendas visíveis nas bordas da imagem. Pontos de surgimento, itens e bots respeitam o piso. Os obstáculos circulares existentes foram preservados.

As imagens foram separadas com a ferramenta integrada imagegen (não CLI); a água oculta foi reconstruída por IA e o recorte pode apresentar pequenas diferenças na borda em relação à imagem original. As saídas foram redimensionadas para manter 2710 × 2320. A máscara foi rasterizada no Unity a partir do mesmo polígono usado pela simulação.

## Prompts usados

### Arena transparente

Use case: background-extraction. Edit target: attached golden island game arena. Extract the COMPLETE island including the golden top, green rim, ALL rocky cliff sides and the thin white shoreline foam immediately touching its base, onto genuine transparent background. Remove ALL blue ocean. Preserve EXACT original canvas aspect ratio 2710:2320, position, size, perspective, island silhouette, textures and colors. Do not recenter or zoom or crop: retain the original empty margins as transparency so layers align. No checkerboard drawn into pixels. Output a single transparent PNG, ideally 2710x2320.

### Água

Use case: precise-object-edit. Edit target attached island game scene. Produce the BACKGROUND ONLY layer: remove the complete island, earth, grass, rocks, white island shoreline and shadow, and seamlessly inpaint the entire removed area with matching vivid blue ocean and repeating cyan caustics. The whole rectangular image must be continuous water, with no land, no hole, no foam outline of an island. Keep original top-down painted game-art style, wave texture scale and blue colors. Preserve aspect ratio and canvas framing, ideally 2710x2320. No objects or text.

