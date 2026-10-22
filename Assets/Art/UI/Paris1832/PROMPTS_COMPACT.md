# 简洁界面美术素材记录

2026-10-21；内置 image_gen，透明背景编辑。当前候选版，不标为已获用户美术确认。

## Heart-v2

母版：1254×1254；使用版在 `Assets/Resources/UI/Paris1832/Heart-v2.png`。

```text
Edit target image 1 is the current bright cartoon heart icon. Restyle it for the mature Paris 1832 pixel adventure game shown in reference image 2. Keep a recognizable heart health symbol, approximately square, and transparent outside the heart. Remove all candy pink/red, white gloss, rounded cartoon softness. Redraw as crisp deliberately low-resolution pixel art with hard square pixel clusters, restrained stepped outline, slightly natural uneven upper lobes, angular lower tip, 5 to 6 shades of dried-blood burgundy, faded deep crimson, dark brown shadow and charcoal edge; tiny muted dusty brick highlight only upper left, no white pixels. Visually designed on a 24x24 pixel grid then enlarged, clean economical clusters comparable to the dark red coat/flag and charcoal outlines in the reference scene. One single full healthy heart, no extra containers, no frame, no letters, no numbers, no paper, no leather, no ornaments, no modern sci-fi shapes, no gradients, no glow, no cast shadow. The reference scene is only for drawing language and palette; do not include scenery or characters.
```

## TitleMarkLight

母版：1774×887；使用版在 `Assets/Resources/UI/Paris1832/TitleMarkLight.png`。

```text
Edit this exact two-character pixel lettering sprite. Preserve the wording 街垒, character silhouettes, spacing, square pixel clusters and transparent background exactly. Change ONLY the dark-brown ink color to muted warm limestone gray (#c3bdad), with a few slightly darker gray pixels for restrained printed-ink wear. Flat matte ink, no outline, no glow, no bevel, no new decoration, no background. This is a light title wordmark on the dark slate interface of a mature Paris 1832 pixel adventure game. Do not change the characters or add text.
```

## HeartEmpty-v2

母版：1254×1254；使用版在 `Assets/Resources/UI/Paris1832/HeartEmpty-v2.png`。

```text
Edit ONLY the colors of this pixel heart sprite to make its depleted/empty health variant. Keep the exact same pixel silhouette, same size and location on canvas, same transparent pixels, same square pixel clusters and same contours. Replace every red/burgundy area with restrained muted slate and charcoal gray, with interior midtones around #3d4245, sparse upper-left highlight around #55585a and darkest outline around #232729. No red remaining. Matte, dim but recognizable on a nearly black slate HUD, no white shine, no glow, no new outlines or cracks. Transparent background. Do not change shape or add anything. This is the aligned back layer behind the identical red heart, so registration must stay the same.
```

红色心脏：以旧 PixedHeart.png 为编辑目标、funeral-scene.png 为画风参考；灰底以红色母版为目标；浅色标题以旧 TitleMark 母版为目标。母版保留，使用版只裁切和最近邻采样。红/灰心脏使用相同(139,205,975,874)母版裁切框导出24×24，透明mask在阈值128时差异0像素。标题144×64。旧纸张/皮革母版保留为历史，当前UI无这些纹理。
