# 第1832次心跳

以巴黎街垒为背景的 2D 节奏防守游戏。Unity **2022.3.62f3c1**。

打开 `Assets/Scenes/Title.unity` 后进入 Play。编辑器菜单 **街垒 → 工程导航** 可直接打开主菜单、建造、防守和酒馆场景，或定位章节、配乐字幕、字体和对白配置。

- [工程地图与常用修改位置](Documentation/README.md)
- [运行架构与模块职责](Documentation/ARCHITECTURE.md)
- [章节、音乐、UI和素材编辑](Documentation/Guides/Editing.md)
- [酒馆 Timeline 与对白](Documentation/Guides/Timeline.md)
- [验证与维护](Documentation/Guides/Verification.md)
- [接手说明](HANDOVER.md)

运行时脚本位于 `Assets/Script/Runtime`，编辑器工具位于 `Assets/Script/Editor`。素材按 Art、Audio、UI、Cinematics 分类；Resources 保留原加载路径。

先前的阶段报告已收进 [Documentation/History](Documentation/History)，用于回查历史；当前行为以现有配置和上面的指南为准。
