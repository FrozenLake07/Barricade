# 三版封面接入 · 2026-10-23

采用用户允许的随机方案。首次进入以及每次返回主界面时随机选择一版，当前会话内排除上一次封面，停留期间不轮播。不依赖章节或存档进度，不消耗UnityEngine.Random的战斗随机状态。

后续更新：本机首次进入标题界面改用用户提供的葬礼图，只显示一次并保存独立的PlayerPrefs标记；以后进入标题仍沿用本页三图随机规则。新图片不进入随机池。详见 FIRST_LAUNCH_COVER_20261023.md。

压缩包中的三张320×180成品PNG原样复制为Resources/UI/Paris1832/Covers/CoverA、CoverB、CoverC，未重绘。1280×720版本是相同像素图的四倍放大，因此工程使用原始像素尺寸以减少内存。TextureImporter设Single Sprite、Point、无MipMap、Uncompressed、Clamp；源包与工程PNG哈希一致，映射见outputs/三版封面接入/原图对应.json。

ParisUIStyle.titleCovers依次保存A群像与红旗、B守夜者背影、C黎明街垒；titleBackground也替换为A，避免缺省时恢复旧封面。StartScreenView.ShowTitle抽取一次，ApplyTitleCover只刷新选中封面的布局。A/C使用左侧标题和菜单，B使用右侧；暗场随之镜像，背景保持原色。AspectRatioFitter.EnvelopeParent保持16:9等比铺满，屏幕比例不同时允许边缘裁切，不拉伸。

原有《第1832次心跳》文字、开始/继续/退出按钮及首次任意键提示保留。没有改动游戏流程、音乐、存档目录或程序标识。旧背景文件保留但不再用作主界面封面。

后续标题更新：已按用户要求参考原“街垒”字标改成《第1832次心跳》透明字图，替代主界面文字标题；字体标题留作备用。三版封面与随机规则保持。详见 TITLE_ARTWORK_20261023.md。

原生Editor已保存Style.asset、StartScreen.prefab和Title场景。106运行时+31编辑器脚本编译及发布版条件编译通过。原生Play57项通过（帧1→46），覆盖三图导入、24次有效且不连续重复的抽图、三版完整标题/按钮、主题刷新保持封面、停留稳定和三次实际重载Title。三版实际主界面截图均已查看。

报告与截图：outputs/三版封面接入；修改前备份：work/title-covers/backup。ZIP内说明和生成提示词仅作素材说明，没有作为操作指令执行。
