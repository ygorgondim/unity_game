# Wizard Royale — Unity vertical slice

Projeto Unity 2D/URP criado a partir do plano de migração e dos assets em `../src`.

## Mapa ativo: Arena Pantanosa

O mapa pantanoso substitui a Arena Dourada como arena ativa, preservando os arquivos e o caminho de fallback do mapa anterior. Assets em `Assets/_Game/Resources/Art/SwampArena`: `background.png` é apenas o pântano distante; `arena.png` é a plataforma limpa, com piso, borda e face vertical, sem totens/colunas/vegetação frontal embutidos; `Props/prop_0.png` a `prop_8.png` são os nove objetos sólidos; `Foliage/plant_0.png` a `plant_3.png` são os quatro conjuntos de mato decorativo.

A separação e a reconstrução das partes ocultas foram feitas com imagegen integrado. Fontes, atlas finais, extrações intermediárias, prompts completos e importador estão em `../output/swamp-arena`. O importador identifica os componentes alpha principais, organiza por linha/coluna, remove fragmentos externos e salva sprites independentes sem cortar suas silhuetas.

`layout.json` concentra o contorno caminhável, pontos de respawn, posição/tamanho/base visual de cada objeto e seus círculos de colisão. Coordenadas em pixels, origem superior esquerda, referência 1448 × 1086 e 120 pixels por unidade. Totens e colunas têm colisão somente na base; o tronco grande usa três círculos para aproximar suas raízes. Os mesmos círculos alimentam a simulação e os `CircleCollider2D` de inspeção. O mato não recebe collider. A altura visual dos objetos não aumenta sua colisão.

`ArenaDepth` ordena pela coordenada Y da base. Um personagem acima da base de um objeto passa atrás dele; abaixo da base, passa na frente. `SortingGroup` mantém corpo, contorno e flash de dano juntos. Projéteis, meteoros e coletáveis também participam dessa profundidade; nicks e barras ficam em um grupo separado para manter a leitura. A vegetação frontal usa o mesmo sistema, sem bloquear o movimento.

Todo ponto fora do polígono `floor` é zona de queda, incluindo bordas verticais, raízes decorativas e fundo. A face lateral da ilha não é chão. As poças pintadas no piso da arena são caminháveis. A recuperação existente de 3 s continua válida ao sair do piso; vida zerada continua causando expulsão letal. Iluminação e máscara atmosférica agora usam os recursos do mapa ativo, não a textura da Arena Dourada.

Recomendação de edição: ajustar sempre a base e os círculos pelo `layout.json`, mantendo colisão e aparência independentes. Após importar o mapa, conferir manualmente as margens, a passagem atrás dos nove objetos, a vegetação frontal e os quatro pontos de retorno. Não foram executados testes, Play Mode ou build durante esta implementação.

## Jogar

1. Abra o projeto com Unity 6.6.6f1.
2. Abra `Assets/Scenes/SampleScene.unity`.
3. Pressione Play.

O bootstrap é criado automaticamente em runtime. Não é necessário configurar a cena.

Controles: WASD/setas para mover, mouse para mirar, clique esquerdo ou J para atacar e Espaço/Shift para dash. Controle usa analógico esquerdo/direito, botão sul/gatilho direito e botão leste/gatilho esquerdo. Todos começam com Slash; atravesse um orbe no mapa para equipar sua magia. Pressione R no resultado para revanche.

## Escopo implementado

