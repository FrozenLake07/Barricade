# 巴黎1832 UI 美术素材记录

2026-10-21；使用内置 `image_gen.imagegen`，四次独立生成，均设置 `transparent_background=true`。没有使用外部 CLI/API。参照新版巴黎像素技能与已采用第一章素材；以下是实际提交的英文提示词。母版为生成工具输出，不标为已获用户美术验收。

## Paper

母版 `Paper.png`：1536×1024；使用版在 `Assets/Resources/UI/Paris1832/Paper.png`。

```text
Asset type: single blank reusable game UI background sprite. Create a flat straight-on rectangular sheet of worn gray-beige rag paper, suitable for a Paris 1832 barricade game. Mature early adventure-game pixel art, distinct square pixel clusters, restrained muted limestone gray beige, soot brown edges and charcoal flecks sparsely. Blank spacious center with very low texture for readable dark text. Slight irregular stair-stepped torn corners, thin uneven dark paper edge, NO ornament, NO scroll rolls, NO metal, NO gold, NO illustrations, NO text. Wide landscape 3:2 panel filling 96% of canvas, transparent outside its silhouette. Crisp pixel edges, no blur or soft gradients. Matches navy slate rooftops, gray limestone, old brown timber and dark red flags of the project's supplied Paris pixel art reference aesthetic. Realistic subdued proportions, not cartoon parchment, not fantasy medieval UI.
```

## Leather

母版 `Leather.png`：2172×724；使用版在 `Assets/Resources/UI/Paris1832/Leather.png`。

```text
One single historical game HUD background material sprite, transparent outside silhouette. Single flat front-facing horizontally rectangular cut piece of dark worn brown-black leather, very wide 3:1 aspect ratio, center occupies 90 percent canvas width. Small irregular stepped corners, subtle broken brown thread seam just inside edge. Blank dark center for cream text layered later. Paris 1832 musket ammunition pouch material, plain practical humble used leather. Mature early adventure game pixel art: visibly coarse hard square pixel clusters, 5 subdued brown charcoal shades, sparse muted taupe edge highlights; visually drawn at 240 by 80 logical pixels then integer nearest-neighbor enlarged. NO text NO icons NO metal NO rivets NO ornamental border NO fantasy NO gold NO plastic NO gradients NO modern technology NO glow. Upper-left low light, restrained contrasts, consistent with muted brown timber, slate navy clothes and limestone in the Paris 1832 pixel adventure game.
```

## LeadBall

母版 `LeadBall.png`：1254×1254；使用版在 `Assets/Resources/UI/Paris1832/LeadBall.png`。

```text
Create exactly ONE isolated spherical lead musket ball game inventory icon on truly transparent background. Paris 1832 ammunition, simple round charcoal slate-gray lead sphere with one small matte muted light gray highlight top-left and a few coarse stepped shadow clusters bottom-right, no reflection gloss. Mature retro pixel adventure game, designed as a 20x20 logical pixel sprite enlarged with nearest-neighbor hard square edges, economical 5 gray colors, square pixel clusters visible, transparent margins. Sphere fills 75 percent of square canvas. NO cartridge case, no brass, no modern pointed bullet, no red or gold outline, no symbols, no labels, no UI frame, no ground shadow.
```

## TitleMark

母版 `TitleMark.png`：1774×887；使用版在 `Assets/Resources/UI/Paris1832/TitleMark.png`。

```text
Game title lettering sprite on genuine transparent background. Exact text in simplified Chinese: 街垒 . Two characters only, side by side, fully legible and correctly drawn, no other words. Both characters drawn in the SAME hard-edged square-pixel Chinese bitmap lettering, similar to a mature early PC adventure game pixel font. Very dark soot brown ink (#302923) with tiny uneven pixels as if printed on a Paris 1832 street pamphlet, restrained and dignified. Medium stroke weight, squared pixel corners, NO serif calligraphy, NO brush-script curves, NO bevel, NO gold, NO outline, NO shadow, NO parchment or plate, no other objects. Horizontal wordmark filling 85 percent of canvas, transparent margin. Crisp deliberately low-resolution pixel clusters with visible stepped contours, no antialiasing or blur. The word must read exactly 街垒.
```

导出只做透明范围裁切、最近邻尺寸采样，未用脚本改画内容。实际使用版尺寸：Paper 384×256、Leather 384×112、LeadBall 24×24、TitleMark 144×64。母版不放在 Resources 中。
