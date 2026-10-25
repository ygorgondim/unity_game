# Novo personagem do Odin

Modo: imagegen integrado, edição com fundo transparente.

Referência: `C:/Users/ygora/Downloads/Folha de Personagem do Mago Sombrio.png`.

## Prompt utilizado

undefined

## Integração

`CharacterType.AutumnFloatingWizard`, aplicado ao bot Odin em substituição à aparência original. Os demais personagens não mudam. Quatro sprites em `UnityProject/Assets/_Game/Resources/Art/AutumnFloatingWizard/Idle`, com canvas 512 × 512, escala 600 pixels/unidade e pivô `(0.5, 18/512)`.

Reutiliza o carregador e os efeitos dos outros flutuantes: flutuação, sombra no chão, ondulação 5× durante o movimento, arrasto lateral da barra e deformação sincronizada no contorno e no flash de dano. Hitbox padrão preservada.

Importador: `output/floating-wizard/Import-FloatingWizard.ps1`, com `-SourcePath` apontando para `directions-transparent.png` desta pasta e `-OutputDirectory` para a pasta de sprites acima. Os recursos do personagem antigo permanecem preservados como fallback, sem uso pelo Odin.