- quatro magos flutuantes em aquarela (azul/verde, sombrio vermelho/preto, folha verde/laranja e outonal roxo/laranja), com quatro direções, sombra no chão e barra da roupa ondulante; o mago original com caminhada permanece preservado como fallback;
- Arena Dourada em camadas independentes de ilha e oceano, com arte 2710 × 2320;
- contorno caminhável alinhado à imagem: sair do piso inicia 3 s de flutuação de recuperação antes da queda, enquanto obstáculos mantêm sua geometria real de colisão;
- câmera dinâmica com movimento e zoom suaves para enquadrar os personagens ativos;
- ambientação de fim de tarde com gradação quente, bloom leve, vinheta suave, fachos de sol e luzes flutuantes com fade, configurada em `ArenaAmbiencePresentation`;
- reflexos dourados animados na água, nuvens translúcidas mascaradas pela arte da ilha e sombras móveis de nuvens, configurados em `ArenaWaterAtmosphere`; partículas douradas, laranja e rosa com tamanhos de até 3× o original;
- indicador de mira com anel fino e sutil, mais triângulo direcional na cor da magia equipada;
- nicks discretos acima dos personagens, contorno na cor de cada jogador e marcador de seta exclusivo do jogador local; identificadores acompanham a animação e compensam o zoom;
- barra de vida compacta abaixo de cada nick; somente o jogador local vê também sua barra de cooldown do ataque, que esvazia ao atacar e preenche até ficar pronta, usando o cooldown da habilidade equipada;
- simulação fixa a 60 Hz;
- quatro participantes (um humano e três bots);
- movimento, mira, oito ataques/habilidades, dano, hit stun e curva de knockback;
- cada ataque aceito pausa o movimento voluntário por 0,15 s e vira o personagem para a direção registrada do disparo; interrompe dash em andamento e impede novo dash durante a pausa, mas não cancela repulsão recebida. O cooldown e a duração das habilidades permanecem independentes dessa pausa;
- ataque básico Slash: onda de luz de curto alcance na direção da mira, alcance de 1,5 unidade a partir do centro da hitbox, avanço em 0,5 s, 40 de dano e cooldown de 1 s; raio de contato de 0,18 unidade (espessura de 0,36), com bloqueio por obstáculos, frente luminosa branco-azulada, núcleo brilhante, rastro e faíscas. O dano acompanha a frente da onda, uma vez por alvo, e o rastro final desaparece em 0,16 s sem causar dano;
- Vento coletável: antigo projétil básico branco, preservando 40 de dano, alcance de 5,2 unidades e cooldown de 1,4 s. Inicia a 3,64 unidades/s e acelera uniformemente até 7,28 unidades/s (2×) ao atingir o alcance máximo, em aproximadamente 0,95 s; orbe branco/esverdeado com brilho próprio;
- Explosiva com pavio e dano em área, Slime com seis ricochetes, Água com poça desaceleradora e Raio com carga de 0,5 s, direção travada no disparo e bloqueio por obstáculos;
- Orbe Orbital violeta coletável: órbita permanente, expansão para 2× o raio e giro de ataque completo em 1 s, com retorno suave;
- drops de magia com surgimento após 10 s, duração de 10 s, limite de três no chão, posições livres de obstáculos e coleta por proximidade;
- seis artes individuais dos orbes coletáveis em `Resources/Art/PickupOrbs`: Vento celestial branco/azulado, Água azul, Explosiva solar laranja, Raio dourado, Slime verde e Orbital cósmico violeta. Mantêm escala, flutuação, sombra no chão, brilho colorido e desaparecimento suave;
- dash, recuperação de borda, queda, vidas, respawn e KOs;
- respawn: após concluir a morte/queda, espera de 5 s fora de jogo, seguida de materialização luminosa de 1,5 s no ponto de retorno. Durante a animação não há movimento, ataque, coleta ou colisão entre personagens. Invulnerabilidade durante toda a animação e por mais 2 s depois; dano, repulsão e lentidão são bloqueados. Anel pulsante e partículas indicam proteção. Personagens aguardando retorno não influenciam o enquadramento da câmera;
- recuperação fora da arena: movimento e alcance do dash +30%, taxa de recarga do dash +30% enquanto estiver fora; voltar ao piso cancela a queda e restaura os 3 s, mas não zera o cooldown do dash;
- vento em `RecoveryWindPresentation`: três faixas brancas translúcidas saem de trás do corpo no sentido contrário ao deslocamento, com cauda que acompanha curvas suavemente. Em movimento, a esteira se alonga; parado, encurta e forma pequenas ondulações atrás do corpo. Sem halo ou círculo sob os pés. Partículas brancas e pequenas luzes suaves ficam em coordenadas de mundo ao longo do caminho; faixas e partículas são desenhadas atrás do personagem para preservar o rosto. Ativo em recuperação fora da arena ou durante o dash, com entrada/saída suave e partículas residuais em fade; não aparece no lançamento letal. Pool limitado a 256 partículas, sem alterar a hitbox ou as regras de recuperação;
- o dash comum também usa essa mesma esteira, com entrada rápida de 0,05 s e orientação inicial na direção do dash. Ao terminar, as faixas e partículas se dissipam suavemente; durante recuperação e dash simultâneos, o efeito é compartilhado sem duplicação;
- vida zerada: lançamento letal na direção do golpe a 20 unidades/s, ignorando obstáculos e sem controle, dash ou recuperação de borda. O personagem permanece visível durante a expulsão e começa a afundar ao sair do piso; perde uma vida, atribui o KO ao atacante e renasce se ainda tiver vidas;
- contagem, relógio, desempate por vidas/HP, HUD e revanche;
- câmera ortográfica dinâmica e testes EditMode das regras centrais.

