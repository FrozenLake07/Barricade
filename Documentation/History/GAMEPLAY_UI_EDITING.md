> 2026-10-22 心形更新：Style.asset和AmmoHUD.prefab当前采用Heart-Classic-v4 / HeartEmpty-Classic-v4（16×16），暗红/灰色边缘替代近黑块；两个heartTint均为白色。母版与验证见Assets/Art/UI/Paris1832/PROMPTS_HEART_EDGE.md。布局、心跳与HP计算不变。

# 局内界面编辑（2026-10-21）

本轮延续庄重宽幅菜单的米灰文字、细线、深红点缀和暗场，但局内更重视操作可读性、场景视野和信息层级。已接入候选版，未标为已获用户美术确认。

## 画面结构

顶部用一张全宽、向下渐隐的信息底色替代左右零散黑块。左侧章节标题42号字、操作26号、任务26号分层；右侧为480×152的心脏/弹药组，余弹数字46号，圆铅弹和装填提示不再套木框。底部短提示使用全宽向上渐隐底色，不再出现独立矩形牌子。所有装饰均不拦截鼠标。

建造提示位于底部中央：小号「建造/修补」、大号当前键、细灰线及短红线。正常双键字号70，单键/休息76；原文字换题脉冲收敛为1.12倍/0.16秒。只有现有Tap接受输入后才显示0.65秒「合拍/继续建造/继续修补」；错按后的REST继续显示原有休息秒数，行动标签变为「稍作喘息」。不读取或提前提示Perfect窗口，不新增节拍/声音/奖励。

## 直接编辑位置

- `Assets/UI/Campaign/StartScreen.prefab`：`GameplayTopVeil`为顶部暗场，`GameplayFineRule/GameplayRedRule`为细线；`ChapterLeather/GameplayHeading`、`GameplayControls`、`GameplayObjective`分别编辑章节/操作/任务位置、字号和颜色。ChapterLeather为透明容器。旧`ChapterHUD`仅作Director/存档的数据源，其Text.enabled=false；不要删除引用或重新启用，否则会重复显示。
- `GameplayBottomVeil`为提示底色，`NarrativeCaption`为短提示。顶部暗场只在应显示生命提示的游玩阶段出现，底部暗场随实际提示出现；标题页隐藏。`CampaignGameplayHud`只分层呈现原HUD文本，任务另起一行；StoryDirector仅改变第8章目标的换行，不改变内容/目标数值。
- `Assets/Resources/UI/AmmoHUD.prefab`：参考1920×1080，尺寸480×152，右上锚点/偏移(-40,-20)。移动根节点一起移动心脏与弹药。`HeartPulse`在左、`AmmoSlots`和文字在右；`HeartAmmoDivider`、`HeartBaseRule`、`AmmoFineRule`为装饰。`VitalsBackdrop`和旧`LeadBallTray`保留引用但透明，不重新生成黑底。心脏最大1.35倍搏动与分隔线仍留间距。弹药容量按实际3/4发显示。
- `Assets/Resources/UI/ConstructionCue.prefab`：独立可编辑的建造提示；根节点锚定画面35%～65%宽、11%～28%高，`ActionLabel`为状态、`KeyLabel`为按键、`CueFineRule/CueRedRule`为装饰、`CueShade`为非常淡的局部可读性底色。运行时优先挂在战役Canvas上，统一缩放；独立调试场景使用原Canvas。移动根节点即可整体改位。字号取预制体KeyLabel初始值，双键减6，不受旧存档里原提示字号影响。
- `ParisSceneUI.Construction`保留原Tap和KeyPulse，隐藏原WordUI的文字绘制与旧计数，绑定ConstructionCueView。新界面镜像现有文本、脉冲和InputAccepted事件；旧场景卸载后因source为空而隐藏，下次场景绑定复用，不保留旧计时器。
- `Assets/Resources/Campaign/GameFont.asset`现在指向`Assets/TTF/Silver-BarricadeUI.ttf`。这是原Silver的独立UI派生副本，只新增缺失的简体「垒」（U+5792），用同样100单位像素网格的轮廓绘制；原13343个字形和13340个Unicode映射、原版权/许可记录完整保留。原Silver.ttf未修改。字体内部family改为Silver Barricade UI，3个活动UI预制体及运行时统一引用新字体，避免单字掉到异体字体。字标仍使用上一版图片，章节配置中的名字不改写。

## 保留规则与验证

页面仍不显示拍数/HP数字；心脏镜像原Heartbeat/HeartFill，无新计时或声音组件。第四章同一小孩三段全部成功才+1容量（3→4），跨章与存档保留；隐藏装填效果不公开。章节名、时长、难度、波次、音乐、指纹不变。

87个实际运行时源文件、20个编辑器源文件对Unity程序集独立编译零错误/零警告；590项原逻辑回归与CSV、2978项资源/预制体/呈现检查通过，包含原按钮动作不变、原场景不变、Director只改目标换行、心脏最大搏动与建造按键脉冲间距、所有装饰点击穿透。字体独立检查确认全部原轮廓/映射与版权许可记录保留、只加U+5792、TTF校验和正确；Windows字体加载和离线排版可用。

工作区outputs/局内界面统一有14张离线预览、修改前备份、编译/回归/资源/字体报告和最终哈希。预览由真实预制体和实际字体生成，包含建造/修补合拍/休息、战斗/受损扩容/缺弹、第8章任务，背景为共用场景示意，不代表各章节实际关卡构图。它们不是Unity运行截图。Editor此前退出140063且无日志的问题仍未解决，本轮未重复失败启动；原生Canvas/字体渲染、动态绑定、暂停/读档/跨章和声画同步仍需Play验收。可以启动Editor后先重新进入Play加载新字体，执行「街垒/界面/检查巴黎像素界面与护送扩容」，再从Title完成建造→战斗→第四章全护送扩容→暂停/存读档→跨章。
