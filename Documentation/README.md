# 工程导航

## 从哪里开始

| 要做的事 | 入口 |
| --- | --- |
| 理解启动、章节、战斗、结局与存档 | [ARCHITECTURE.md](ARCHITECTURE.md) |
| 找脚本和资源 | [PROJECT_MAP.md](PROJECT_MAP.md) |
| 修改章节、难度、配乐、字幕、字体 | [Guides/Editing.md](Guides/Editing.md) |
| 修改酒馆人物、Timeline 和对白 | [Guides/Timeline.md](Guides/Timeline.md) |
| 编译、检查引用和运行验证 | [Guides/Verification.md](Guides/Verification.md) |
| 控制台命令 | [Guides/DebugConsole.md](Guides/DebugConsole.md) |

Unity 中的 **街垒 → 工程导航** 菜单提供对应快捷入口。目录移动已保留原脚本/资源 GUID，场景名与 Resources 名称不变。

## 当前功能位置

| 功能 | 主要代码/配置 |
| --- | --- |
| 战役入口、阶段调度 | `Assets/Script/Runtime/Campaign/Flow/StoryDirector.cs` |
| 建造输入与自然换字 | `Runtime/Construction/`、`StoryDirector.Construction.cs` |
| 建造至防守过渡 | `StoryDirector.PreparationTransition.cs` |
| 目标选择、射击、换轨 | `Runtime/Combat/Player/` |
| 敌人行为与波次 | `Runtime/Combat/Enemies/`、`Runtime/Combat/Waves/` |
| 受击停顿 | `Runtime/Animation/Enemies/CampaignReactionVisual.cs` 与移动控制器 |
| 准星显示 | `Runtime/UI/HUD/TargetMarker.cs`、`TargetMarkerContrast.cs` |
| 音乐与歌词 | `Runtime/Audio/`、`Runtime/UI/Cinematics/CampaignMusicLyricsView.cs` |
| 结局 | `StoryDirector.Endings.cs`、`CampaignCinematicView.cs`、`CampaignEndingView.cs` |
| 存读档 | `Runtime/Persistence/`、`StoryDirector.Persistence.cs` |
| 酒馆对白与名字回复 | `Runtime/UI/Cinematics/Tavern*`、`NameResponseTable.cs` |

表内 `Runtime/...` 均相对于 `Assets/Script/`；主控部分文件位于 `Runtime/Campaign/Flow/`。

历史报告集中在 [History](History)，其中的旧玩法、路径与验证数量可能已被后续改动替代。