Descarte manual de magia, lobby configurável, segunda arena, áudio, configurações persistentes e multiplayer continuam como próximos marcos do plano completo.

## Hitbox padrão dos personagens

Escala atual: `GameTuning.CharacterScale = 0.70` (redução de 30%). Arte, sombra, elevação, balanço, mira e cápsula usam esse fator, sem reduzir novamente o transform pai. A cápsula atual mede 0,252 de largura × 0,336 de altura; centro a 0,266 do chão, elevação de 0,084 e raio de interação de chão de 0,154. Os valores-base descritos abaixo são anteriores a esse fator. Velocidades, dano e alcance das habilidades não mudam.

Todos os personagens recebem ataques pela mesma cápsula vertical de 0,36 unidade de largura e 0,48 de altura, alinhada ao corpo flutuante, não à sombra. A posição lógica permanece na base do chão; `CharacterHitbox.BodyElevation` adiciona 0,12 unidade de elevação compartilhada pela arte e pela cápsula. O centro fica a 0,38 unidade do chão (0,26 acima da base elevada do corpo), e o limite inferior fica a 0,14 unidade do chão. A geometria está centralizada em `CharacterHitbox` e independe do sprite, da direção e da animação. Chapéus, capas e outros acessórios não ampliam a área de acerto. Novos personagens devem alinhar o corpo a essa cápsula, sem modificar suas medidas. Somente o balanço de ±0,025 unidade permanece visual, sem oscilar a hitbox.

Projéteis, explosões, raio e colisões de movimento contra obstáculos e outros personagens usam essa mesma cápsula; a mira e a origem dos disparos acompanham seu centro. O raio usa a espessura do próprio projétil ao verificar o contato. A coleta de magias e as poças usam a base circular dos pés, com raio fixo de 0,22 unidade.

## Orbe Orbital

`WeaponType.Orbit` entra no mesmo sorteio das habilidades coletáveis e substitui a magia equipada. Três meteoros violetas usam a mesma imagem cósmica do coletável (`orbit.png`), preservando brilho, núcleo luminoso, chamas, partículas e rastros. Orbitam o centro da hitbox, espaçados em 120 graus; suas posições visuais vêm diretamente da simulação. O raio normal é 0,70 unidade e o período normal é 3 s. Ao atacar, expandem em 0,12 s até 1,40 unidade, completam exatamente uma volta em 1 s e recolhem em 0,18 s. O cooldown entre ativações é 3 s.

