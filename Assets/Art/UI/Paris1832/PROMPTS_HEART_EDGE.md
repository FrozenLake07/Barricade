# 心形边缘配色（2026-10-22）

用户最终方向：参照最早Assets/UI/PixedHeart.png的粗像素心形，避免仿真器官，边缘黑块不要突兀。此前解剖心脏、修长印记及变成双底脚的尝试均未接入当前工程。当前为候选版，不标为已获用户终稿确认。

## 采用资源

- 红色：Masters/Heart-Classic-v4.png，对第一张原版形状配色母版仅修改近黑边缘为暗红，保留宽厚块面和小亮块。
- 灰色：Masters/HeartEmpty-Classic-v4.png，以上述红色母版为编辑目标，改为靠近主体的灰边，不用近黑描边。
- 两张母版均1254×1254，以完整正方形画布、同一归一化坐标最近邻采样成16×16，资源在Assets/Resources/UI/Paris1832。没有分别紧裁切，也没有脚本改色或修改原始PixedHeart/GreyPixedHeart。生成母版不声称原生16像素绘制。
- 活跃版本是上述v4。Heart-Classic-v3资源为中断前尝试的原图副本，保留供回退；原版PixedHeart与GreyPixedHeart不覆盖。
- 两层在16×16、alpha阈值128的可见轮廓差异为0；alpha本身并非完全相同，99个像素有差异、最大11/255。与最早原图轮廓的同尺度差异为26个像素，因此是原版风格延续，不是逐像素复刻。可见像素中无最大RGB小于65的近黑色。

## 接入与验证

Style.asset和AmmoHUD.prefab改用v4，heartTint/heartEmptyTint为白色以免再次压暗。此前中断回合已新增两个着色入口，AmmoHudView与ParisSceneUI均读取它们，战役与独立场景一致。心跳计时、填充方向、HP计算、心脏/弹药位置不变。

87运行时/20编辑器源文件使用实际Unity程序集独立编译零错误/零警告；资源引用、导入、布局不变、垂直填充、原图未覆盖及红灰轮廓检查通过。工作区outputs/心形边缘柔化的PNG为离线排版/局部放大预览，不是Unity运行截图。此前Editor启动退出140063且无日志的问题仍未解决，尚未完成原生字体/Canvas、动态填充/搏动及Play验收。

## 实际内置image_gen指令

### 1

```text
EDIT the supplied original game heart icon, not a new shape. Reference/edit target is PixedHeart.png. Preserve its ORIGINAL compact squat symmetric pixel HEART silhouette, same broad square upper lobes, center notch, stepped sides and lower pointed base, original proportions and very large deliberate square pixels. This original simple blocky heart is specifically what the user wants to return to. Do NOT use an anatomical heart and do NOT make a tall narrow emblem. Only refine palette and material: use 4 to 5 muted red shades, brick crimson front plane (#aa3435 approximately), wine-red lower/right shade (#76232b), dark warm charcoal narrow edge (#292425), and one tiny muted dusty coral upper-left highlight (#bd6260), much dimmer than the original pure white shine. Preserve the recognizable economical flat color-block layout: a clear small upper-left light block, broad red face, angular lower-right red shadow. Crisp hard square pixel clusters, no soft painterly surface, no gradients, no blur, no gloss, no rounded puffy volume, no ornate bevel. DO NOT add hatching, texture noise, cracks, organs, vessels, engraving lines, metallic badges, frames, wreaths or decorative objects. This is a modest refinement of the exact original classic simple pixel heart, more sober and compatible with a mature 1832 Paris retro pixel-art game. All background and negative space genuinely transparent. Keep same square canvas, alignment and margin proportions as the original, one full intact healthy heart and no other objects.
```

### 2

```text
Edit this exact large pixel heart image. Strictly preserve ALL shapes and positions, especially the ONE SINGLE centered bottom tip pixel block (never split it into two feet), the original symmetrical silhouette, square canvas, transparency and internal red face/shadow/highlight blocks. Change ONLY the near-black/charcoal square blocks around the heart's outer contour to a burgundy color close to its existing lower-right shadow, about #782c37. Existing red areas, existing muted coral highlight, geometry and alpha remain unchanged. Do not move any pixel blocks. No outlining, no extra pixels, no reinterpreted shape. The result should be the identical image except the formerly black blocks are now dark red and blend into the heart. Keep original canvas size and original margins. True transparency outside.
```

### 3

```text
Recolor this exact simple blocky heart to its dim slate-gray depleted-health back layer. Strictly preserve the identical square canvas, margins, every coarse block coordinate and silhouette, the SINGLE CENTERED bottom tip (not two feet), all transparency/alpha, notch and internal face/shadow/highlight boundaries. Only replace colors: main red face becomes #55595c, lower-right darker red region becomes #44484b, all formerly dark-red EDGE blocks become #4b4e52 (not black, only a little darker than the gray face), and upper-left small coral highlight becomes #727676. No near-black outline, no separated black edge squares, no new detail or shape, no cropping or shift, no gloss or gradients. One unchanged original compact classic pixel-heart shape in coherent gray tones on true transparency, registered exactly behind the red source.
```

