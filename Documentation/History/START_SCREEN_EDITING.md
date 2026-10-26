# Silver 字体与开始页编辑

当前游戏文字统一使用 `Assets/TTF/Silver.ttf`（实际文件名为 Silver）。包括准备关卡的文字、章节界面、开始提示、波次调试面板和心跳计时调试文字。

## 编辑开始页

1. 打开 `Assets/Scenes/Title.unity`，或菜单“街垒 / 战役 / 打开黑屏开始场景”。不必进入 Play 模式。
2. Hierarchy 展开 `StartScreen / TitlePage`。
3. 选 `Prompt`：在 Text 中编辑开始文案、字号、颜色、对齐；在 RectTransform 中编辑位置和尺寸。
4. 选 `Subtitle`：填写副标题或操作提示，默认空白；同样可编辑文字和布局。
5. 选 `Background`：在 Image 中编辑背景色或指定背景图片。
6. 选 `StartScreen`：在 StartScreenView 组件调整 Pulse Prompt（呼吸开关）、Pulse Cycles Per Second（每秒变化次数）、Minimum Opacity（最低透明度比例）。Canvas Scaler 可调参考分辨率和屏幕适配。
7. 保存 Title 场景。若希望所有实例和兜底界面都共享修改，可对 StartScreen 执行 Overrides / Apply All，或直接编辑 `Assets/UI/Campaign/StartScreen.prefab`。

`TitlePage` 独立于章节的 `MainText / Detail / ChapterHUD / NarrativeCaption`，开始页布局修改不会改变关卡提示布局。新增开始页装饰也应放在 TitlePage 下，进入游戏会一起隐藏。

运行时采用 Title 场景中编辑好的实例；校准界面返回开始页会显示原文案和样式，通关返回时接管新加载的 Title 实例并清理旧画布。开始提示呼吸只改变自身透明度，保留编辑的 RGB 颜色及基础透明度。任意键开始和 F2 声音校准继续由 StoryDirector 处理。

## 字体配置与核验

`Assets/Resources/Campaign/GameFont.asset` 引用原始 Silver.ttf；未复制或改名原字体。场景中的全部 8 个序列化 Text 字体引用均为 Silver，运行时生成的界面也使用该引用。

65 个运行时脚本、14 个编辑器脚本已使用 Unity 2022.3 对应引用离线编译通过；字体引用、预制体内部引用和 Title 场景预制体引用静态核验通过。当前环境 Unity 原生进程仍无法正常启动，本次没有完成 Editor 导入、Play 模式或实际屏幕显示核验。项目菜单“街垒 / 战役 / 检查十章配置与开始界面”保留原生检查入口，并允许自定义开始文案和背景。

本次修改前备份与差异位于工作区 `outputs/字体与开始页/`，不影响此前街垒生命阶段与战役配置。

## 2026-10-21 巴黎像素样式

开始页已采用第一章场景背景、旧纸菜单和像素“街垒”字图。全局材质及配色在 `Assets/Resources/UI/Paris1832/Style.asset`，根节点 `ParisUITheme.useAuthoredLayout` 默认开启，保留预制体内位置和字号；菜单绑定与首次按任意键/后续菜单逻辑沿用。标题字图解决Silver缺少“垒”字导致的宋体回退。暂停、倒数、卡片与弹药编辑入口见根目录 `UI_REVISION_AND_REWARDS.md`。上文历史黑屏方案的背景描述以此版为准。


## 2026-10-21 简洁像素与心脏/弹药同组

当前去除纸张/皮革纹理，使用炭黑与板岩色底、浅色像素字图。编辑说明以UI_HEART_AND_AMMO.md为准。开始页、暂停与卡片仍直接编辑StartScreen.prefab；心脏/弹药一起编辑Resources/UI/AmmoHUD.prefab根节点。


## 2026-10-21 精修像素边框

当前候选版已用Frame-v3细旧木/暗铁框替换上版纯色块。直接编辑原StartScreen.prefab和AmmoHUD.prefab；当前说明以UI_HEART_AND_AMMO.md为准，素材提示词见PROMPTS_REFINED.md。


## 2026-10-21 庄重宽幅候选版

当前不再使用木/铁外框，采用TitleEpic-v4字标、无外框文字菜单与暗场构图。编辑入口仍为StartScreen.prefab、AmmoHUD.prefab及Style.asset；ParisMenuAccent负责焦点，ParisBackdropGraphic负责暗场。以UI_HEART_AND_AMMO.md和PROMPTS_EPIC.md为准。


## 2026-10-21 局内HUD分层

菜单沿用宽幅版；局内最新入口为GAMEPLAY_UI_EDITING.md。ChapterHUD保留作隐藏数据源，直接编辑GameplayHeading/Controls/Objective；心脏弹药改为480×152，建造独立编辑Resources/UI/ConstructionCue.prefab。