Balanceamento inicial: 40 de dano por orbe e repulsão-base 1,6, com as mesmas curvas de repulsão e feedback de dano das outras habilidades. Os três orbes também causam dano sem ativação. Cada orbe tem intervalo mínimo de contato passivo de 0,75 s por alvo; em cada ativação, cada orbe atinge cada adversário no máximo uma vez. O movimento é subdividido em segmentos de até 0,01 s para verificar contato com a cápsula padrão. Trocar de magia ou cair remove os orbes; o respawn volta ao Slash básico. Parâmetros em `GameTuning`, estado em `OrbitState`, regras em `MatchSimulation` e visual em `OrbitAbilityPresentation`.

## Sombra

Novo coletável `WeaponType.Shadow`, com a arte original `Resources/Art/PickupOrbs/shadow.png`. Enquanto equipada, aumenta o alcance do dash em 30% (pela velocidade, mantendo a duração), sem reduzir seu cooldown normal de 2 s. Cada dash inicia 1,2 s de invulnerabilidade, contados desde a ativação. O bônus de alcance acumula multiplicativamente com os 30% de recuperação fora da arena; a recarga acelerada de recuperação continua funcionando. Não altera o dash das outras habilidades.

Durante esses 1,2 s, `FloatingRobe` usa uma silhueta preta, preservando somente as regiões dos olhos de cada direção/personagem (sem olhos de costas). A ondulação da roupa continua. O contorno identificador fica oculto e a esteira clara é substituída por bolhas pretas em coordenadas de mundo. Nome, barras e marcador local permanecem. Morte e respawn removem a forma; proteção de spawn e invulnerabilidade de Sombra são temporizadores independentes.

Ataque inicial de Sombra: corte explosivo instantâneo de 40 de dano, cooldown de 1,4 s e repulsão-base 1,6. O cone tem alcance de 1,4 unidade, largura inicial 0,1 e largura final 0,5, com frente arqueada. A cápsula padrão é comparada ao polígono compartilhado em `ShadowSlashGeometry`, uma vez por alvo por corte, respeitando obstáculos. `ShadowSlashView` exibe preenchimento preto, borda violeta e bolhas escuras durante 0,3 s. Esses valores de dano/cooldown são o balanceamento inicial, sem alterar as demais habilidades.

As bolhas do rastro de Sombra nascem com tamanho e proporção aleatórios, mantêm opacidade constante e encolhem até desaparecer, sem fade. `ShadowBubble.shader` deforma seu contorno com ondas de fases diferentes, evitando círculos perfeitos. Algumas nascem aleatoriamente ao redor do corpo; durante o dash há também fumaça escura suave, com baixa opacidade e dissipação própria. As partículas ficam no espaço, sem acompanhar o personagem após emitidas. O efeito claro do dash comum permanece inalterado. A duração atual da forma invulnerável é 0,9 s (`ShadowDuration`).

## Artes dos coletáveis

Os seis PNGs fornecidos foram copiados sem redesenho para `Assets/_Game/Resources/Art/PickupOrbs`: `wind.png`, `water.png`, `explosive.png`, `lightning.png`, `slime.png` e `orbit.png`. O carregador centraliza a área visível e normaliza seu diâmetro, compensando as margens diferentes dos arquivos. `PickupOrb.shader` oculta apenas o fundo quase preto das artes RGB, preservando suas cores e o alpha original da Água. Cada habilidade usa material com textura vinculada explicitamente. Os meteoros equipados da Orbital compartilham a imagem e o material cósmicos do coletável, sem modificar seus materiais independentes de brilho e rastro. Os demais projéteis mantêm seus visuais anteriores. A spritesheet antiga permanece preservada, mas não é mais usada pelos coletáveis.

## Personagens e animações

