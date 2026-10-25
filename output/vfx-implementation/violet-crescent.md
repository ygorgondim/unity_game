# Ataque básico em crescente violeta

Substituído por solicitação do usuário: o ataque básico agora usa `WhiteLightWave.shader`, com frente branca ondulante, camadas de luz que fluem para trás e emissão dedicada de aproximadamente 300 partículas brancas por segundo por ataque na densidade padrão, sujeita aos limites globais de partículas. Lançamento e impacto também usam branco. A rotação de corte e o crescente violeta não são mais usados. A simulação permanece igual. Não foram executados testes ou build nesta alteração. A descrição abaixo registra a versão anterior.

O Slash inicial dos personagens passa a usar uma interpretação procedural da referência: crescente violeta com núcleo branco, gradação magenta, filamentos externos e pontas irregulares. Não inclui o fundo nem a marca da imagem.

`VioletCrescent.shader` desenha a faixa e revela o arco ao longo dos primeiros 85 ms. O efeito acompanha a trajetória já simulada, gira na direção da mira e desaparece no tempo de fade existente. As partículas de lançamento, trajetória e impacto foram alinhadas à paleta violeta. A paleta é aplicada à cópia de configuração em execução, sem modificar o asset salvo.

Os materiais são criados no método Initialize da biblioteca. O bloco de propriedades do Slash é criado no construtor da classe de apresentação (não é um MonoBehaviour), chamado pelo Awake do jogo.

Dano, alcance, recarga e duração da simulação foram preservados. Não foram executados testes, Play Mode ou build; a aparência precisa ser conferida manualmente no jogo.
