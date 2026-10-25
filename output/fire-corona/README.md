# Fogo sobre a imagem da habilidade

Atualização: a animação foi retirada do item coletável por solicitação do usuário. Ela permanece apenas no projétil da habilidade ativa. O coletável voltou a usar a imagem e o brilho suave padrão. Sem testes ou build nesta alteração.

O GIF fornecido (500 × 500, 16 quadros de 80 ms) foi usado como referência visual de faixas amarelas com bordas vermelhas circulando e deformando sobre uma esfera.

A recriação é procedural, em `Assets/_Game/Resources/Shaders/FireCorona.shader`. Um padrão de faixas é projetado sobre uma esfera e animado em ciclo de 1,28 segundo, com recortes transparentes, silhueta irregular e brilho vermelho suave. É uma interpretação animada do estilo do GIF, não uma reprodução pixel a pixel nem uma importação do fundo preto/roxo da referência.

O efeito foi colocado sobre a imagem da habilidade de fogo tanto no coletável quanto no projétil. O projétil passa a usar a mesma arte de fogo do coletável. Os flares de estrela, os cartões de labaredas presos ao projétil e o antigo halo do coletável foram substituídos pela camada animada. O brilho aumenta discretamente durante o preparo da explosão.

O rastro de movimento, as brasas e a demarcação sutil da área foram preservados. A estrela pontuda de impacto foi removida da explosiva. Dano, alcance e tempo de detonação não foram alterados.

Não foram executados testes, Play Mode ou build. Os PNGs `reference-*.png` são quadros extraídos do GIF para inspeção da referência; não são capturas da implementação.
