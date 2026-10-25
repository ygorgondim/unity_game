# Mago folha flutuante

Modo: imagegen integrado, edição com fundo transparente.

Referência: `C:/Users/ygora/Downloads/Folheto Quatro Ângulos do Mago Folha.png`.

## Prompt utilizado

undefined

## Integração

Aplicado ao bot Loki. Quatro poses em `UnityProject/Assets/_Game/Resources/Art/LeafFloatingWizard/Idle`, com canvas 512 × 512, escala de 600 pixels por unidade e pivô `(0.5, 18/512)`, iguais aos outros magos flutuantes. Compartilha flutuação, sombra, ondulação 5× em movimento, arrasto lateral, contorno e flash de dano. Hitbox padrão inalterada.

Importador reutilizado: `output/floating-wizard/Import-FloatingWizard.ps1`, com `-SourcePath` apontando para `directions-transparent.png` desta pasta e `-OutputDirectory` para a pasta de sprites do mago folha.