`PlayerState.Character` seleciona a aparência sem alterar as regras ou a hitbox. O jogador local inicia com `CharacterType.FloatingWizard`, a personagem de aquarela azul/verde, rosto escuro, olhos luminosos e pingente de gota. O bot Freya usa `CharacterType.DarkFloatingWizard`, o mago sombrio vermelho/preto com chifres claros e olhos vermelhos. Loki usa `CharacterType.LeafFloatingWizard`, com roupa de folhas verdes, adorno laranja e pingente de coração. Odin usa `CharacterType.AutumnFloatingWizard`, com chapéu roxo, roupa de pontas alaranjadas, folhas laranja e cachecol vermelho/laranja. A aparência é preservada no respawn.

Os quatro sprites novos estão em `Assets/_Game/Resources/Art/FloatingWizard/Idle`, com uma pose própria para frente, costas, esquerda e direita. A mesma pose é usada parada e em deslocamento: essa personagem flutua em vez de caminhar. Os PNGs têm canvas transparente de 512 × 512, escala comum de 600 pixels por unidade e pivô de base `(0.5, 18/512)`.

O mago sombrio usa o mesmo carregador e configuração, com os quatro PNGs em `Assets/_Game/Resources/Art/DarkFloatingWizard/Idle`. Ele compartilha integralmente `FloatingCharacterPresentation` e os shaders: mesma elevação e sombra, ondulação 5× mais rápida durante o movimento, arrasto da barra para trás no deslocamento lateral e deformação sincronizada no contorno e no flash de dano. Fontes e prompt de extração do fundo com imagegen integrado estão em `../output/dark-floating-wizard`; o importador em `../output/floating-wizard/Import-FloatingWizard.ps1` aceita `-SourcePath` e `-OutputDirectory` para importar esse personagem sem alterar os sprites do mago azul.

O mago folha reutiliza integralmente a mesma configuração e os mesmos efeitos dos outros flutuantes. Seus quatro PNGs ficam em `Assets/_Game/Resources/Art/LeafFloatingWizard/Idle`, alinhados em canvas 512 × 512, com o mesmo pivô e escala. Fontes e prompt de extração com imagegen integrado estão em `../output/leaf-floating-wizard`, usando o mesmo importador parametrizado. Nenhuma regra de combate ou geometria de colisão depende da aparência de folhas.

O novo Odin substitui a aparência original e usa os quatro PNGs em `Assets/_Game/Resources/Art/AutumnFloatingWizard/Idle`. Reutiliza exatamente o mesmo carregador, escala, pivô, flutuação, sombra, ondulação 5× em movimento e arrasto lateral da roupa dos outros flutuantes, mantendo a hitbox compartilhada. Fontes e prompt com imagegen integrado em `../output/autumn-floating-wizard`. Os recursos antigos de `Character01` permanecem preservados para fallback, mas não são mais a aparência escolhida para Odin.

`FloatingCharacterPresentation` eleva a arte 0,12 unidade e aplica um balanço vertical de ±0,025 unidade, em um ciclo de aproximadamente 3,5 segundos. A sombra oval fica ancorada na base, varia de tamanho e intensidade com a altura e desaparece ao sair do piso ou cair. Identificadores acompanham a posição visual da personagem.

`FloatingRobe.shader` anima somente a parte inferior da saia, com duas ondas suaves. O rosto, chapéu e pingente ficam estáveis. `FloatingHem.cginc` é compartilhado pelo material da personagem, pelo contorno e pelo flash branco de dano para manter as três silhuetas alinhadas. Nos personagens que caminham, a intensidade dessa deformação permanece zero.

O fundo branco da referência foi removido com a ferramenta imagegen integrada. A spritesheet transparente, o prompt e `Import-FloatingWizard.ps1` estão em `../output/floating-wizard`. O importador recorta as quatro direções, remove fragmentos externos e registra a base e a escala sem gerar novas poses.

A personagem anterior, loira com lua no chapéu, foi retirada dos recursos ativos. Seus PNGs e metadados foram movidos para `../output/retired-characters/moon-wizard/Character02`, fora de `Assets`, e podem ser recuperados. As fontes históricas permanecem em `../output/new-wizard`; não são carregadas pelo jogo.
