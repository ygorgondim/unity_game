# Implementação dos VFX

## Revisão dos rastros e da demarcação de área

Os rastros de fogo passaram a combinar emissão por distância e por tempo, com direção baseada no movimento real. O orbital guarda até 32 posições recentes (aproximadamente 0,2 segundo) em vez de reconstruir um arco ideal. As chamas recebem deformação animada, turbulência, menor rotação aleatória e perda gradual de cor e brilho. A explosiva deixou de emitir o segundo rastro procedural e as rajadas de círculos duplicadas na criação das áreas foram removidas.

Os contornos grossos das áreas e dos projéteis de fogo e água foram substituídos por luz difusa. `SpellAreaLight.shader` produz preenchimento suave e uma cintilação fina dentro do raio real, sem engrossar ou pulsar o limite da área. A preparação da explosiva usa o mesmo tratamento e as ondulações da água ficaram mais discretas.

A capacidade dos buffers nativos agora é de 3.200 partículas. Esta revisão foi feita sem executar testes, entrar em Play Mode ou gerar build, conforme solicitado. As capturas e medições das seções seguintes são anteriores a esta revisão.

## Resultado

As sete habilidades usam partículas específicas por elemento, com impactos ligados aos acertos da simulação. O orbital combina corpo negro-arroxeado, fissuras, centro branco-lilás, sete línguas de fogo por orbe, brasas e rastros que se intensificam durante a expansão.

- Slash: crescente curvo, núcleo luminoso e faíscas direcionais.
- Explosiva: chamas animadas, brasas, clarão, onda de expansão e fumaça breve.
- Slime: corpo com reflexão, bolhas, gotas e rajadas nos ricochetes.
- Água: gotas, respingos, reflexos e ondulações na poça.
- Raio: concentração de partículas, descarga irregular e quatro ramificações.
- Orbital: três estrelas escuras com fogo violeta, núcleo e rastros de brasas.
- Vento: arcos perolados, curvas de ar, poeira e detritos.

## Ajustes pelo Inspector

O asset `UnityProject/Assets/_Game/Resources/Vfx/SpellVfxSettings.asset` contém os sete perfis e a densidade global. Cada perfil permite ajustar cor principal, cor secundária, taxa de emissão, tamanho e duração das partículas. A alteração persiste para a próxima entrada em Play Mode.

Em Play Mode, o componente `Runtime Presentation/Spell Particle Library` permite experimentar os parâmetros da instância atual. Os perfis são clonados ao carregar, evitando gravar ajustes temporários no asset.

## Estrutura

- `SpellParticleLibrary.cs`: 16 buffers nativos de ParticleSystem, oito formatos de textura, materiais, emissão e luz composta.
- `SpellVfxSettings.cs`: configuração persistente da direção de arte.
- `SpellParticle.shader`: materiais HDR aditivos e transparência para gotas e fumaça.
- `OrbitAbilityPresentation.cs`: corpo, núcleo, coroa de fogo, luz no chão e rastros dos orbes.
- `SpellVfxPresentation.cs`: integração dos projéteis, ricochetes, carregamento, descargas e áreas.
- `MatchPresentation.cs`: sincronização de impactos e reflexos de luz nos personagens.
- `GameModel.cs` e `MatchSimulation.cs`: elemento original do disparo e notificações de acerto, com sequência e histórico limitado a 128 registros. Dano, velocidade, alcance e recargas foram preservados.

A arena e os personagens usam arte sem iluminação física. A integração luminosa usa halos aditivos no chão e uma camada colorida sobre os pixels opacos dos personagens. Ela acompanha a distância e a duração da descarga; não é uma migração para materiais iluminados ou luzes com sombras.

Não foram adicionados pacotes, imagens externas, áudio, pausas de impacto ou tremor de câmera.

## Validação

- Compilação no Unity 6000.6.6f1: zero erros e zero avisos.
- Shader suportado e capturas das sete habilidades inspecionadas.
- Três testes novos passaram: preservação do elemento após troca de habilidade, ausência de acerto falso na expiração e contato orbital sem projétil.
- Suíte completa: 24 testes, 19 passaram e cinco falharam. Os testes antigos esperam valores diferentes dos já existentes no catálogo e em GameTuning, que não foram alterados por este trabalho:
  - Dois testes do raio esperam 30 de dano; o catálogo já usa 60.
  - Um teste da arma inicial espera velocidade 3,64; o Slash já usa 3.
  - Um teste de movimento espera velocidade 1; GameTuning já usa 1,2.
  - Um teste do catálogo espera 28 de dano para a explosiva; o catálogo já usa 64.
- Teste de carga: 21 segundos simulados, quatro personagens, sete habilidades, pico de 675 partículas. A biblioteca manteve os mesmos 16 emissores e limpou todas as partículas na reconstrução.
- Amostra em tempo real: 180 frames após aquecimento, média de 8,34 ms (120 FPS), percentil 95 de 11,69 ms e pico de 225 partículas. Medição local no Editor, sem comparação de desempenho com a versão anterior e sem garantia para outros dispositivos.
- Console sem erros ou avisos de execução durante a amostra em tempo real.

Os buffers nativos comportam até 2.880 partículas adicionais; o sistema procedural anterior continua com seu limite de 256. Há limites de emissão por frame e reserva de capacidade para impactos.

## Evidências e reprodução

As imagens `orbital.png`, `explosive.png`, `lightning.png`, `slash.png`, `slime.png`, `water.png` e `wind.png` são capturas do Unity. Nas capturas isoladas, os marcadores de colisão e os outros personagens foram ocultados temporariamente, e a câmera foi aproximada. Esses ajustes não foram salvos na cena.

`tests.json`, `stress.json` e `live-performance.json` registram os resultados. Os scripts C# nesta pasta são usados por `unity command eval_file` para reproduzir a inspeção; ficam fora de Assets e não entram no jogo. Os arquivos `preview-1.cs` a `preview-7.cs` selecionam cada habilidade.

O Editor foi devolvido ao modo de edição ao terminar, e a opção de execução em segundo plano foi restaurada.
