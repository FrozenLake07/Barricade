# 酒馆 Timeline 与对白

制作场景仍为Assets/Scenes/3.unity，可通过“街垒/工程导航/场景/酒馆 Timeline”打开。

- 舞台预制体：Assets/Cinematics/TavernUI/Prefabs/TavernUI_AnimationStage.prefab。
- 正式酒馆Timeline：Assets/Cinematics/TavernUI/Timelines/TavernIntroduction.playable。
- 独立制作工作台：Assets/Cinematics/TavernUI/Timelines/TavernUI_Workbench.playable。
- 对白配置：Assets/Cinematics/TavernUI/Data/TavernDialogue.asset。
- 特殊名字回复：Assets/Cinematics/TavernUI/Data/NameResponses.asset，文本源通过该资产的引用编辑。

对话与名字输入由TavernNameDialogue处理，Timeline信号触发后暂停，玩家确认后继续。第三、第四段均由Enjolras发言。离场顺序为Enjolras、主角、Grantaire；Grantaire最后喝一口再走。

人物的RectTransform位置轨道负责走位，TavernActorDepth.sortingOrder轨道负责前后遮挡，经过门槛时切到门框后方。大幅改动人物纵向路线时同步检查图层关键帧。正式Timeline和工作台是两个资产，修改前确认Director引用。

战役通过ChapterTable、章节分镜配置与StoryDirector调度，不能用Unity场景编号直接等同章节。结局音乐信号PlayHopeFinale保留兼容接口，当前连续配乐从结局入口已开始，重复信号不会重播。

## 2026-10-26 酒馆布景同步

正式过场仍由ChapterTable第2章的openingSequence实例化TavernUI_AnimationStage.prefab，不加载场景3。已将场景3保存的布景同步至该预制体：移动35/36/37号绿色酒瓶，删除38/39/40号绿色酒瓶，隐藏绳索，门扇位置改为(291,-39.5)。人物站位、走位、开门时间及Timeline不变。

同步通过Unity PrefabUtility完成。保留所有剩余对象的相对Sibling顺序、GameObject Layer、Canvas sortingLayerID/sortingOrder/overrideSorting及TavernActorDepth值；动画和Timeline文件逐一哈希核验未变。预制体GUID和第2章资源引用保留，场景3文件未改动。原生渲染的同步后预制体与场景3布置参考PNG完全相同。

编辑器工具：Assets/Script/Editor/Tools/Cinematics/TavernSceneLayoutSync.cs。仅同步布景白名单，不整体应用场景实例的Overrides，避免覆盖图层和演出配置。若以后新增布景对象，工具会要求先明确其图层位置；现有布景的位置与显隐可同步。运行前退出Play和动画预览；未保存的场景3已加载时使用当前编辑实例，否则读取保存的场景3。备份与核验报告在本次输出目录。
