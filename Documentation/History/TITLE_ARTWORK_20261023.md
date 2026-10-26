# 沿用原《街垒》字标改字

用户要求直接沿用原“街垒”的字形。原资源是生成的透明字标，不是完整字体；参考 Assets/Art/UI/Paris1832/Masters/TitleEpic-v4.png，通过内置 image_gen 编辑成一行《第1832次心跳》，保持浅石色、楔形宋体笔画和像素轮廓。补足两端留白后选择最终版本，PNG 原样复制，没有脚本修图。

运行资源：Assets/Resources/UI/Paris1832/TitleHeartbeat-OriginalStyle.png。ParisUIStyle.titleArtwork 原生保存为该 Sprite；原 titleMark 和 titleFont 保留。ParisUITheme 用现有 GameTitleMark 节点显示新字标，隐藏 GameTitle 文本；新字标为空时恢复字体标题。StartScreenView 随 A/C 左、B 右的封面布局放置字标，保持等比，不遮挡按钮或拦截点击。没有更改随机封面逻辑。

Unity Editor 原生保存 Style.asset、StartScreen.prefab 和 Title.unity。单图 Sprite、Point、无 MipMap、Uncompressed、Clamp、保留透明度，最大纹理尺寸 4096。没有更改 PNG 像素或 Sprite 切片。

106 运行时与 31 编辑器源码编译零错误零警告；发布条件编译通过。原生 Play 57 项通过，帧 1→49；三版实机画面已查看，检查了字标引用、位置、无重复标题、按钮、24 次不连续重复抽图及三次返回标题。

输出、截图和完整两轮提示词：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/原版街垒字标改字/。修改前备份：同工作区 work/title-original/backup/。
