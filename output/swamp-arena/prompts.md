# Arena Pantanosa — fontes e prompts

Modo: imagegen integrado. Referência: `C:/Users/ygora/Downloads/Arena Pantanosa Flutuante sem Chamas.png`.

As camadas iniciais de objetos/vegetação foram refinadas em atlas independentes para evitar raízes e folhas conectadas. A plataforma e o fundo usam as primeiras extrações; os recortes finais de objetos e plantas vêm dos atlas.

## background

Transparência solicitada: false.

Input image is the edit target, a painted anime swamp floating arena. Preserve its exact camera perspective, 4:3 full-frame composition, original positions and proportions. Output a full-frame layer aligned to the original, not a rearranged asset sheet. No text, characters, flames, borders or invented decorations. Use case: precise-object-edit. Produce ONLY the distant misty swamp background. Completely remove the entire central floating island including its floor, cliffs, hanging roots, pillars, stumps and plants. Reconstruct the hidden swamp water, soft fog, trees and distant huts consistently. Preserve the original soft background and green-blue palette. Opaque full frame, no arena anywhere.

## arena

Transparência solicitada: true.

Input image is the edit target, a painted anime swamp floating arena. Preserve its exact camera perspective, 4:3 full-frame composition, original positions and proportions. Output a full-frame layer aligned to the original, not a rearranged asset sheet. No text, characters, flames, borders or invented decorations. Use case: background-extraction with precise-object-edit. Produce ONLY the floating platform on true transparent alpha. Preserve the circular muddy mossy stone floor, puddles, central cracked stone circle, rim and decorative hanging cliff/root underside. Remove the entire distant swamp background. ALSO remove ALL upright props: large rear central stump/root mound, all three bowl-topped stone pedestals, all five narrow upright wooden stumps/columns, and raised cattails/grass/flowers in front of floor. Inpaint their former footprints with continuous matching muddy floor and mossy rim, so no baked upright objects remain. Keep only low flat ground vegetation and hanging cliff vegetation below the rim. Match original island outline and position exactly; no shadow backdrop and no opaque rectangle.

## props

Transparência solicitada: true.

Input image is the edit target, a painted anime swamp floating arena. Preserve its exact camera perspective, 4:3 full-frame composition, original positions and proportions. Output a full-frame layer aligned to the original, not a rearranged asset sheet. No text, characters, flames, borders or invented decorations. Use case: background-extraction. Produce a transparent prop layer containing ONLY the nine upright solid objects, in their exact original positions and sizes: the large rear central stump with roots; three bowl-topped mossy stone pedestals at rear-left, rear-right, front-left; five narrow mossy wooden columns at left-back, right-back, right-front, lower-right and lower-left. Preserve attached roots, vines, mushrooms and tiny base plants belonging to these props. Delete ALL platform floor, island rim/cliffs, distant background and detached grass. True transparent alpha between isolated objects. Each prop must remain separate and complete, not connected by any floor pixels. Preserve original positions so layer overlays original exactly.

## foliage

Transparência solicitada: true.

Input image is the edit target, a painted anime swamp floating arena. Preserve its exact camera perspective, 4:3 full-frame composition, original positions and proportions. Output a full-frame layer aligned to the original, not a rearranged asset sheet. No text, characters, flames, borders or invented decorations. Use case: background-extraction. Produce ONLY a transparent foreground vegetation overlay: the raised grass/reeds/cattails and small white flowers along the FRONT half of the island rim, around lower-left (near 25% frame width,65% frame height), lower-center, lower-right (near 72% width,70% height) and right-front edge. Remove all floor, rocks, rim, cliffs, stumps, columns, pedestals and background; keep only these soft plant clumps at their original exact locations. True alpha, clean leaf silhouettes and transparent gaps, no opaque backing. Do not include the rear/top plants.

## Atlas final de objetos

Use case: background-extraction, game prop atlas. Using the attached swamp arena as the exact painted style and object reference, extract its NINE solid props into a precise 3x3 atlas of equal square cells with TRUE TRANSPARENT ALPHA. This is an atlas, NOT the original scene layout. Each cell contains exactly ONE complete independent object, centered with 10% clear padding on every side, no overlap, no connecting roots across cells. Row 1 left: the large rear double-trunk stump with its own roots, WITHOUT any adjacent stone pedestal or narrow side column. Row1 middle: rear-left bowl-topped stone pedestal. Row1 right: left-side narrow wooden column. Row2 left: rear-right narrow wooden column. Row2 middle: right bowl-topped stone pedestal. Row2 right: front-left bowl-topped stone pedestal. Row3 left: right-front tall wooden stump. Row3 middle: front-left narrow short stump. Row3 right: front-right short stump. Preserve each object's moss, mushrooms, leaf details and original look. No floor, no platform rim, no background, no shadows painted outside bases, no text, no letters, no gridlines, no white/black/checkerboard matte. Nine clearly separated cutouts, fully visible including all roots.

## Atlas final de vegetação

Use case: background-extraction, foreground foliage sprite atlas. From this swamp arena reference, make a 2x2 atlas with FOUR separate clumps of foreground vegetation on TRUE TRANSPARENT ALPHA. Each cell has one isolated complete clump with 12% padding, no overlap, no roots connecting other cells. Upper left: long green reeds/cattails with orange seed heads and a few tiny white flowers, matching front-left plants. Upper right: low short green grass/leaf clump with a few white flowers for center-front. Lower left: green cattails with orange seed heads and low broad leaves for front-right. Lower right: smaller leafy clump with white flowers for right edge. Preserve original painterly anime detail and green palette. These are noncollidable occluding plant sprites, no stone, no floor, no tree stumps, no pedestal, no island rim, no black/white matte, no shadow backdrop, no labels or grid lines. Four complete independent plant sprites.
