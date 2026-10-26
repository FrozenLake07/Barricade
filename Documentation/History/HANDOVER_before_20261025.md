# 《街垒》项目交接文档

> 面向接手的 AI 代理或开发者。读完这一份应该能独立改这个工程。
> 最近整理于 2026-10-23 · Unity **2022.3.62f3c1** · Assets/Script 内 137 个脚本（106 个运行时、31 个编辑器）

---

## 最新补充：三版随机封面（2026-10-23）

- 已将用户ZIP的A群像、B守夜者背影、C黎明街垒三张320×180原图接入Resources/UI/Paris1832/Covers，未改PNG像素，Point/Uncompressed/无MipMap。ParisUIStyle.titleCovers按A/B/C排列，旧titleBackground缺省也替换为A。
- 采用随机方案：StartScreenView.ShowTitle每次进入/返回主界面抽图，排除上一版，停留不轮播；独立System.Random不消耗战斗Unity随机数。A/C菜单左侧，B右侧；暗场镜像，背景原色等比铺满。标题《第1832次心跳》与原菜单功能保留。
- 原生保存Style、开始页预制体与Title场景。106+31脚本及发布版编译通过，原生Play57项（帧1→46）和三版截图检查通过。详见TITLE_COVERS_20261023.md，报告outputs/三版封面接入，备份work/title-covers/backup。素材包说明未当作执行指令。未更改存档或战役配置指纹。

## 前次：游戏标题（2026-10-23）

- 用户确定将显示标题改为《第1832次心跳》。ParisUI.GameTitle为运行时标题来源；ParisUITheme使用现有Silver像素字体，隐藏旧“街垒”图片字标，扩展标题文字区域。Assets/UI/Campaign/StartScreen.prefab及Title场景已由原生Editor保存，所有标题字符通过字体检查。旧标题图片资源保留，编辑器内“街垒”工具菜单与剧情中街垒一词不替换。
- 本次仅改标题显示，不改变PlayerSettings.productName、程序标识或存档目录。原生操作报告outputs/标题修改/标题修改.json，修改前预制体/场景备份work/title-rename/backup。

## 前次：敌人密度与实际射击（2026-10-23）

- 修复waveControlled士兵直接return绕过射击：走入画面并完成准备后，错开首枪，第三章5拍/中段4拍/后段3拍一枪；动画开始0.25秒后发弹，举枪停步约0.667秒，画外不射击、死亡打断、跳拍不补枪。敌弹连续碰撞且实际扣墙血。掷弹兵不加普通枪击。旧独立场景5%/3s开火概率改为50%/3s。
- pressure-v4：前中段每3拍、后段每2拍增援，持续到收尾前；计划敌人数3～9章分别89/101/107/107/106/132/179，A/B同量镜像。在场上限12/14/14/14/16/16/16。镜头左侧画外1.25单位生成，保留第三章首次提示波。
- 原生Play69项（帧1→1014）通过：无玩家操作、无敌、10倍时间的前30拍，第3/4/7/9章可见峰值12/13/16/16，实际射击32/56/86/90次。验证真实敌弹扣血、画外/冻结限制、死亡取消及读档只发一次。106+29脚本及发布版编译通过；14份波次1084事件、战役72项通过。实际截图已查看，不宣称通关胜率已验证。
- 详见ENEMY_PRESSURE_20261023.md，报告outputs/敌人密度与射击修复，备份work/enemy-pressure/backup。schema9、标签combat-v4-density-fire-20261023；旧配置存档原文件保留但需新开。上一轮炸弹伤害、结局、音乐、QTE规则保留。

## 前次：动作对齐、伤害、双结局与QTE（2026-10-23）

- 五组60帧敌人反应通过原生TextureImporter按各原模型身高、脚底和首帧脚部横向锚点对齐，组内固定PPU；取消ReactionVisual的额外-0.16偏移，PNG未改。手榴弹基础伤害50→20，仍乘章节倍率；同组只结算一次，火炮规则不变。
- 建造换题加入180ms旧字母宽限：忽略不奖励、不休息，新题照常响应；双键120ms规则保留，宽限与双键计时支持读档。战役Tap.Start不再临时随机出另一套提示；独立Tap按对立即换字保留。自然换题仍在完整Perfect结束之后。
- 第1至8章openingMusic引用既有People Sing，第1/2章连续，第3至8章每章重播；第9章保持静音，标题/校音静音。可在ChapterTable分别编辑。
- 第10章直接RunEnding，取消普通场景和183拍空尾声：哀悼末图等音乐结束→虚化→三按钮（重开来源本章/标题/退出）；希望D开始Finale，后四图按歌曲扣18秒制作人员表后的时长拉伸→黑屏制作人员→音乐结束回Title。未提供名单，creditsText仅有原作署名与感谢。原来源章节起始检查点保留，重开按钮已验。
- 106运行时+28编辑器及发布版编译通过。原生Play129项（帧1→138）与实际截图已检查；API加速输入、长镜头和音乐结束，不宣称手动键盘或全曲试听。详细说明CURRENT_FIXES_20261023.md；报告outputs/当前问题修复。schema8，旧配置存档保留但需新开。标题名称仅给建议，未改工程。

## 前次：建造自然换字与HUD重影（2026-10-23）

- 用户澄清：保留按对后立即换字，只检查自然换字。未修改成功输入处理；战役自然换字使用Clock.Completed，在Wait+Window结束后触发，进入Perfect时的Opened不换题。直接打开建造场景的旧Tap漏拍保持原字母，也没有提前自然换字。
- 修复左上角旧ChapterHUD被StoryDirector.UpdateHUD每帧重新启用，覆盖新版GameplayHeading/Controls/Objective的问题。旧Text继续作为文本数据源，显示保持关闭；DebugHud也不再重开它。分栏HUD在文本未变化时仍同步hideHud和关闭旧源。
- 独立编译104运行时+26编辑器脚本通过；实际CampaignBeatClock/ConstructionJudgement源码边界49项通过（等待、窗口开始、结束前、结束后、成功占用、下拍加速和双键）。Unity当前未运行，本次未做原生Play或画面验收，不能沿用上次Play报告作为本次验收。报告在outputs/建造换字与HUD修复。

## 章节机制与角色动画（2026-10-23）

- 用户当前确定：第七章取消玩家抉择，原7拍并入战斗：修补8+防守135=143拍；固定播放释放沙威剧情，取消回血、减敌及第八章节奏奖惩。全战役1830拍不变，A/B波次已同步并补充压力事件。
- 三四章不消耗弹药、不需要补弹且不显示弹药HUD，仍一拍一发；第五至九章启用有限携弹与补弹。第九章原默认无限弹药特例取消，均读取ChapterDefinition.ammoEnabled；调试infinite可覆盖。第四章完整三段救援+1容量保留，提示第五章起生效；隐藏补弹倍率仍保留。
- 沙威第五章开场已被俘：移除两次可射杀Decoy波次，不再击杀得分；第六章删除SpySpared减敌关联。第五章低压开始提示弹药操作，未加入总弹药库存或搬箱系统。
- 第八章无强制拦截要求：requiredShellInterceptions=0，A/B仅70、100拍两次炮击，110拍停止攻击并撤离画外。两发全中仅炮击损失50%最大血量，可存活；不是无敌。炮击仍25%上限伤害，血量不足或其他敌人仍可导致失败。第九章重新从左侧部署火炮。
- 85张原始透明PNG已复制进Resources/CampaignActors/Frames，7套各12帧+静态伽弗洛什，哈希验证原件未改。资源库ActorArt、7份预览/Timeline动画Clip及Gavroche.prefab已在原生Editor生成保存；使用原始帧而非灰底预览，无图像重绘或手改meta。
- GavrocheVisual驱动跑出/停下捡拾/返回；第四章不再是主角剪影。CampaignReactionVisual叠加士兵与掷弹兵中弹、死亡及投弹预备；EnemyHP.TakeDamage统一命中入口，致命命中立刻停用碰撞/移动，死亡单次播放后停留淡出。三掷弹兵生命独立、存活者齐投，投弹预备1秒后衔接原投掷释放姿态。新状态支持存读档，尸体不复活。
- 编译104运行时/26编辑器及发布版通过；原生机制动画Play55项（帧1→229）、控制台回归81项（帧1→121）、战役资源72项、14份A/B共658事件通过。控制台报告明确跳过Editor逐帧状态下Timeline播放/恢复验收；未宣称完整战役通关或人工听感/难度胜率已测试。原生截图已查看。
- 测试已退出Play回Title并恢复runInBackground=0；未覆盖未保存场景。完整接口和限制见CAMPAIGN_MECHANICS_20261023.md，报告outputs/章节机制与动画，修改前备份work/campaign-art-update/backup及work/chapter7-no-choice/backup。配置指纹schema7，旧配置存档保留文件并提示新开。
- 第六章其他玩法、第七章协助选择、第八章压制炮组、第九章目标重写及终章缩时只是前期建议，不应视为已选定或已实现。

## 最新交接：双结局与音乐（2026-10-22）

- 用户最终确认：校音结束后才开始People Sing；第一二章切换不中断、不归零；129.89秒自然播完后静音，不循环。标题/校音静音，开场黑屏已在歌曲内。本次没有为3—8章另选音乐。
- 第9章之前最终失败直接进第10章，不显示重试：Mourning结局一，Empty Chairs At Empty Tables（195.88秒）。第9章失败或正常结束均Hope结局二，终章开始静音，到章首D镜头马里于斯转向门外亮光时播放Finale（042.m4a，167.07秒）。
- 第10章已改名“终章”；报幕/HUD/存档描述读取可编辑章节名字。终章原有183拍及战役1830拍预算不变，歌曲结束不重播。
- 三首源音频来自E:\music\悲惨世界，只读验证哈希；导入Assets/Audio/CampaignRecordings（Streaming/Vorbis）。CampaignRoot的chapterMusic/mourningMusic/finaleMusic已在原生Editor赋值并保存。
- Cinematics增加路线ending筛选与镜头triggerHopeFinale；哀悼只用ABC和末尾C，不执行希望Timeline；希望用ABCD及EFG。D镜头配置触发，跳过补发一次；Timeline公开接口StoryDirector.PlayHopeFinale()，重复调用不重播。
- RunState新增Ending/EndingSourceChapter/HopeMusicStarted；存档保存musicTrack+musicTime，恢复正确曲目/进度；编辑器暂停也不再误判EOF。run.json新增结局字段，events记录ending_selected/ending_hope_music。配置指纹schema5，旧配置存档保留文件但提示新开。
- 第6章仅完成评估，没有改动机制/波次/难度/名字。夜袭主题成立，但“敌军扔火把”文案与实际炸弹+固定遮罩不符。四案为：暗巷夜袭、马里于斯火药桶威慑、夜间警戒修补、守方火光/暴露取舍。优先建议低成本暗巷夜袭，追求原著高潮则选火药桶。
- 验证：运行时101/编辑器24及发布版条件编译通过；原生结局Play41项（帧1→117）、既有控制台Play72项（帧1→121）、战役资源70项通过；不是完整十章通关或人工听感测试。原生回归已退出Play回到Title；最终检查时编辑器已退出，资产已保存在磁盘。
- 完整配置/改进方案见ENDING_MUSIC.md；报告在本次Codex工作目录outputs/双结局与音乐；源码/资产修改前备份在work/ending-music/backup。

## 最新交接：调试控制台（2026-10-22）

### 控制台暂停更新（2026-10-22）

打开控制台保存 Time.timeScale / AudioListener.pause，暂停模拟、音乐心跳、倒数与自动战斗/过场推进；Timeline临时保持在Manual时钟，关闭恢复原更新模式和播放位置，已停止或已换资源的Timeline不会被错误重启。关闭恢复原倍率与声音状态，原先已暂停则继续暂停；time设置关闭后的倍率，step可暂时推进指定帧。暂停统计计入pausedSeconds；暂停中存档保留音乐播放状态。

Unity2022运行时100及Editor23脚本编译通过；新增暂停/恢复/Unscaled Timeline/节拍/time命令检查，原生Play总72项通过，帧计数1→121。报告位于本次Codex工作目录 outputs/控制台暂停。实体按键/IME未冒称自动化覆盖。


- 已接入运行时 uGUI 控制台：从 Title Play，按 **` / ~** 开关，Esc 关闭；47个命令，帮助/历史/Tab补全、最近20行、滚轮、复制输出、分号批量与 exec 脚本。
- 快速入口：`ch 9 hard; hp max 600; hp set 600`；`time 0` 暂停，`step 3` 逐帧，`time 1` 恢复。打开控制台自动暂停游戏、声音与Timeline；关闭恢复原状态。time命令调整关闭后的倍率，step仍可逐帧。
- `reset/load/clear` 需另行 `confirm`；`cancel` 清待执行队列。第2章保持纯建造，hard会明确拒绝；phase只允许本章配置的阶段。
- 编辑器/Development Build默认启用，普通发布版需 `-console`。没有任意C#执行入口；set仅公开标量白名单。
- 运行时覆盖不写 ChapterTable/WaveScript；调试槽独立于主存档。run.json 的 nonDesign/debugLabel、events.jsonl 和 summary.csv 明确标记“非设计值”；试玩对比会排除调试记录。
- 原有 CampaignVerification/WaveVerification 由 verify 命令复用；修正字体检查：预制体可保留原Silver回退，实际运行必须采用GameFont的补字版。
- 手册与全部命令：`DEBUG_CONSOLE.md`；脚本示例：`DebugConsoleExamples/siege9.txt`。默认数据目录位于 `Application.persistentDataPath/DebugConsole`。
- 主要源码：`Assets/Script/Debug/`、`Core/StoryDirector.Debug.cs`、`Enemies/WaveDirector.Debug.cs`；原有场景/预制体无需手动追加组件，StoryDirector.Awake自动安装。
- 已验证：Unity2022真实DLL编译运行时100/Editor23，发布版条件编译无错；3026项逻辑检查；原生Play60项，帧计数1→104；原有战役65项、14份A/B波次662事件校验通过；15份章节/波次资产哈希保持一致。
- 自动Play检查使用受限Temp请求/菜单入口，在后台首帧停滞时回退到编辑器逐帧；退出后恢复暂停和runInBackground设置。默认启动不会自动跑测试。自动检查入口：街垒/测试/检查调试控制台（Play模式），要求已保存Title场景。
- 自动化通过API与SetOpen验证流程/UI；实体键盘、IME与听感仍需人工验收，未宣称完整十章通关。验收报告/截图在本次Codex工作目录 outputs/调试控制台。


## 0. 三十秒速览

一个**跟着音乐节拍、守住街垒**的 2D 节奏防守游戏。灵感来自《悲惨世界》与 1832 年巴黎共和党人起义。

```
        x = -10                  x = 4.88         x = 6.11
  S1 ●  y = -4.5
  S2 ●  y = -2      ──敌人向右──►  ▓▓▓▓▓  ──►   ▲ Capsule
  S3 ●  y =  0.5                  街垒          玩家（不动）
  三个生成点 = 三条轨道          tag = Wall      A/D 换攻击轨道
                                 WallHP 1000     W/S 选空中/地面，Space 开火
```

- **两条场景是一局的上下半场**：场景 1 攒街垒血量，场景 2 花掉它。
- **一个节拍管所有事**：玩家开火的完美窗口、敌人的停火节奏、UI 心脏的搏动、以及将来的音乐。
- **心脏一物两用**：既是节奏指示器（跟着拍子跳），又是血条（跟着街垒血量从上往下变灰）。

---

## 1. 工程地图

```
Assets/
├─ Scenes/
│  ├─ 1.unity          建造阶段（节奏 QTE，攒街垒血量）
│  └─ 2.unity          抗击阶段（正式玩法）
├─ Script/             按功能分类；目录及用途见 Script/README.md 与本文末尾
├─ Resources/
│  ├─ enemy/           Soldier.prefab  Leader.prefab
│  └─ arrow/           Bullet.prefab   Bomb.prefab
└─ Test1.cs            遗留脚本，见 §9
ProjectSettings/
└─ TagManager.asset    自定义 Tag 只有 "Wall"；没有自定义 Layer
```

**加载方式统一是 `Resources.Load<GameObject>("enemy/Soldier")` 这种按名字点名**，
不要再用 `Resources.LoadAll` + 下标 —— 这个坑踩过两次，见 §8.2。

---

## 2. 场景 2 的物体与挂载

| 物体 | 挂的脚本 | 说明 |
| --- | --- | --- |
| `Capsule` (tag **Player**) | `TapSec` + `CapsuleTrack` | 玩家。**原地不动**，A/D 只换攻击轨道 |
| `BulletS` | `Follow` + `SBullet` + `TargetMarker` | 枪口，每帧跟到玩家身上；开火与准星都在这里 |
| `S1` / `S2` / `S3` | `Spawn` | 三个生成点，y 坐标同时是三条轨道的高度 |
| `HeartSet` | `HeartFill` + `Heartbeat` | 心脏。子物体 `GreyHeart`（灰，常驻）与 `RedHeart`（红，Filled/Vertical/Bottom） |
| `Wall` (tag **Wall**) | SpriteRenderer + Collider2D | 街垒，世界坐标 x ≈ 4.88，在玩家左侧 |
| `Canvas` / `EventSystem` | Unity 自带 | — |
| `Circle` | SpriteRenderer + Rigidbody2D | 无用残留，可以删 |

**预制体：**

| 预制体 | 脚本 | 碰撞体 | 关键设置 |
| --- | --- | --- | --- |
| `enemy/Soldier` | `EnemyMove`(role=Soldier) + `EnemyHP`(100) + `WallDamage` | BoxCollider2D **IsTrigger** | 缩放 (0.5, 1) |
| `enemy/Leader` | `EnemyMove`(role=Leader) + `EnemyHP`(100) + `WallDamage` | BoxCollider2D **IsTrigger** | speed 0.5 |
| `arrow/Bullet` | `ArrowDefinition` + `BulletHit` | BoxCollider2D **IsTrigger** | **GravityScale 0**（直线飞） |
| `arrow/Bomb` | `BombHP`(100) + `WallDamage` | CircleCollider2D **IsTrigger** | **GravityScale 0.25**（走抛物线） |

> 四个物体**全部是 Trigger**。这是有意的：非 Trigger 会让敌人之间物理互推，破坏队形。

---

## 3. 核心机制

### 3.1 节拍（全场时钟）

**注意：工程里有两套互不相干的节拍系统。**

| | 玩家节拍 | 敌人节拍 |
| --- | --- | --- |
| 脚本 | `Tap`（场景1）/ `TapSec`（场景2） | `Beat` |
| 驱动 | 自己递减 `beattime`，**和音乐无关** | 由 `Time.time` 推算 |
| 影响 | 开火的完美判定、UI 心脏搏动 | 敌人何时停下、开火、掷弹 |

**玩家节拍的工作方式：**

```
beattime:  Setbeattime(1.0) ──递减──► 0
                                      │──► 完美窗口：perfecttime(0.5) 开始递减
                                      │     └ 在此期间按对键 = 完美
                                      └──► 窗口超时 = 未命中
```

- 完美命中 → `Setbeattime -= 0.05`（下限 0.5）→ **曲子越来越快**
- 非完美 → `Setbeattime = oribeattime`（回到 1.0）→ **惩罚**
- 场景 1 额外：非完美后再**停滞 10 秒**（`Tap.restTime`，可调）

两个场景的 `beattime` **不互通**（不同的类、不同的静态字段）。场景 1 打得多快不会带进场景 2。

**敌人节拍（`Beat`）：** 用组号算出相位偏移，所以**同一批敌人完全同步、不同批互不同步**。
`Spawn` 在生成时把 `groupId` 注入给每个 `EnemyMove`。骰子用整数哈希（`Beat.Hash`）而不是 `System.Random` —— 见 §6.6。

### 3.2 两个阶段的衔接

```
场景 1（建造，约 5 分钟）
   WallCharge.Start()  →  GameManager.WallHP = 0
   完美按下            →  WallCharge.OnPerfect() → GameManager.AddWallHP(10)  上限 WallMaxHP(1000)
        │
        │  WallHP 是 static，跨场景保留
        ▼
场景 2（抗击）
   HeartFill.Start()   →  maxHP = 进场时的 WallHP      ← 攒到 300 就以 300 为满格
   灰心比例            →  1 − WallHP / maxHP
   挨打                →  WallDamage → GameManager.DamageWall(kind)
```

**所以场景 1 是玩家给自己设定难度的地方**：攒得多，下半场扛得久。

### 3.3 战斗

**玩家（`SBullet.Fire`，由 `TapSec` 调用）：**

| 状态 | 键 | 锁定目标 | 子弹能伤谁 |
| --- | --- | --- | --- |
| Up（空中） | `W` | 攻击轨道上最近的**手榴弹** | 只有 `BombHP` |
| Down（地面） | `S` | 攻击轨道上最近的**敌人** | 只有 `EnemyHP` |
| — | `Space` | 用当前状态开火 | — |

- 伤害：完美 **100** / 非完美 **50**。敌人 HP 100 → **完美一击死，非完美两下**。
- **严格类型隔离**：子弹带着 `ArrowDefinition.targetKind`，扫到另一类会直接穿过去（不结算、不消耗）。
- **轨道隔离**：子弹带着 `trackY`，只结算 y 在 `trackRange`(1.25) 内的目标。
- **预判**：`SBullet.TryLead()` 解 `|d + v·t| = bulletSpeed·t` 取最小正根，瞄准目标将会在的位置。
- 攻击轨道上**什么都没有** → 不发射，并额外压一档节拍（`Penalty()`）。

**敌人（`EnemyMove`）：** 沿 +x 前进；出生后 `settleTime`(2秒) 内只前进防止挤在一起；之后按 `Beat` 每周期停下做一次动作：

- Soldier：25% 概率停下开火（直线弹）
- Leader：20% 概率掷弹（抛物线，抛向 `Spawn.target`）+ 20% 概率开火（直线弹）
- 掷弹要求 `CanReach`（`sin(2θ) = d·g/v² ≤ 1`），够不到就退化成直线开火

**伤害数字（`GameManager`）：**

| 撞墙者 | 扣街垒血 | 结果 |
| --- | --- | --- |
| 敌人 | 10 | 敌人消失 |
| 敌人的子弹 | 1 | 子弹消失 |
| Bomb | 50 | 炸弹消失 |
| 玩家的子弹 | 0 | 穿墙而过 |

---

## 4. 脚本清单

### 活跃（在用）

| 脚本 | 行数 | 职责 |
| --- | --- | --- |
| `GameManager` | 40 | 全局静态状态：`WallHP` / `WallMaxHP` / `DamageWall` / `AddWallHP` / `IsWall`。**不需要挂载** |
| `Beat` | 44 | 敌人用的全局节拍（按组相位偏移 + 哈希骰子） |
| `Spawn` | 182 | 生成点：类型抽取、刷怪率、音乐模式、中后期、注入 `groupId` 与 `target` |
| `EnemyMove` | 132 | 敌人行为：前进 / 停 / 开火 / 掷弹 |
| `EnemyHP` / `BombHP` | 6 | 纯数据组件（HP=100），同时是"可被击中的目标"的识别标记 |
| `ArrowDefinition` | 24 | 子弹的数据：`Damage` / `fromPlayer` / `trackY` / `trackRange` / `targetKind` |
| `BulletHit` | 59 | 子弹的碰撞结算：撞墙 / 按类型和轨道结算伤害 / 命中后自毁 |
| `WallDamage` | 18 | 挂在敌人和炸弹上：撞墙就扣血并自毁（扣多少按身上是 `BombHP` 还是 `EnemyHP` 自动判断） |
| `TapSec` | 113 | 场景 2 的玩家节拍与输入（W/S/Space）。`cnt`、`ShootStatu` 是 public static |
| `CapsuleTrack` | 99 | 玩家的攻击轨道：A/D 上下切换，广播 `AttackY`；**玩家不动** |
| `SBullet` | 198 | 开火：选目标、预判瞄准、生成子弹。`CurrentTarget()` 给准星用 |
| `Follow` | 20 | 让枪口每帧跟到玩家身上 |
| `TargetMarker` | 61 | 把准星贴到 `SBullet.CurrentTarget()` 上 |
| `HeartFill` | 53 | 心脏血条：驱动 `RedHeart.fillAmount` |
| `Heartbeat` | 110 | 心脏搏动：跟 `beattime` 跳，完美窗口内静止 |
| `Tap` | 129 | 场景 1 的节拍与 QTE（随机出 A/D/W/S） |
| `WallCharge` | 29 | 场景 1 攒血：进场清 0，每次完美 +N |
| `KeyPulse` | 46 | 场景 1 的 Key 文字切换时脉冲一下 |

### 遗留 / 死代码（见 §9）

`Test1` · `AiLeaderMove` · `LeaderEnemMove` · `SBomb`

---

## 5. 关键数据流

```
TapSec.ShootStatu  ──┐
                     ├─► SBullet.CurrentTarget() ─► TargetMarker（准星）
CapsuleTrack.AttackY ┘         │
                               ▼
                        SBullet.Fire()
                               │  写入 targetKind / trackY / fromPlayer / Damage
                               ▼
                    ArrowDefinition（子弹身上）
                               │
                               ▼
                        BulletHit.OnTriggerEnter2D
                           ├ 撞 Wall ？→ 敌弹扣 1 并消失；玩家子弹穿墙
                           ├ 非玩家子弹 ？→ 放行（见 §6.3）
                           ├ y 不在 trackY 附近 ？→ 放行
                           └ 按 targetKind 只结算对应的一类
```

**跨脚本通信用的是 `public static`，不拖引用。** 这是本工程贯穿始终的写法：
`GameManager.*`、`TapSec.*`、`Tap.*`、`Beat.*`、`CapsuleTrack.AttackY`。

---

## 6. 必须知道的"为什么"

这些是看起来奇怪、但都是**故意**的设计，别当成 bug 改掉。

**6.1 玩家原地不动** —— `CapsuleTrack` 只在 `Start` 就位一次。A/D 只改 `AttackAttackY`。主题上街垒不会移动，你是守墙的人。策划案原本写 Wasd 四向移动，但同一份文档又说"敌人的子弹不会打中主角"——那就没有东西能伤到你，移动就没有意义。**这是有意选择"炮台"方案。**

**6.2 子弹从玩家处射出，不是从攻击轨道高度** —— 因为子弹是**瞄准飞行**的（朝目标飞），不是沿轨道平射。曾经为了让子弹走在轨道高度上加过一个 `Muzzle()`，改成自动瞄准后就不需要了。

**6.3 敌人的子弹是无害的** —— `BulletHit` 里 `if (!def.fromPlayer) return;`。原因见 §8.4：敌我共用同一个 `Bullet.prefab`，而子弹生成在开火者自己的 Trigger 里，不挡住的话它会当场把自己打掉（表现为"开火没有子弹"）。

**6.4 全 Trigger** —— 见 §2 的表注。

**6.5 `Bomb` 的 `GravityScale` 必须非 0，`Bullet` 必须为 0** —— 掷弹走抛物线需要重力；开火走直线需要没有重力。**这两个数值曾经给出过互相矛盾的建议，以现在为准。**

**6.6 `Beat` 用整数哈希而不是 `System.Random`** —— .NET 的 `Random` 在**连续种子**下首个值高度相关（会形成一条"阶梯"），按组掷骰子时相邻组会拿到雷同结果，破坏"组间不同步"。而且它每帧要 new 2~3 次，有 GC 压力。哈希是纯函数、零分配。

**6.7 心脏的灰是从上往下推进的** —— 用 `Image.Type = Filled` + `Vertical` + `FillOrigin = Bottom`，`fillAmount = WallHP / 进场血量`。**没碰过颜色**，只动了裁剪。`Filled` 裁的是贴图矩形而不是像素，所以心形会被横切一条平边，这正是"液面"感。

**6.8 `Heartbeat` 的幅度靠"进度 p"驱动** —— `p = 1 − beattime/Setbeattime`。p 从 0 到 1，幅度 0.05→0.35、周期 0.7→0.12 秒，所以越接近窗口跳得越重越快。完美窗口内直接吸附到 `restScale`(0.85) 并锁住不动。

**6.9 脉冲形状刻意避开 u = 0** —— `Pulse()` 的鼓包中心在 0.12 / 0.30。因为窗口刚超时那一帧会走"跳动"分支且 p=1、phase=0，如果 u=0 处是满值就会每拍闪一下。

---

## 7. 编辑器里必须配好的东西（不在代码里）

**改代码不会影响这些，它们存在场景/预制体文件里。**

1. **Tag `Wall`** —— `ProjectSettings/TagManager.asset` 里已建好 ✓。`Wall` 物体要挂 Collider2D 并设 tag 为 `Wall`。
2. **四个预制体的 Collider2D 全部勾 `IsTrigger`** ✓ 已配好。
3. **`Bullet.prefab` 的 BoxCollider2D `Size` 偏小** ⚠️ —— 它的 Transform 缩放是 `(0.641, **−0.049**, 1)`（那是为了把方块压成箭形），碰撞体尺寸会**乘上**这个缩放。`Size` 现在是 `(1,1)`，所以世界里只有 **0.641 × 0.049** —— 宽度正常，但高度只有约 **0.05**，是想法的 1/20。
   - 正面瞄准时**仍然能命中**（子弹是朝敌人中心飞的，两条 y 带会重叠），所以这不是"完全打不到"。
   - 影响在于**容差**：对手榴弹的拦截、对移动目标的命中都紧了一个数量级。想要约 1 个单位的实际高度，把 `Size.y` 设成 **20**。
   - 同理 `Bomb` 缩放 0.2，`Size` 要放大 5 倍。
4. **生成点的 `target`** —— 三个 `Spawn` 的 `target` 目前都指向 **Capsule（玩家）**，意思是 Leader 把手榴弹抛向玩家所在位置。`Spawn` 在生成时注入给实例（预制体存不了场景引用）。
5. **`HeartSet` 上的 `Heartbeat.Source`** —— 保持 `Auto`（默认）。它会用 `FindObjectsByType` 判断场景里是 `Tap` 还是 `TapSec`，两个场景自动选对。
6. **准星物体** —— `TargetMarker.marker` 要拖一个带 `SpriteRenderer` 的物体。脚本挂在 `BulletS` 上。
7. **`WallCharge` 必须和 `Tap` 同一个物体** —— 它用 `GetComponent` 找，挂别处找不到，失败是**静默**的（攒血完全不生效）。

**⚠️ Unity 序列化的陷阱：改了脚本里的默认值，场景里已挂的组件不会跟着变。**

三个生成点现在存的是**旧数值**（那是它们被创建时的默认值，之后没 Reset 过）：

```
mode: 0 (Time)   interval: 2      earlyGroupSize: 2   lateGroupSize: 3
spawnGap: 1      baseSpawnRate: 25   rateGrowthPerMinute: 0
maxSpawnRate: 100   leaderChance: 20   leaderUnlockTime: 30
```

而 `Spawn.cs` 里现在的默认值是 `interval 2.5 / baseSpawnRate 30 / maxSpawnRate 60 / leaderChance 15 / leaderUnlockTime 45 / rateGrowth 2`。
**也就是"难度调优"那一步实际上没有生效。** 要生效就对三个生成点各点一次组件的齿轮 → `Reset`，或者手动填。

---

## 8. 八个已经踩过的坑（别再踩）

**8.1 下标访问 `Resources.LoadAll`** —— `arrow/` 里一旦多于一个预制体，返回顺序就不可靠。曾经 `prefab[0]` 拿到没有 `ArrowDefinition` 的 Bomb → `GetComponent<>()` 返回 null → 空引用。**全部改成按名字 `Resources.Load`。**

**8.2 `Test1.cs` 每帧把速度写成 `(1,1)`** —— 那句 `rb.velocity = new Vector3(1,1,0)` 让所有子弹恒定往右上角飞，调 `bulletSpeed` 完全无效，因为下一帧就被覆盖。**已从 `Bullet.prefab` 上移除，但脚本文件还在。**

**8.3 读错了场景的节拍源** —— `Heartbeat` 原来固定读 `Tap.*`，但场景 2 里只有 `TapSec`，于是 `beattime` 永远停在 1 → `p = 0` → 幅度恒为最小值 0.05（只有正常峰值的 1/7）。**症状极隐蔽：配置看起来完全正常，只是幅度偏小。** 现在用 `Auto` 自动识别 + 5 秒自检警告。

**8.4 子弹生成在自己身上被自己吃掉** —— 敌人开火时 `Instantiate(..., transform.position, ...)`，正好在敌人自己的 Trigger 里。两边都是 Trigger → 同帧触发 → `BulletHit` 打到开火者自己；而敌人的子弹 `Damage` 恒为 0，打不掉自己，**但子弹自己 `Destroy` 了** → 表现为"开火没有子弹"。用 `fromPlayer` 挡住解决。

**8.5 `Simple` 模式下的 `fillAmount` 是空转** —— 场景 2 的 `RedHeart` 原来一直是 `Image Type = Simple`，血条脚本每帧正确写 `fillAmount` 但 Unity 完全忽略。**必须 `Filled` + `Vertical`。**（场景 1 的 `RedHeart` 现在**仍然是 `Simple`**，所以那里灰心天然不生效 —— 那是**有意**的。）

**8.6 改了默认值但场景没 Reset** —— 见 §7 末尾。这个坑已经出现过至少两次（`Heartbeat` 的幅度参数、`Spawn` 的难度数值）。

**8.7 分母取错导致血条假满** —— `HeartFill` 的分母必须是**进场时的 `WallHP`**（`maxHP = GameManager.WallHP`），不是 `WallMaxHP`。攒到 300 进场景 2，分母就该是 300、显示 100%。反过来如果分母固定 1000，玩家一进场就看到 30% 的血，等于开局就"残血"。

**8.8 血条的方向** —— `fillAmount = 剩余比例`，配 `FillOrigin = Bottom`，所以彩色留在下方、灰色从上方压下来。改 `FillOrigin` 会让灰从上/从下反向。

---

## 9. 遗留代码

| 文件 | 状态 | 说明 |
| --- | --- | --- |
| `Assets/Test1.cs` | **有害** | 每帧写 `(1,1)`。已从预制体上摘掉，建议直接删文件 |
| `AiLeaderMove.cs` | 死代码 | 早期的单预制体敌人 AI，带**低抛抛物线求解器**。已被 `EnemyMove` 取代 |
| `LeaderEnemMove.cs` | 死代码 | 同上，更早的一版 |
| `SBomb.cs` | 空脚本 | 只有空的 `Start` / `Update` |

删除它们不会影响任何场景（已用 GUID 搜索确认无引用）。

---

## 10. 尚未实现（对照策划案 `策划案.docx`）

策划案已经对上的部分：完美/非完美 = 一击死/两击死 ✓、W 空中 S 地面 ✓、开火锁定最近目标 ✓、心跳作为时钟 ✓、建造阶段是 QTE ✓、完美加快心跳 ✓。

**还缺的：**

| 策划案 | 现状 | 备注 |
| --- | --- | --- |
| **音乐本体** | **完全没有 AudioSource** | `beattime` 是自走的定时器。`Spawn.CurrentBeat()` 是 `protected virtual`，留好了接音频的口子 |
| 三个小阶段（普通 → +精英 → +火炮） | 只有刷怪率随时间增长 + `IsLateGame()` 空接口 | `Spawn.IsLateGame()` 是接入点 |
| **火炮**（每 30s，最大生命 25%） | 完全没有 | 策划案说"几乎不可拦截" —— 建议改成**可拦截但极难** |
| 精英兵种 | 没有 | 建议做成"士兵的修饰符"而不是新 AI |
| 掷弹兵"三轮后转为普通士兵" | 没有 | 策划案里最好的敌人设计，值得做 |
| 街垒三阶段外观（矮→高） | 没有 | 需要美术 |
| 玩家 WASD 移动 | 已改为原地不动 | 见 §6.1，是有意选择 |
| 马吕斯等革命者 NPC | 没有 | 街垒血量目前是纯抽象的 int。把"革命者生命"做成看得见的剪影是性价比最高的一步 |
| 像素风 2.5D 美术 | 全是占位图形 | — |
| 街垒血量归零的结算 | **`WallHP` 到 0 之后什么都不发生** | 需要胜负流程 |

---

## 11. 约定与风格

1. **跨脚本通信一律 `public static`**，不拖引用、不写单例。这样任何一个脚本都能在任何场景里读到全局状态。
2. **注释用中文**，写"为什么"而不是"是什么"。工程里注释密度不高，请保持一致 —— 不要给每行加注释。
3. **需要留口子的地方用 `protected virtual` 方法**，而不是把参数堆成字段。已有的例子：`Spawn.SpawnRate()` / `LeaderAllowed()` / `IsLateGame()` / `CurrentBeat()`。需要非时间的判定就覆写它。
4. **预制体存不了场景引用** —— 需要场景里的东西（比如落点）时，由生成点在 `Instantiate` 之后注入。
5. **查对象用 `FindObjectsByType<T>(FindObjectsSortMode.None)`**（Unity 2022.3；`FindObjectsOfType` 在 2023 起弃用）。
6. **对玩家的提示性失败要打日志**。这个工程里静默失败害过很多次（攒血没生效、节拍源选错、准星没接线），加一句 `Debug.LogWarning` 值得。

---

## 12. 常见任务的改动位置

| 想做的事 | 改哪里 |
| --- | --- |
| 调难度 | 三个生成点的 Inspector（**记得 Reset**，见 §7），或 `Spawn.cs` 的默认值 |
| 改完美判定的宽严 | `TapSec` 的 `Setperfecttime`（默认 0.5） |
| 改心跳快慢的上限 | `Setbeattime` 下限（现在 `0.5`）与 `oribeattime`（现在 1） |
| 改敌人行为概率 | `Beat.SoldierFires` / `LeaderBombs` / `LeaderFires` 里的百分数 |
| 改敌人动作周期 | `Beat.Cycle` / `ActTime` / `HoldTime` |
| 接真音乐 | 覆写 `Spawn.CurrentBeat()`；再把 `Tap`/`TapSec` 的 `beattime` 改成从音频位置推导 |
| 加敌人类型 | 复制一个预制体，挂 `EnemyMove` + `EnemyHP` + `WallDamage` + Trigger 碰撞体，放进 `Resources/enemy/`，改 `Spawn.PickGroupPrefab()` |
| 加街垒的"输了"流程 | `GameManager.WallHP` 降到 0 的地方（`DamageWall` 里判断） |
| 调瞄准手感 | `SBullet.bulletSpeed`（影响预判提前量）与 `sameTrackRange`（轨道容差） |

---

## 13. 已知脆弱点

1. **三处 `public static` 的隐式耦合**（`TapSec.ShootStatu`、`TapSec.cnt`、`CapsuleTrack.AttackY`）—— 一个场景里如果有两个 `TapSec` 或两个 `CapsuleTrack`，它们会互相覆盖。目前都是单例，安全。
2. **`Spawn.nextGroupId` 是 static，跨场景不重置** —— 组号会一直累加。功能上无害（只需要互不相同），但如果场景被反复重载，相位偏移会一直变。想让每局一致，在 `Spawn` 的 `Awake` 里加个重置。
3. **`Tap` 和 `TapSec` 是两份高度相似的代码** —— 改节拍逻辑要改两处。长期看应该合并成一个带 `Source` 开关的类（`Heartbeat` 已经这么做了）。
4. **两个场景之间只共享一个 int** —— 节奏速度不跨场景。建造阶段攒到的"节奏推得多快"不会带进下半场。建议开一个 `RunState` 静态类收纳：街垒血量 + 心跳速度 + 阶段索引。
5. **`WallHP` 到 0 无处理** —— 游戏不会结束，玩家会一直看到全灰的心脏。
6. **没有任何音乐** —— 整个"律动"的主题目前建立在一个自走定时器上。

## 14. 2026-10-14 敌人与刷出逻辑更新

本节覆盖上文中旧的刷怪参数和 Leader 随机投弹描述。

- Soldier 每次只生成一只。三个 Spawn 每 1.5 ± 0.3 秒尝试生成，初始成功率 30%，每分钟增加 2 个百分点，上限 38%。全场敌人上限 12，只是控制数量，难度还需试玩。
- 三个 Spawn 共用 0.65 秒最短刷出间隔，Leader 改为同帧刷出同轨道三人组，横向站位间距 0.9。Leader 解锁从战斗场景进入后 45 秒开始计算。
- EnemyMove 内部的 LeaderSquad 管整组行为：至少推进 3 单位，若慢弹够不到街垒则继续前进，直到全部存活成员能命中街垒表面；停下预备 1 秒，连续三轮投弹，轮间隔 3.5 秒，最后恢复 1 秒后继续行进，只再开枪不再投弹。
- LeaderBusy 是全局锁。前一组正在推进、准备、投弹或恢复时，任意生成点都不能再生成 Leader。恢复行进或全部被击杀后，再冷却 18 秒。前组已恢复行进的存活敌人可以与下一组共存。
- Leader.prefab 的实际 bombSpeed 从 5 降为 4。Bomb.prefab 的 GravityScale 保持 0.25；弹道使用高抛解并考虑落点高度差，减少水平速度并留出拦截时间，不需要手动改重力。Spawn.target 已改为 Wall。
- BombHP 现在统一处理伤害与爆炸。击杀一枚会引爆当前仍存活的同 groupId 手榴弹，包含已发射但仍在空中的其他轮次。后续尚未投出的手榴弹不受影响。
- 手榴弹撞墙也触发同组连锁，只有触发撞墙的那枚扣街垒 50 血，其余连锁弹只消失，不额外扣墙血。爆炸没有新增范围伤害。
- 爆炸视觉复用手榴弹自己的 SpriteRenderer，橙色扩散并在 0.3 秒内淡出，没有新增 GameObject、组件脚本或美术资源。爆炸开始立刻禁用物理和碰撞，避免重复结算。
- 手榴弹的瞄准和命中改为读取出生轨道，而非飞行中的实时 y。SBullet 对手榴弹的预判补上重力，爆炸中的弹不会再被准星选中。
- 场景 2 里场外测试 arrow / Soldier / Leader 预制体实例已禁用，否则单只测试 Leader 会占用组锁。正常 Resources 预制体和生成点保持启用。

调节位置：刷出频率、概率、数量上限、组间冷却在三个 Spawn 的 Inspector；推进距离、投弹间隔与速度在 Leader.prefab；爆炸时长与缩放在 BombHP。修改脚本默认值不会自动覆盖已保存的 Inspector 数值。

## 15. 2026-10-14 提前投弹与场景局部计时

本节覆盖第 14 节的推进距离、重力和高抛说明。

- Leader.prefab 的 leaderAdvanceDistance 从 3 改为 1.5。speed 保持 0.5，因此约推进 3 秒后准备投弹，仍保留 1 秒预备时间和三轮投弹。
- Bomb.prefab 的 GravityScale 从 0.25 降为 0.08，bombSpeed 保持 4。平地最大射程约 20.4，足够三人组在从 S1/S2/S3 短距离推进后投向街垒。弹道改为低抛解，避免远距离高抛飞出画面；射程不足的保护仍保留。
- Leader 完成投弹后与 Soldier 共用 UpdateMarchAndFire 和 Beat.SoldierFires，开火概率均为每个 3 秒周期 5%。Leader 的角色标识和预制体移动速度保持不变，不再使用 20% 的 Leader 专属开火概率。
- Spawn 的刷出间隔门控、概率增长、Leader 解锁、组间冷却、音乐等待超时，全部改为 Time.timeSinceLevelLoad，不再按组件 Start 时间或全局 Time.time 计算。
- Spawn.Awake 根据场景 handle 重置共享刷出与 Leader 冷却截止时间，避免重载场景后局部时间归零，但沿用上一场景静态截止时间造成长期不刷怪。同一场景中新加生成点不会重置计时。
- Beat 的组节拍与 EnemyMove 的出生、预备、三轮投弹和恢复计时也统一使用场景局部时间。
- 没有增加 GameObject、脚本或手动挂载要求；参数已写入 Leader 与 Bomb 预制体。

## 16. 2026-10-14 切换禁射与换弹

本节覆盖上文 A/D、W/S 切换消耗节拍的描述，仅修改场景 2。

- A/D 换攻击轨道和 W/S 切换空中／地面瞄准立即生效，不重置节拍、不改变心跳速度、不增加 cnt。真正切换后禁止开枪 0.15 秒；连续切换从最后一次重新计时，不累加。边缘轨道的无效切换、重复选择当前瞄准模式不触发禁射。
- CapsuleTrack 使用 DefaultExecutionOrder(-100)，保证与 Space 同帧按 A/D 时先切换并进入禁射；W/S 在 TapSec 中先处理，同帧 Space 同样不能绕过。
- 每次真正发射子弹后进入 1 秒换弹等待。Perfect 弹实际命中有效的敌人或手榴弹后，立即结束该发子弹的换弹等待；只在完美窗口开枪但未命中不会提前结束。普通命中、错误类型／轨道、已死亡目标和敌军子弹均不能免换弹。
- 子弹保存自己的 shooter、shotId 和 perfectShot。旧弹延迟命中不能取消后续新弹的换弹等待；Perfect 命中也不会取消仍未结束的切换禁射。
- 禁射／换弹期间 Space 输入不发射、不消耗节拍、不增加 cnt；心跳照常计时，没有射击输入缓存。换弹期间仍可以切换瞄准，两种等待各自计时，需都结束才能开枪。
- 准星禁射或换弹时透明度降为正常的 35%，恢复可射击后还原，不修改心跳显示。当前换弹是计时状态，没有新增换弹动画或音效。
- 初始瞄准改为 Down，场景重新加载会重置玩家节拍、切换与换弹状态。Perfect 加速仍保留，最低节拍间隔严格为 0.5 秒；原有空轨道开火惩罚保留，未真正出弹不触发换弹。
- 参数已写入场景 2：Capsule 上 CapsuleTrack.switchSettleTime = 0.15；BulletS 上 SBullet.reloadTime = 1。未增加 GameObject、脚本文件或挂载需求；普通士兵和掷弹兵的敌人行为保持第 15 节规则。

## 17. 2026-10-15 音乐衔接与自主心跳交接

- 新增 RhythmManager、StageMusicTransition，以及 Resources/Rhythm/RhythmRoot.prefab。运行前自动实例化 RhythmRoot 并 DontDestroyOnLoad，运行时补上一个专用 2D AudioSource；没有新增 AudioListener。同一局只保留一个管理器。
- 在 RhythmRoot 预制体的 RhythmManager 中分别配置 Opening To Construction（目标场景 1）和 Construction To Defense（目标场景 2）的 Clip、BPM、First Beat Offset、Beat Count、Perfect Window、Fade Out Seconds。默认 Clip 为空，不伪造音乐或拍点；为空或参数无效时保持自主模式。
- 音频采用 DSP 时间和 PlayScheduled；指定第一拍为 scheduledStart + offset，跟随 beatCount 个整拍间隔，结束于 firstBeat + beatCount * 60/BPM。最后一段音量渐弱，音源通过 SetScheduledEndTime 在指定 DSP 边界停止；不会用音量是否等于零或 isPlaying 判定交接。长度不足会自动缩短整拍数，不足一拍则退回自主模式。
- Tap、TapSec 的音乐分支由同一个 MusicBeatClock 提供判定，窗口为拍点前后各 perfectWindow 秒，运行时限制为至多 0.45 个拍间隔。Perfect 不加速音乐，普通输入／空轨道射击不改变音乐拍点；每个音乐拍点只能取得一次 Perfect。禁射、换弹和实际 Perfect 命中免换弹保持原规则。
- 场景 1 歇息仍限制玩家操作，但不暂停音乐和心脏。Heartbeat 音乐分支直接读取 DSP 相位，每个音乐拍点一次完整双鼓包，不再使用原来逐渐加快的视觉计时；自主分支保留原有表现。
- 到交接边界，管理器将边界作为自主模式第一拍，注入剩余 Perfect 窗口；较迟完成加载时对齐未来拍点，不补发已过期的音乐拍点。同一个组件仅接收一次交接，之后正常输入重新控制自主倒计时。TapSec 的 Awake 不会覆盖持续存在的音乐时钟。
- 心脏从音乐模式回自主模式时，外观用 0.15 秒平滑衔接，不让缩放突然跳变；判定仍在准确 DSP 边界交接，不因视觉平滑延后。
- 如果希望音乐横跨实际切场景的瞬间，可提前调用 RhythmManager.PrepareMusicForScene("1"／"2")；StageMusicTransition.BeginMusic 可作为过场末尾的 Signal。稍后调用 GoToNextScene／LoadSceneWithMusic 时保持同一段音乐，不重新计时。不提前准备也可以直接切场景并开始音乐。
- 切场景统一用 RhythmManager.LoadSceneWithMusic("1" 或 "2")。StageMusicTransition.GoToNextScene 可绑定按钮或 Timeline Signal。它先开始音乐再 LoadSceneAsync，sceneLoaded 不会重复播放已准备的同一段音乐；直接启动或通过其他入口进入 1／2 时也会尝试相应音乐。尚未制作开场场景，不自动创建剧情或擅自规定建造何时结束。
- Build Settings 原来只有场景 1，本次补入场景 2；未加入不存在的开场场景。可在运行时选 DontDestroyOnLoad 下的 RhythmRoot，通过组件右键菜单测试进入 1／2。目标场景不存在时仅提示，不会停止正在播放的音乐。
- 敌人 Beat、刷怪局部时间、Leader 三轮投弹与炸弹连锁保持原逻辑。当前未实现整个游戏的暂停／音频设备热切换校准；修改 Time.timeScale 不等同于暂停音乐。


## 18. 2026-10-15 两首衔接音乐已配置

- Assets/Music/OneDayMore_OpeningToConstruction.wav 使用约50.87秒革命者剪辑版，配置到 RhythmRoot.openingToConstruction；PeopleSing_ConstructionToDefense.wav 使用73.1秒连续过渡版，配置到 constructionToDefense。保留用户原有 Do You Hear The People Sing.mp3，不覆盖它。
- WAV为44.1 kHz双声道，Unity导入使用PCM、预加载、不强制单声道、不归一化。音源仍为专用2D AudioSource、volume 0.65，不添加AudioListener。
- 新增 MusicBridge.beatTimes（音频内逐拍秒数）。非空且至少两拍时优先使用时间表，以适应电影版速度变化与剪辑位置；音乐完整播放到clip.length才交接自主模式。为空时原固定BPM逻辑保持兼容。两个BPM值仅作回退参考，不代表整段恒速。
- 拍点由频谱起音和动态规划自动估测，尚未人工听辨校准。可在Inspector修改beatTimes；必须严格递增，不能超出音频长度。perfectWindow保持每侧0.1秒，并按相邻拍点间距限制。音频已有淡出，prefab.fadeOutSeconds设为0，避免重复淡出。
- 场景1新增根物体StageFlow_ToDefense，挂已有StageMusicTransition，nextSceneName=2；没有自动规定建造结束时间。运行中可从组件菜单先BeginMusic，再GoToNextScene，音乐不重播；正式结束按钮或Timeline Signal绑定相同方法。
- 当前仍只有场景1和场景2，开场动画场景尚未创建。直接运行场景1会自动测试One Day More；未来开场结束调用PrepareMusicForScene("1")与LoadSceneWithMusic("1")即可沿用已配置音乐。
- 没有改敌人刷出、场景局部计时、切换禁射、换弹或Perfect实际命中收益。


## 19. 2026-10-16 场景1固定100秒与过场预留

- 场景1现有StageFlow_ToDefense增加ConstructionStageFlow和PlayableDirector，没有增加GameObject。Gameplay Seconds已序列化为100，Placeholder Cutscene Seconds为10。QTE、StageMusicTransition与Director引用已接好。
- 从场景1流程Start、玩家可操作时开始，用Time.timeSinceLevelLoad计时；按错歇息、音乐结束、Perfect数量与街垒是否满血都不改变100秒截止。Time.timeScale为0时场景计时不推进。
- 第100秒停止接受QTE输入并隐藏按键提示，保留建造出的WallHP；不会清空分数或重置血量。Tap输入端也检查截止时间，不依赖Update先后顺序，避免截止帧多算Perfect。
- 随后通过StageMusicTransition.BeginMusic开始Do You Hear the People Sing。没有Timeline资源时，预留10秒停留，然后自动进入场景2；这是动画占位时间，并没有制作角色动画或字幕。歌曲跨切场继续，不从头播放。
- 以后在同一物体的PlayableDirector中拖入有效Timeline资源。组件在100秒结束后自动Play，设置为GameTime、WrapMode.None，按Director.stopped事件切场，不用10秒固定计时截断正式动画。动画Pause时等待，结束Stop后继续。也可在末尾Signal调用ConstructionStageFlow.FinishCutscene。
- Director为空、资源为空或长度无效时退回10秒占位。重复完成回调只请求一次场景加载；销毁／禁用时解除Director事件订阅。场景2仍以进入后的局部时间开始刷怪。
- 本次没有修改按错10秒惩罚、每次Perfect建造收益、HP上限或心跳加速参数。


## 20. 2026-10-16 场景1首次按键后提示与心跳修复

- 复现了自主模式倒计时跨零当帧的正确输入被按“过早”处罚的问题。Tap现在先推进计时再做判定；不会在跨零当帧误进10秒休息。自主心跳加速也改为Mathf.Max(0.5f, ...)，不再出现0.47秒下限。
- 随机提示排除上一次显示的字母；成功、漏拍换题和休息恢复后都能看到新提示。成功次数在同一帧更新。
- 10秒休息规则保留，但界面明确显示REST Ns倒计时，结束后自动恢复新的按键。场景1的Key文本启用Best Fit（20—100），避免REST提示因原来的100号字体被裁切。当前占位字体不支持中文，因此暂用英文REST。
- 仅W/A/S/D与Space视为建造输入；鼠标点击Game窗口和其他非操作键不再触发休息。Space仍算错误的QTE尝试。
- 自主模式休息期间，Heartbeat跟随Tap.IsResting进入restScale静止期，不再针对冻结的beattime循环快速脉冲。音乐模式优先读DSP，休息期间歌曲和音乐心跳继续，恢复操作也不重播音乐。
- 100秒建造截止、10秒过场占位／正式Timeline、街垒收益与场景2规则沿用第19节和此前实现。


## 21. 2026-10-16 错按休息规则调整

本节覆盖第20节关于非Perfect休息、10秒休息和自主模式休息时心脏静止的规则。

- 仅按错当前提示的字母触发休息，时机无论是否Perfect均按错处理；Space仍算错误的QTE尝试，鼠标和非操作键继续忽略。Tap默认restTime与场景1实际序列化参数均改为3秒。
- 按对字母但不在Perfect窗口：立即换成下一个不同字母，不进入休息、不增加Perfect计数或建造血量；自主模式回基础节拍，音乐模式不改变拍点或播放进度。只有Perfect保留原收益。
- 3秒休息期间显示REST Ns，操作不能增加收益，倒计时结束自动恢复新的提示。自主模式心脏按稳定的基础拍间隔持续双脉冲，不冻结、不按冻结倒计时高速循环；有音乐时继续跟随DSP拍点。
- 100秒建造计时、10秒过场占位／Timeline、音乐切场与场景2规则保持既有实现。


## 22. 2026-10-16 直接启动场景1的音乐初始化兜底

- 原设计保持不变：直接运行场景1播放One Day More；正式开场提前准备的歌曲跨切场继续；建造100秒后播放Do You Hear the People Sing。
- 排查已确认音频、RhythmRoot音乐引用、场景1摄像机预制体的AudioListener均存在，工程未禁用音频。尚未通过编辑器带声音运行确认实际故障根因。
- 旧逻辑只在sceneLoaded或显式衔接入口调用StartBridge；漏掉首次加载事件时，即使管理器存在也不会开始音乐。RhythmManager.Start现在检查本局是否尚未处理任何音乐启动请求，必要时按当前场景补齐初始化。
- musicVersion和loading防止已准备的跨场景音乐、正常加载事件、已经结束的歌曲和异步切场被重复启动；不根据AudioSource.isPlaying重试，避免在0.25秒预排期误重播。
- 不新增GameObject、音频监听器或音轨，不改变3秒错按休息与100秒建造流程。


## 23. 2026-10-16 音乐强拍Perfect

- 音乐期间Perfect使用已标记的强拍；MusicBridge新增perfectBeatIndices（beatTimes内从0开始的序号）。空数组兼容原有每拍Perfect。非空时，弱拍IsPerfect=false、PerfectTime=0；计时读取DSP，窗口中心就是该强拍的音频时间。
- 现有两份剪辑音频用低频／中低频起音加权校准拍点，保留72／92拍的节奏网格，自动筛选30／42处较强伴奏落点。波形分析属于候选标记，尚未人工听辨校准，不能把它当作已确认的乐理小节强拍。
- Heartbeat继续使用同一份拍点表，强拍幅度100%、弱拍45%；不会在弱拍冻结心脏。Perfect窗口保持前后各0.1秒，并按相邻拍间距限制防重叠。每个强拍仍只给一次Perfect收益。
- 强拍序号复制到运行时独立掩码，运行中修改Inspector数组不影响当前歌曲；无效／重复／越界序号只关闭该段Perfect并提示，不擅自开放所有拍。
- 音乐完整播放、跨场景不重播、首次启动兜底、自主模式、3秒错按休息、100秒建造及10秒过场保持现有规则。不改音频本体，不新增GameObject。


## 24. 2026-10-16 强拍对应心脏静止间歇

- 本节覆盖第23节“强拍心脏重跳”的表现：有音乐时，Heartbeat直接使用MusicBeatFrame.IsPerfect，在强拍的整个Perfect窗口内缩小至baseScale * restScale并保持静止，与无音乐时相同。
- 窗口外按DSP节奏继续跳动；弱拍没有Perfect资格，因此不会触发静止。窗口结束自动恢复跳动，不暂停或重排音乐。
- 保留原本强拍前后各0.1秒窗口、30／42处候选强拍、单拍一次Perfect收益及自主模式。3秒错按休息期间仍跟随音乐的跳动／自然间歇，不将整段休息变成静止。
- 不改变拍点表、歌曲、音乐衔接、100秒建造或10秒过场，不需要新增GameObject或组件挂载。


## 25. 2026-10-16 Perfecttime调试提示

- Heartbeat增加仅编辑器／Development Build可见的左上角调试面板，默认显示，F3切换；Inspector可关闭Show Perfect Timer或调整字体。不新增GameObject、脚本文件或场景挂载。
- LateUpdate在玩家脚本之后采样，OnGUI只画快照。显示MUSIC/FREE、当前来源、Perfect窗口状态、窗口剩余秒数、距下一窗口秒数、raw perfecttime/beattime、音乐拍点序号（从0开始）与QTE休息状态。绿色表示判定窗口激活，不代表已通过禁射、换弹或单拍收益限制。
- 自主模式在等待时raw perfecttime仍保留预设值，但Perfect left显示--，避免把预设0.5秒误当作正在倒计时。错按休息时下个自主窗口显示PAUSED；音乐期间的窗口计时照常显示。
- MusicBeatFrame新增HasNextPerfect，只用于调试显示：最后一个允许Perfect的强拍过去后显示无后续强拍，不把曲尾到音乐结束的时间误当作下一窗口倒计时。
- 保留当前间歇Perfect视觉、强拍表、音乐、自主判定、3秒休息与100秒建造流程。正常非Development发布包不包含该调试UI。


## 26. 2026-10-16 音乐Perfect频率与容错小幅调整

- 两段实际音乐配置的Perfect Beat Indices从30／42处增至36／50处，约增加20%。原有允许Perfect的拍点全部保留；从已有节奏网格中补入较明显的伴奏落点，并优先填补较长空档，不改变歌曲速度或拍点秒数。
- 两段音乐的Perfect Window从每侧0.10秒增至0.12秒，总长从0.20秒增至0.24秒；最近的拍间距仍满足0.45倍限幅条件，不会被隐式缩短或发生重叠。
- Heartbeat继续在实际Perfect窗口内缩小静止，因此音乐间歇也随判定延长。F3调试提示读取当前实际窗口，不需要额外修改。
- 自主模式0.5秒窗口、错按休息3秒、单音乐强拍一次Perfect收益、100秒建造和10秒过场均保持原规则；未改脚本、场景或原音频。
- 工作区音乐重拍试听WAV和CSV已同步为36／50处，仍为自动标记候选，尚未逐处人工听辨。此前“主跳Perfect”试玩版备份继续保留，不用它覆盖当前调试面板。


## 27. 2026-10-16 动态BPM每拍Perfect试玩

- 本次以“随音乐速度变化，每拍开放Perfect”作为尝试。原音乐强拍模式36／50处与每侧0.12秒窗口的4份文件已备份到 `%USERPROFILE%\Documents\Codex\2026-10-14\e-unity-project-my-project-1\outputs\动态BPM试玩版\调整前备份_20261016_123549`，位于Unity工程外。
- MusicBridge新增Perfect Timing枚举：MarkedAccents沿用perfectBeatIndices，DynamicBpm忽略强拍资格掩码，按已有逐拍时间表每拍开放一次Perfect。两首实际配置已切到DynamicBpm，分别72／92拍；原36／50处强拍序号仍保留，便于切回。
- 动态BPM由当前相邻拍点间隔换算60／interval，DSP按完整拍点表定位，避免简单恒速累加漂移；不是运行时听音识别BPM，也不保证自动拍点已人工校准。无逐拍表时回退原固定BPM。
- 两段音乐窗口每侧0.12秒增至0.15秒，总长0.30秒，仍按相邻间隔0.45倍限制避免重叠。该尝试窗口以秒配置，不随BPM再次缩放。
- F3调试面板新增当前BPM、实际窗口半宽与DYNAMIC BPM／MUSIC ACCENTS模式标签。心脏在实际Perfect窗口内缩小静止；每拍仅一次收益、3秒休息、100秒建造与音乐交接保持原逻辑。无音乐自主模式的0.5秒窗口保持不变。


## 28. 2026-10-16 音乐0.4秒窗口与视觉频率交接

- 两段实际音乐Perfect Window从每侧0.15秒增至0.20秒，总长0.4秒，动态BPM每拍Perfect模式保持。无音乐自主窗口仍为0.5秒。
- 窗口保护上限由相邻间隔45%调整为49.999%，让最短0.4秒拍间隔也能得到约0.4秒窗口；中点仍留极小间隙，避免相邻窗口重叠，快速录音仍自动限幅。
- 判定计时本就通过ApplyHandoff继承最后一拍间隔，本次修正的是视觉：Heartbeat记录音乐末段Interval，音乐结束后第一次自主跳动从该周期开始，再在约一个末拍周期的实际跳动时间内平滑回原slowPeriod／fastPeriod曲线。
- Perfect静止期间不会消耗频率过渡时间，避免音乐结束先静止0.5秒后，真正恢复跳动时过渡已经结束。休息期间仍跳动，并使用相同的周期交接。原0.15秒缩放交接保留。
- 交接完成后，自主倒计时越接近窗口越快越重、Perfect加速、错按／漏拍恢复、3秒休息和场景流程均沿用原规则。没有把整段自主模式锁定为固定BPM，也没有改Tap或TapSec。
- F3的FREE模式Tempo显示visual BPM，表示当前动画跳动周期；Perfect静止时保留上一跳读数，不等同于判定窗口出现频率。


## 29. 2026-10-16 音乐窗口继续放宽到0.5秒

- 两首音乐Perfect Window每侧从0.20增至0.25秒，目标总长0.5秒；动态BPM每拍Perfect和音乐转自主的频率交接保持。
- 极快拍点继续按邻近间隔49.999%限宽防重叠。One Day More的部分紧邻拍实际窗口约0.40／0.48秒，其他正常段与People Sing为0.5秒；F3显示实际半宽。
- 仅修改音乐预制体和本交接记录，没有改脚本、拍点表、自主判定或音频。动态BPM CSV同步记录每拍实际窗口总长。


## 30. 2026-10-16 QTE误REST边界修复与自主节奏加快

- 用修改前脚本复现一条路径：原提示D，自主窗口超时那帧自动改成S，紧接着按刚看到的D会触发REST=3。此项是已确认的时序边界，不能断言涵盖用户描述的全部概率情况。
- 自主窗口漏拍只重置计时，不再自动换字。字母只在正确输入或休息恢复后改变，消除后台超时换题导致的误判。
- 换题后80毫秒内，旧字母重复按下只忽略，不给Perfect、不进入REST；正确的新字母立即响应，其他错字母及超过保护期的旧字母仍按错处理。音乐与自主模式均适用，初始提示不豁免错按。
- F3面板在场景1新增QTE last记录，包含场景时间、MUSIC/FREE、expected字母、pressed实际按下和判定结果。编辑器／开发构建中进入REST时也写一条Console日志，记录判定前的显示文字，便于继续追查其他触发条件。
- Tap与TapSec的自主基础等待从1.00秒改为0.85秒；Perfect窗口仍为0.5秒，每次Perfect缩短0.05秒及最低0.5秒等待保持。音乐每拍判定、目标0.5秒音乐窗口、视觉频率交接及3秒错按休息保持。


## 31. 2026-10-16 场景2五状态像素准星

- Assets/UI/Scene2Crosshairs导入角色适配版五张透明PNG，参考1.1.png的低饱和暖灰米白、深褐轮廓与粗像素。Single、Point、关闭Mip Maps、无压缩、64 PPU、中心Pivot。
- 复用场景2现有Target与BulletS上的TargetMarker，没有新增GameObject。已连接五张图与9张换弹进度帧；Target缩放从0.25调整为0.75，offset由头顶1单位调整为目标中心0，排序仍为20。
- TargetMarker继续调用SBullet.CurrentTarget，与实际开火共用目标。显示优先级：切换禁射 > 换弹 > 无目标 > Perfect > 可射击。没有目标时中心点位于当前攻击轨道、枪口左侧3单位，切轨时一起改变位置。
- Perfect提示：音乐期间直接读取MusicBeatFrame.Started/IsPerfect，自主期间读取TapSec.beattime<=0且perfecttime>0；仅提示窗口，不申领或消耗拍点。Perfect命中取消换弹后，下一次LateUpdate即可恢复对应准星。
- 换弹外圈按ReloadRemaining/reloadTime选择0至8段图片，不再显示固定进度；切换与换弹并存时先显示禁射，切换结束后显示实际剩余换弹进度。PNG保留原色与透明度，取消旧的红黄染色与35%透明度。
- Inspector调整：BulletS的TargetMarker控制五张Sprite、Reload Frames、无目标距离与Offset；Target的Transform Scale控制大小。本次没有改变瞄准、开火、Perfect判定、换弹时长或心跳逻辑。
- 修改前的TargetMarker.cs、2.unity和HANDOVER.md保存在Codex工作区work/crosshair_integration/backup，备份在Assets目录之外，避免重复编译。


## 32. 2026-10-16 取消Perfect准星提示

- 按用户难度设计，取消第31节的Perfect准星。TargetMarker不再读取音乐或自主Perfect时间，进入窗口时仍显示普通可射击样式，不收拢、不提亮。
- 仅保留切换禁射、换弹、无目标、可射击四种状态；场景2移除perfectSprite引用，Inspector不再显示该选项。原Perfect PNG作为未使用的素材保留。
- 现有Perfect判定、伤害奖励、命中免换弹、音乐、心跳和调试计时器均保持。修改前文件备份于Codex工作区work/remove_perfect_reticle/backup。


## 33. 2026-10-16 五张基础美术接入

- 来源%USERPROFILE%/Desktop/杂项/Art的主角、士兵、掷弹兵、手榴弹、子弹PNG，原图字节保持。导入Assets/Charactor/Basic_*.png；Sprite Single、Point、关闭Mip Maps、无压缩、透明背景、Full Rect，关闭自动物理形状。
- 场景2的Capsule换为主角图并取消原蓝色染色；Resources/enemy/Soldier换士兵图，Leader换掷弹兵图；Resources/arrow/Bullet和Bomb换对应图片。角色为静态图，未添加不存在的动画。
- 主角与掷弹兵PPU=32，士兵因源图放大4倍，PPU=128，三者的实际像素密度与脚底高度一致。人物pivot.y=0.5，脚底约为出生轨道y-0.594，掷弹兵帽羽高出正常身体；Sprite pivot.x对准各自可见主体中心。
- 四个战斗预制体根缩放统一为1，消除原Soldier横向压缩、Bullet负Y极度压扁与Bomb额外缩小。图片比例保持；兵种站位、速度、发射位置仍由原脚本控制。
- Soldier与Leader的Trigger Box分别为0.32×1.08、0.38×1.08，offset.y=-0.05，覆盖身体并排除帽羽；Bullet Trigger Box为0.18×0.16，Bomb Trigger Circle半径0.18，pivot对准弹体。弹丸碰撞范围已随图片重配，会影响边缘擦碰容差，需实机试玩确认。
- Bullet PPU=128，Bomb PPU=80；不透明弹丸主体对齐碰撞体。排序：敌人4、主角6、手榴弹9、子弹10，现有准星20保持。子弹图弹体在右、尾迹在左，匹配既有+X朝向和玩家瞄准旋转。
- 没有新增GameObject或脚本。没有改战斗、刷敌、音乐、心跳、Perfect或场景1的QTE；场景1没有现有角色物体，本次不凭空增加静态角色。第32节取消Perfect准星的规则继续保留。
- 修改前4个预制体、场景2、HANDOVER.md备份于Codex工作区work/basic_art/backup。配置说明与素材清单位于outputs/基础美术配置。

## 2026-10-17 士兵与掷弹兵动画试接入

- `EnemyMove.Awake` 自动添加 `EnemyVisualAnimation`，无需修改预制体或手动挂 Animator。Soldier 播放8帧行走/8帧开枪；Leader 使用最新“原图保真版”7部件，播放行走/开枪/投弹。
- `Assets/Resources/EnemyAnimations` 保存两张士兵切好帧的原图、两段原生 `.anim`、掷弹兵原图/手榴弹原图/动作 JSON。全部图像与原有切片 metadata 按原包逐字节复制；未重新生成掷弹兵样貌。不要擅自统一两张士兵图不同的 PPU。
- 开枪先请求动画，0.25秒后实际发射，周期在请求时占用；死亡或禁用会取消待发子弹。缺失动画时退回原静态开火。投弹动画在原发射时刻前0.4秒开始，原有三轮、间隔、恢复和刷怪锁继续由 EnemyMove 管理。
- `GrenadierOriginalRig` 运行时自动创建7个部件、手持手榴弹和两层枪口闪光；隐藏原根 SpriteRenderer，使用 SortingGroup 整体排序。禁用时恢复原渲染状态并清理。视觉偏移X=+0.0625，脚底和碰撞根物体不变。
- `Assets/Script/Editor/EnemyAnimationImportCheck.cs` 自动执行原生导入检查，也可通过“街垒/检查敌人动画导入”运行。报告位于系统临时目录 `StreetBarricadeEnemyAnimationImportCheck.json`；不切换场景，不自动进入运行模式。
- 接入前已通过 Unity 2022.3.62f3c1 全项目 API 编译及137项确定性逻辑断言（旧敌人行为64、弹道/局部时间39、动画攻击时机34）；原生编辑器检查和肉眼运行效果需分别看报告/场景。士兵帧仍有少量横向摆动，后续可继续修美术；子弹仍沿用根位置生成。
- 备份与导入清单：`%USERPROFILE%\Documents\Codex\2026-10-14\e-unity-project-my-project-1\work\enemy_animation_import`。新手使用说明在同工作区 `outputs/敌人动画导入说明.md`。

## 2026-10-17 士兵与掷弹兵改用 Animator 状态机

- 士兵 Idle / Walk / Fire，掷弹兵（Leader）额外有 Throw。Moving(bool) 控制站立与行走，Fire / Throw(trigger) 请求攻击；攻击播完后根据 Moving 返回。像素动画零秒切换，Idle/Walk循环，Fire/Throw不循环，Write Defaults关闭，Root Motion关闭。
- EnemyVisualAnimation 仅初始化 Animator、发送参数与严重卡帧时追赶已过去的动作时间；不再逐帧换图或调用 rig.PlayAt。EnemyMove 保持不变，实际子弹延迟0.25秒、手榴弹提前0.4秒开始动作、三轮投弹与队伍刷新锁仍由战斗逻辑控制；动画无发射事件。
- GrenadierOriginalRig 只创建原图部件与初始姿势。保留原图像素网格、修复后的 OverrideGeometry 像素坐标、整体排序与禁用清理；不再自行播放 JSON 动作。动作 JSON 由编辑器转为 Transform 与 SpriteRenderer 曲线，运行时由 Animator 采样。
- 原 PNG 与切片元数据不改动。士兵行走的统一水平锚点保存为新库的 Sprite 子资源，原 PPU、垂直 Pivot、8fps步频与碰撞体均保持；开枪仍为12fps、投弹10fps。
- Editor/EnemyAnimatorSetup.cs 在编辑器完成编译后自动生成 .anim、.controller 和 EnemyAnimationLibrary.asset，位于 Assets/Resources/EnemyAnimations/Animator。通过 PrefabUtility 给 Soldier/Leader 添加 Animator、EnemyVisualAnimation 和对应 controller；不手改 YAML，不改场景。
- 编辑器菜单“街垒/动画/生成或检查敌人Animator”补齐或检查资源，普通启动不覆盖已生成的状态机；可自行编辑生成资源。明确选择“重建生成的敌人Animator（覆盖生成内容）”时才重新烘焙。首次生成及重建先把旧预制体和生成资源备份到工程根目录 AnimatorBackups（不在Assets内）。脚本及本交接文件另有安装器备份。
- 掷弹兵仍在运行时创建原有10个外观子物体（7部件、1颗手持手榴弹、2层闪光），本次不添加额外场景GameObject。运行时选中敌人，在Animator窗口查看当前状态；停止运行后动态部件会正常清理。
- 编辑器检查报告：Library/EnemyAnimatorSetupResult.json。运行时代码与编辑器代码已分别通过 Unity 2022.3 程序集编译；战斗回归137项通过。自动启动的独立原生Unity测试未能产生启动日志，不能视为已通过原生播放测试；最终导入/曲线校验由编辑器实际生成后报告，画面需在场景2试玩。


## 2026-10-17：主角17组Animator与Timeline接入

主角素材为“精选重制版”：保留Idle、AimGround、FireGround、AimUp、FireUp、Reload、RaiseGun七组；其余十组重制；无交谈动作。136张PNG存于Assets/Resources/HeroAnimations/Frames，每帧64×64，PPU32，Point，无压缩，无MipMap。

- HeroAnimatorSetup使用Unity2022原生Editor API生成17个.anim、Hero.controller、HeroAnimationLibrary、Hero.prefab、HeroEndingSequence.prefab、ConstructionToDefense.playable及HeroEnding.playable，并绑定场景1/2。Editor首次编译后自动执行；没有播放拦截。菜单：街垒/动画/生成或检查主角Animator。
- HeroStateMachine选择动作与优先级；HeroActor把Motion整数及ReloadSpeed参数交给Animator，只有显式动作重启才调用Play，不逐帧手动换Sprite。17个状态通过AnyState的Motion条件转移，禁止同状态自动重入。
- 场景2：仍使用原Capsule、TapSec、CapsuleTrack、BulletS。Capsule原SpriteRenderer禁用，子物体HeroVisual负责显示，物理/轨道组件不搬移。HeroCombatDriver只监听SBullet.ShotFired，没发弹就不播开火。瞄准取TapSec.ShootStatu；换弹取SBullet.ReloadRemaining，Perfect取消换弹时立即回瞄准。新发可打断上一发后坐，不额外禁射。
- SBullet新增groundMuzzle/upMuzzle引用；发弹与预判统一使用对应的动画枪口坐标，未绑定时回退原位置。子弹伤害、目标类型隔离、攻击轨道、切换禁射和Perfect免换弹机制不变。
- 场景1：HeroConstructionStage内的HeroConstructionDriver监听Tap.InputAccepted（正确普通/Perfect均通知，积分逻辑保持原样），播放拾取→搬运→放置→走回的视觉循环，工作最多暂存一个待办；Tap.IsResting触发3秒休息画面，心跳仍由原系统控制。
- ConstructionStageFlow在100秒建造结束时广播CutsceneStarting，然后播放原来的过场导演。未自定义Timeline时自动绑定10秒“站定→举枪→瞄准”，音乐与载入仍由原StageMusicTransition负责。已经自定义的导演资产不被替换。
- 正式结尾尚未绑定通关或血量归零。用户指定先准备独立Timeline：把HeroEndingSequence.prefab放入未来酒馆结尾场景，调用HeroSequenceController.Play()，先纪念2秒再憧憬2秒，保持末帧。
- HeroActor的17个无参数动作方法可直接连Timeline Signal；ResumeGameplay释放剧情控制。PlayEndingSequence也可不经Timeline播放两段结尾。

生成前备份在工程AnimatorBackups/Hero-时间戳，原脚本安装前的备份保存在工作区“Unity主角动画配置/Backup-时间戳”。生成验证报告：Library/HeroAnimatorSetupResult.json。

### 主角动画原生核验完成（2026-10-17）

- HeroAnimatorSetup版本2：Sprite曲线只保留8个实际帧键，并在设置曲线后明确startTime/stopTime，修正片段末尾多一帧的问题。
- 当前工程Unity 2022.3原生生成报告Library/HeroAnimatorSetupResult.json：success=true，17动作、136帧、场景1/2绑定完成。
- 新增只读Editor检查器HeroAnimatorVerification：使用隔离预览对象验证136次Sprite播放采样、17个Animator参数转移、两个场景的组件/枪口/流程引用，以及结尾Timeline两段采样。报告Library/HeroAnimatorVerification.json：success=true；不进入Play、不保存场景。
- 本次原生生成前备份：AnimatorBackups/Hero-20261017-210059-799；最初安装前的旧脚本和场景备份仍保留于Codex输出包Backup-20261017-202336-420。结尾仍未接通关。

## 2026-10-18 主角大衣下摆与鞋底局部修补

- 用户确认场景2中主角的下摆和鞋底轮廓需要修补。现场诊断Library/HeroScene2VisualDiagnosis.json确认136帧没有不透明像素落在网格外，角色位于镜头内，未发现遮挡；处理针对PNG素材轮廓。
- 128帧共1801个边缘像素局部修补；8帧Idle字节保持，所有原图脸帽像素、35行以上像素、64×64尺寸、二值透明、51像素脚底基准、17动作与原帧率保持。原精选重制版留存，补边版与新预览在Codex输出“主角像素资源_64x64_边缘修补版”。
- PNG原位更新，未替换.meta/GUID，Animator、Timeline与场景引用继续沿用。Editor/HeroAnimatorVerification增加显式运行模式保护和1秒编辑模式稳定等待，避免退出游戏时调用EditorSceneManager.OpenScene。
- 修补脚本与检查记录在Codex工作区work/hero_edge_fix；本轮安装备份由输出包Apply.ps1保存，Native截图位于Library/HeroScene2GameCapture.png。显示诊断不修改场景或资源。

## 2026-10-18 主角步态与呼吸重制

- 用户要求重新处理主角行走、搬运行走和呼吸。本轮更新Idle/IdleLeft/Walk/CarryWalk共32帧，其余13组帧保持。内置image_gen生成三张研究/姿势图，再以统一周期尺度、原图107像素头帽、脚底和箱子抓握校准整理为64×64。原版和边缘修补版保留，新的完整包为Codex输出“主角像素资源_64x64_步态与呼吸重制版”。
- Idle/IdleLeft为8帧3fps，约2.667秒一轮；头帽、下半身与双脚固定，只微幅改变上胸。Walk/CarryWalk仍8帧8fps基准，按实际位移动态调整LocomotionSpeed。CarryWalk箱子始终采用原道具，不在帧间变形。
- HeroAnimationLibrary新增walkingCycleDistance=.875、carryingCycleDistance=.75；HeroLocomotionTiming负责步频计算和无效输入/极值防护。HeroActor只给两个行走状态设置LocomotionSpeed，保持射击/换弹时序。HeroConstructionDriver默认moveSpeed=1.05、carryingSpeedMultiplier=.8；生成时只迁移旧默认1.7，其余自定义速度保留。
- HeroAnimatorSetup版本3添加参数/行走状态速度绑定，更新呼吸片段时长并保留8帧曲线规则。运行时代码和Editor代码均通过Unity2022程序集编译；状态机/步频74项检查通过。编辑器实际生成和采样以Library/HeroAnimatorSetupResult.json、HeroAnimatorVerification.json版本3报告为准。
- 本轮脚本、生成提示词与像素校准记录在work/hero_motion_v4；安装器备份旧脚本、PNG、metadata和两个场景，原生生成器另保存在AnimatorBackups中。

## 2026-10-18 敌人动画时长修复与音效系统接入

### 敌人动画片段时长（EnemyAnimatorSetup 版本 2）

- 原问题：`SpriteClip` / `FloatCurve` 在片段末尾多写了一个首帧键，Unity 把时长算成 `(帧数+1)/fps`。磁盘上表现为 Soldier_Idle **2.0 s**、Soldier_Walk **1.125 s**、Soldier_Fire **0.75 s**，走路循环每轮会多停一帧（约 11% 时间冻在第一帧）。
- 改法：只写实际帧，新增 `FinishClip` 显式设置 `startTime / stopTime / loopTime`，`RigClip` 同样处理。`Version` 由 1 提到 2、`SessionState` 键改到 v3，所以“生成或检查敌人Animator”也会强制重建，不再只校验旧资源。
- 重建结果（2026-10-18 00:44）：Soldier_Idle **1.0**、Walk **1.0**、Fire **0.6667**；Grenadier 保持 1 / 1 / 0.6667 / 0.8。两个敌人预制体已指向新 controller。
- **未完成**：`Library/EnemyAnimatorSetupResult.json` 仍是 `success:false`，报错“动画时长与攻击时机不匹配：Grenadier_Idle”。该片段磁盘上的 `m_StopTime` 已是 1，但校验读到的 `AnimationClip.length` 不符，根因尚未定位。接手时先修这条，否则生成器每次重建都会抛异常、报告永远红。
- 重建前状态备份：`AnimatorBackups/20261018-004405-804`（含 4 个动画、2 个 controller、library 和两个预制体）。

### 音效系统（新增）

- 新增 `Assets/Script/SfxPlayer.cs`：静态类，运行时自建 12 路 AudioSource 池并 `DontDestroyOnLoad`，**不需要在场景里挂任何物体**。资源路径固定为 `Resources/SFX/{编号}`。
- 命名约定：文件名就是需求文档编号，例如 `SFX-PLY-01.wav`；变体为 `SFX-PLY-01-V1..V3`。缺变体时自动回退到基础文件（只放一个也能响），缺文件按编号只警告一次，不中断游戏。
- 已接入 26 条：

| 脚本 | 编号与触发 |
| --- | --- |
| `SBullet` | PLY-01 出弹（3 变体）、PLY-03 装填、PLY-04 换弹结束（Update 里检测 `IsReloading` 下降沿）、PLY-02 空轨道 |
| `BulletHit` | ENM-02 命中、QTE-01 叠加（Perfect 时）、BMB-03 手榴弹未爆、WAL-01 敌弹撞墙 |
| `BombHP` | BMB-04 爆炸、BMB-05 同组连锁 |
| `WallDamage` | WAL-02 敌人撞墙 |
| `EnemyMove` | ENM-01 敌枪（3 变体）、BMB-01 投弹 |
| `Spawn` | ENM-07 三人组刷新 |
| `Tap` | QTE-01 完美、QTE-02 普通、QTE-03 错按、QTE-04 休息 |
| `TapSec` | PLY-05 切瞄准、PLY-06 禁射/换弹时按开火 |
| `CapsuleTrack` | PLY-05 换轨 |
| `Heartbeat` | HRT-01 每拍（音乐按相位回绕、自主按 beattime 跳变）、HRT-04 强拍叠加 |
| `HeroConstructionDriver` | BLD-01 拾起、BLD-02 放下、BLD-07 休息丢弃、BLD-06 脚步（0.32 s 节拍） |
| `ConstructionStageFlow` | SYS-03 进入过场 |

- 未接入 30 条，原因是**代码里还没有对应事件**：WAL-03/04/05、END-01/02 需要 `GameManager` 的血量阈值（66% / 33% / 0）与结算流程；ART 全组需要火炮系统；AMB/VOX 需要环境层与人群；SYS-01/02/04/05 需要按钮逻辑；CUT-01 需要过场音频；HRT-02/03/05、BMB-02 尚未接。
- 规格缺口：战斗中的 Perfect 命中没有独立编号，当前借用 `SFX-QTE-01`。要区分的话建议新增 `SFX-PLY-07`。
- 验证方式：用工程自身 csproj 的引用集单独跑 Roslyn 编译 37 个运行时源文件，0 错误。**但 Unity 的 `Library/ScriptAssemblies/Assembly-CSharp.dll` 时间仍是 01:24，说明编辑器还没重新编译这批改动**；下次打开工程先确认 Console 无红字。

### 音效需求文档与外部素材

- 需求文档：`%USERPROFILE%\Desktop\杂项\temp\街垒音效资产制作要求_v1.2.docx`。56 条，编号体系为 `HRT / SYS / QTE / BLD / PLY / ENM / BMB / WAL / ART / VOX / AMB / END`，含技术规范、验收清单、代码接入状态、资源预算和实施顺序。
- 旧的 `街垒音效需求清单.xlsx`（SFX-01～28）编号已不通用，以新编号为准。
- `E:\SoundAsset` 下另有 21 个 CC0 素材（Kenney、OpenGameArt），**尚未导入工程**；文件名不是编号，需要改名成 `SFX-XXX-NN` 后放进 `Assets/Resources/SFX/` 才会被播放器找到。
- 工程内新增 `Assets/Resources/SFX/README.txt` 说明命名规则，目前没有任何音频文件。

### 计数更新

本文头部的“23 个脚本”已过时：`Assets/Script` 现有 36 个 `.cs`（含 `SfxPlayer`），`Assets/Script/Editor` 6 个，另有 `Assets/Test1.cs`。



## 2026-10-18 用户指定行走图、呼吸与抱物袖子修正版导入

- 本轮来源：用户剪贴板中的256×256行走图（4×4、每帧64×64），以及Codex输出“主角抱物搬运_袖子修正版”。Walk和CarryWalk均使用完整16帧，基准12fps，保留原有朝左坐标约定；Idle/IdleLeft各8帧3fps，呼吸约2.667秒。总数更新为17动作、152帧。
- HeroAnimatorSetup版本4支持可变帧数。已有版本3配置完整时，只刷新Idle/IdleLeft/Walk/CarryWalk四组片段和动画库，不重建原状态机、场景或Timeline，不改任何运行时脚本和音效接口。HeroAnimatorVerification按实际fps×duration核对帧数，不再固定每动作8帧。
- 音效检查基于当前SfxPlayer和HANDOVER末尾音效记录：26个编号已接入，但Resources/SFX暂无实际音频。脚步仍固定0.32秒；自主休息心脏分支没有HRT-01播放；换弹完成采用Update下降沿采样，极短Perfect免换弹路径可能漏播。此次仅记录检查结果，保留现有音效行为。完整调用位置、规范对应及缺资源清单见Codex输出“主角动画导入_16帧与音效检查”。
- 核验看Library/HeroAnimatorSetupResult.json与HeroAnimatorVerification.json的版本4报告。安装器先保存被替换文件、原meta及现有主角生成资源；只由Unity原生Editor API写入导入参数和动画资产。

### 本轮导入核验完成

- Unity版本4实际生成与播放核验均success=true：17动作、152帧、2场景绑定、2次结尾Timeline采样；Walk/CarryWalk各16帧12fps，Idle/IdleLeft各8帧3fps。
- 导入前后36个运行时脚本、两个场景、Hero.controller、Timeline和其他13组片段均保持哈希一致，SfxPlayer及全部29处音效调用保留。32个已有Sprite GUID保留，16个新增帧由Unity生成meta。
- 当前音效仍缺实际音频文件。另确认BulletHit死亡时仍用ENM-02且ENM-03未接；SYS-03代码在过场开始触发，与文档结束时的描述存在差异。本轮仅记录接口审查，不改音效行为。
- 本轮安装备份：%USERPROFILE%\Documents\Codex\2026-10-14\e-unity-project-my-project-1\outputs\主角动画导入_16帧与音效检查\Backup-20261018-115953-504；原生资源备份：E:\Unity Project\My project (1)\AnimatorBackups\Hero-20261018-120255-466。


## 2026-10-18 心跳声音、动画与Perfect统一相位

- 当前工程已存在HRT-01三个0.55秒双心跳变体和HRT-04。本轮不覆盖这些原音频，整理出三个约90ms、主峰约2ms的单跳PCM，保存于Resources/SFX/HeartbeatPulse/Pulse-V1..V3。
- Heartbeat不再在beattime重置或音乐相位回绕时播放整个双心跳。HeartbeatPulseTiming共用波形与峰值事件，自主/休息0.12和0.30、音乐窗口之间0.28和0.67，副跳55%音量。音乐只新增PulsePhase/PulseSeconds/PulseCycle/HasPulseInterval显示字段，原MusicBeatClock判定字段及交接算法保持。
- Perfect静止时停止心跳专用AudioSource并清除旧峰事件；自主休息继续按原稳定周期跳动并发声。原有音乐结束后的周期平滑交接与自主加速保留。窄间隙不改Perfect来强行塞入声音，卡帧不追补旧拍。
- Heartbeat在现有HeartSet上按需添加一个2D AudioSource，不新增GameObject或AudioListener，不占全局SfxPlayer池；默认enableHeartbeatSound=true、heartbeatVolume=.55，仍受SfxPlayer.MasterVolume控制。禁用组件即停声。F3在自主休息期间明确显示REST，而非把冻结raw perfecttime误标为Perfect可用。
- 新增Editor/HeartbeatAudioSetup：停止运行后原生配置3个短音频导入参数，只读核验场景1/2和Perfect静音，报告Library/HeartbeatAudioSetupResult.json。Native导入状态以该报告为准；声音输出设备延迟未自动测量。
- 工作区work/heartbeat_sync，输出“心跳声音画同步”。运行时/Editor编译通过，独立时序55402项对照断言通过，确认判定、计时、音乐交接不变。安装前备份由Apply.ps1保存。

### 心跳声音画同步原生核验完成

- Library/HeartbeatAudioSetupResult.json：success=true，3段90ms单跳音频与2个场景配置通过，1751个原生时序检查通过。最新运行时脚本已由Unity重新编译。
- 独立新旧计时对照55402项断言通过；原录音、SfxPlayer、Tap/TapSec、音乐配置、场景和主角动画等439个保护文件哈希保持。实际音频设备延迟与主观听感未自动测量，可进入场景1/2试听。
- 本轮安装前备份：%USERPROFILE%\Documents\Codex\2026-10-14\e-unity-project-my-project-1\outputs\心跳声音画同步\Backup-20261018-232912-207。


## 2026-10-18 心跳发声窗口纠正

- 用户明确：beattime倒计时期间跳动并发声，perfecttime为无心跳声音的静止间歇。perfecttime在自主beattime期间保留初值，不代表Perfect已激活。
- Heartbeat动画和声音统一在LateUpdate读取计时，F3使用同一份音乐采样，标明BEATTIME: HEARTBEAT / PERFECT: SILENT / STILL。只有实际beattime>0才允许播放；自主错按3秒休息仍继续搏动。
- 普通单声源播放替代PlayOneShot，按剩余beattime用DSP定时截断最后一跳尾音，进入Perfect即停止声源、清相位并立即静止，不再用音乐交接缩放插值跨入Perfect。未修改判定窗口、BPM、音乐、场景或角色资源。
- Editor/HeartbeatAudioSetup报告升为版本2，增加beattime/Perfect门控与尾音边界核验。原版脚本备份及离线测试在Codex工作区work/heartbeat_gate_fix。音频设备延迟和实际听感仍需运行试听确认。


## 2026-10-19 蓝牙心跳输出延迟与排程改造（v3）

- 用户实际症状：使用蓝牙耳机/音箱，在应该静止的Perfect期间仍听到心跳。v2报告只验证了逻辑门控，没有测量真实声学延迟。此前在动画峰值才PlayScheduled(dspTime)没有准备时间；当帧Stop也不能撤回已送往设备的声音。
- 新增HeartbeatAudioTimeline与HeartbeatAudioOutput。心跳按未来峰值预排，主/副跳使用独立排程声源，读取PCM实际主峰位置补偿起音，不在峰值出现后补播。提前截断预计会进入Perfect的音频尾音。声源均在已有HeartSet，不新增GameObject。原WAV及meta不变。
- RhythmManager新增输出延迟补偿：Unity缓冲队列粗估(bufferLength*(buffers-1)/sampleRate)加额外延迟，额外初值150ms仅针对本次用户蓝牙设备的试调起点，并非测量值。F3显示总估计，F6/F7每次减/加10ms，PlayerPrefs保存额外值跨场景/重启复用；换设备需重新校准。关闭compensateOutputLatency可回到0补偿。
- 音乐原始DSP拍表不变；画面/输入判定共用每帧一次的dspTime-输出延迟采样，衔接到自主计时也用同一时间。音频心跳与背景音乐按原始DSP时间排程，避免重复补偿。歌曲BPM、Perfect窗口宽度和每拍领取规则不变；实际屏幕窗口整体随设备估计延迟平移。
- 自主心跳按现有加速周期预测未来波形，休息仍继续。Perfect期间会提前提交下一段beattime的音频，不能根据AudioSource.isPlaying直接认定Perfect漏音，因为音频还在设备队列。正确比较预计可听时间与画面窗口。输入提前结束Perfect或重置倒计时时撤销尚未开始的旧预测，停止QTE或暂停时停止排程。已经进入蓝牙设备的声音无法追溯撤回。
- Editor/HeartbeatAudioSetup报告版本3，增加补偿后可听尾音边界校验。新增Editor/HeartbeatPlaybackAudit：每次Play后8秒或提前退出时写Library/HeartbeatPlaybackAudit.json，含实际缓冲/采样率、补偿设置、最近100个已提交声音的DSP与预计可听时间；不是麦克风或设备实测。
- 离线Unity2022运行时与Editor编译通过；5758门控检查、26562延迟/帧率/输入重置检查、55402原计时回归检查通过。原生导入与实际听感需刷新运行核验，不能以离线检查代替硬件试听。工作区work/heartbeat_latency，备份与使用说明在Codex输出“心跳蓝牙延迟修正”。

### v3原生运行核验（2026-10-19）

- Library/HeartbeatAudioSetupResult.json版本3，success=true；用户确认F3显示v3。
- Library/HeartbeatPlaybackAudit.json实际runtimeVersion=3，场景2/TapSec。设备运行配置为1024采样×4缓冲、48000Hz；总输出延迟估计214ms（队列粗估64ms+额外150ms）。19次实际已提交排程的预计可听尾音全部早于相应Perfect边界，未发现预计尾音越界。
- 硬件输出延迟仍未实测；214ms不是耳机测量结果。F6/F7按10ms校准额外值并保存。原生报告、实际排程及离线检查结果已保存到Codex输出“心跳蓝牙延迟修正”。

## 2026-10-19 用户试听确认与后续开场调音待办

- 用户在确认F3显示v3并实际运行后反馈：“声音与跳动、静止已基本同步”。当前214ms总补偿（缓冲估计64ms＋额外150ms）可作为此次蓝牙设备的保留基准；它是试听认可的设置，不是硬件实测值，也不应当作所有玩家设备的固定值。
- 保持规则：beattime期间心脏跳动并发声；Perfect是心跳静止、心跳声停歇的间歇；错按后的三秒休息仍继续跳动。F6/F7按10ms调节额外补偿并保存，换设备后需要重新校准。

### 待办：在游戏开头添加调音／声音延迟校准环节（尚未实现）

- [ ] 在正式开场动画与玩法开始前提供简短调音环节，让玩家通过可视节拍／心脏动画与心跳试听校准声音延迟，尤其考虑蓝牙耳机或音箱。
- [ ] 为玩家提供易懂的音量与延迟调节、试听和确认保存入口，复用现有延迟补偿与保存逻辑；不能只依赖开发用的F3、F6、F7。
- [ ] 建议允许使用已有设置跳过，并在设置菜单提供重新调音入口，以便更换输出设备后重调。
- 后续处理开场流程或设置菜单时，请提醒用户并安排此项；本轮只记录待办，没有创建调音界面或修改开场流程。


## 2026-10-19 Script脚本按功能分类

- 47个脚本已按下表整理。原文件名、类名及47份.meta/GUID保持；运行时39个、编辑器8个，Editor脚本全部保留在Editor路径内，不增加asmdef。
- 仅HeroAnimatorSetup.cs两处脚本路径由Assets/Script/HeroConstructionDriver.cs、HeroCombatDriver.cs更新到Animation/Hero下的新位置；玩法、节奏和音频逻辑不改。历史记录里的旧路径应以本节与Script/README.md为准。
- 三个旧脚本AiLeaderMove、LeaderEnemMove、SBomb保留于Legacy；本轮未找到其场景、预制体或资产引用，不作删除。Assets/Test1.cs不属于本轮Script目录范围，保持原位置。

| 新目录（相对于Assets/Script） | 用途 | 脚本数 |
| --- | --- | --- |
| `Core/` | 全局数值、阶段流程、场景音乐衔接和通用跟随 | 4 |
| `Rhythm/` | 玩家节奏、心脏动画、Perfect和音乐计时 | 3 |
| `Audio/` | 全局音效、心跳音频排程与延迟计算 | 3 |
| `Gameplay/Construction/` | 场景1的QTE与街垒建造 | 2 |
| `Gameplay/Combat/` | 场景2的操作、开枪、弹道、伤害与手榴弹 | 7 |
| `Enemies/` | 刷敌、敌人行为、血量与敌人组节奏 | 4 |
| `Animation/Hero/` | 主角状态机、Animator驱动、建造/战斗动作与剧情播放 | 7 |
| `Animation/Enemies/` | 敌人动画库、Animator驱动与掷弹兵原图骨架 | 3 |
| `UI/` | 心脏血条、按键反馈和准星 | 3 |
| `Legacy/` | 旧实现及占位脚本；目前未找到场景/预制体/资产引用，保留供回查 | 3 |
| `Editor/Animation/` | 主角与敌人动画资产生成和绑定工具 | 2 |
| `Editor/Audio/` | 心跳音频配置核验与实际排程记录 | 2 |
| `Editor/Diagnostics/` | 动画导入、播放及场景2显示诊断 | 4 |

- 完整旧路径→新路径清单、安装前脚本备份及GUID核验结果保存在Codex输出“脚本目录分类”。
- 后续开场调音／声音延迟校准的待办继续保留，尚未实现。

- 本轮整理核验：47份脚本meta逐字节保持、47个GUID无重复；46个脚本内容原样，只有HeroAnimatorSetup更新两处路径。531个其余资源文件哈希保持；整理后的运行时与Editor脚本分别通过Unity2022引用集的独立编译。

## 2026-10-19 第二幕三张街景素材接入

- 用户提供的三张PNG原样导入Assets/Environment/Act2：Cobblestone（石板路）、Buildings（透明建筑）、Barricade（透明街垒）。不改原始像素，Point、无MipMap、无压缩、Single、FullRect、中心Pivot、94.1 PPU。
- Assets/Scenes/2.unity新增Act2Environment及三个同名SpriteRenderer子物体，位置(0,0,0)、缩放1；排序分别-30/-20/2。原图按完整画布叠合，约16:9构图对应原摄像机高度10，未改摄像机投影或引入PixelPerfectCamera。
- 原Wall只关闭SpriteRenderer；原Wall的Transform、旋转、缩放、Collider2D和Wall标签保持，三个Spawn/轨道及所有玩法、动画、音乐、心跳逻辑不改。美术街垒独立于原旋转碰撞根物体；本轮保留现有碰撞位置，原图中的石块轮廓不等同于碰撞边界。街垒排序2，角色/敌人显示在它前面。
- 当前没有可连接的Unity2022 Pipeline；后台原生Unity2022启动均退出140063且没有日志，未完成原生导入与实机画面核验。配置以离线方式保存，结构比对确认既有场景仅改变Wall渲染开关和SceneRoots列表，新增4个美术对象；其余659个既有资源/设置文件哈希保持。不可把素材叠层预览当作实机截图。
- 新增Editor/Scene2EnvironmentSetup.cs，使用工程Unity2022引用集独立编译通过。菜单“街垒/场景/配置第二幕街景”可通过Unity原生API重新导入、保存并校验碰撞/轨道，报告和摄像机预览写Library/Scene2EnvironmentSetup；未自动改动其他场景。
- 修改前场景与HANDOVER、配置说明、离线核验与美术叠层预览：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/第二幕场景配置。
- 开场调音/声音延迟校准、敌人动画Grenadier_Idle时长核验、归零结算等既有待办保持。

## 2026-10-19 火炮与炮弹独立Prefab

- 用户请求新建火炮Prefab，参考桌面“杂项/作品/策划案/策划案.docx”，美术指定E:/ArtAsset/街垒动画资源_12帧_64x64/.火炮。策划原文：每30秒炮击，扣街垒最大生命值25%，炮弹极快、几乎不可拦截；第28分钟后出现火炮是阶段规则，本轮未接自动刷出。
- 新增Resources/enemy/Cannon.prefab、Resources/arrow/CannonShell.prefab。Cannon根节点是脚底，默认向右部署1.5单位（.4单位/秒）后停住；启用后首发30秒，之后每30秒实际出弹。第三帧火光对应2/12秒出弹延时；禁用取消待发炮弹，重启重新计时，卡帧不追补多发。
- Cannon脚本位于Script/Enemies，自动查找启用的Wall碰撞体，也可ConfigureSpawn(Collider2D)注入。炮车本体没有EnemyHP，不是地面可击杀目标，不占普通兵数量配额；策划没有炮车血量，本轮未新增该设计。右键组件“立即试射（运行中）”可快速测试。
- CannonShell脚本位于Script/Gameplay/Combat。炮弹默认80单位/秒、100HP、5秒寿命，无重力、Trigger、Continuous，额外CircleCast扫墙防高速漏撞。撞墙扣CeilToInt(GameManager.WallMaxHP*.25)，当前1000上限即250；与当前剩余血量、HeartFill进场分母无关，最低扣至0。一弹只结算一次，不叠加Bomb/Bullet固定伤害。
- SBullet和BulletHit最小扩展：W空中选择最近的手榴弹或炮弹，保留轨道隔离、Perfect一枪/普通两枪和Perfect实际命中免换弹；炮弹直线预判使用原二次方程，手榴弹重力迭代保持。炮弹没有BombHP，不参加手榴弹连锁；其他兵种、Down射击及准星逻辑保持。
- Resources/CannonAnimations含36张原始64x64透明PNG及2个controller、4个clip。Move/Fire来自用户指定.火炮，Fly来自同包炮弹/飞行；三组各12帧12fps、1秒，Move/Fly循环，Fire单次；Idle复用Move首帧。炮车32PPU、脚底pivot(32,8)，炮弹64PPU、弹体pivot(44,32)；Point、无MipMap、无压缩。原图字节与来源哈希已核对。
- SFX-ART-01部署、02开炮、03飞行、04撞墙已接SfxPlayer接口，未生成音效文件，缺失只警告一次。
- 本轮后台启动Unity被自动审批拒绝，工具返回需本会话未开放的审批权限；未完成原生导入、Animator播放或真实物理试玩。Prefab/动画以离线文件配置保存，224处本地/外部引用检查通过，GUID无重复。
- Assets/Script下41个运行时源文件及新增CannonPrefabVerification编辑器脚本使用Unity2022程序集引用独立编译通过。49项实际Cannon/CannonShell源码行为检查通过，Unity时间/查询接口为测试替身，不等同于原生物理通过。
- 新增Editor/Diagnostics/CannonPrefabVerification.cs；菜单“街垒/检查火炮预制体”只检查资源和隔离对象中的36帧原生采样，不打开或保存场景，报告Library/CannonPrefabVerification.json。尚未在本轮运行该原生入口。
- 第二幕场景、现有Spawn、其他资源和音频配置保持；仅修改既有SBullet/BulletHit两份运行时代码，保留其meta/GUID。新增两份运行时脚本、一份编辑器核验脚本，未增加asmdef。
- 说明、素材预览、原始来源清单、离线报告、修改前SBullet/BulletHit/HANDOVER备份：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/火炮预制体。工具与行为检查源码在work/cannon。

## 2026-10-19 火炮接入第二幕实际刷出流程

- 用户要求“把火炮写入项目”，本轮将既有独立Cannon.prefab接入现有Spawn运行流程，覆盖上节“未自动刷出”的状态。没有增加场景物体或修改场景文件，场景2现有三个Spawn通过新增字段默认值启用此分支。
- 默认enableCannon=true，cannonUnlockTime=1680秒，按策划第28分钟部署。三个Spawn共用cannonDeployed标记，随现有场景局部计时重置；只部署一门，不受普通兵概率、数量上限及Leader锁影响。场景中已有启用火炮则视为已部署，不叠加。只在场景名2运行。
- 从启用Spawn中按y排序选择中间轨道，脚底偏移-0.59375。若本场景存在正交Camera.main，初始x至少为镜头左边缘向内1单位；保持原炮车部署1.5单位、启用后30秒首发和每30秒炮击规则。Wall碰撞体由中轨Spawn.target或同场景Wall查询获得后注入。
- Spawn组件右键新增“立即部署终局火炮（运行中，全场一门）”，跳过阶段等待和自动开关但仍防重复，只对场景2生效。生成后可在Cannon组件右键“立即试射（运行中）”快速测试。正式解锁时间可改三个Spawn的Cannon Unlock Time，保持数值一致。不要Reset整个Spawn组件，以免覆盖原刷怪调优。
- 缺少Cannon资源或Wall时全场只警告一次，不占部署名额；Wall之后出现可恢复部署。Resources预制体按enemy/Cannon名字加载，无LoadAll下标依赖。
- 仅修改Assets/Script/Enemies/Spawn.cs，原meta/GUID不变，场景、两份Cannon prefab、动画、音频及其他现有玩法代码未改。
- 全部41个Script运行时源文件使用Unity2022引用集独立编译通过；15项实际Spawn源码部署检查通过，Unity接口使用替身，不等同于原生实机通过。本轮未运行Unity原生编辑器检查，既有编辑器启动/审批限制仍未解除。
- 使用说明及修改前Spawn/HANDOVER备份：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/火炮接入第二幕；实际源码行为检查位于work/cannon_integration。

## 2026-10-19 火炮尺寸、入场、单门限制与街垒判定修正

- 用户要求四项：开炮/行走大小一致；只走到露出大部分；只生成一门；Wall判定移植到街垒。
- 原始行走炮车本体约55x36像素，开炮无烟火帧约41x28。Cannon新增fireBodyScale=(55/41,36/28)，LateUpdate读取Animator实际Base Layer.Fire状态时校正显示比例，Idle/Move还原Awake时的原缩放。脚底pivot不变，不把火光/烟雾外框计入本体。原PNG及所有meta/GUID原样保留，没有重新生图或改SpriteRect。
- Spawn初始屏幕偏移默认-.9，实际值限制在[-2,0]，兼容此前Inspector正偏移。Cannon新增deploymentVisibleFraction=.8，在Start按当前正交镜头左边缘和SpriteRenderer完整边界限制remainingAdvance，约八成画布入画即停止；advanceDistance仍为上限。炮车停留左侧，不向中央推进。
- Cannon.Awake按场景handle登记唯一所有者，重复实例立即SetActive(false)并Destroy，FireNow/Update均不能绕过。OnDestroy只释放自身登记；SubsystemRegistration清理跨局静态状态。Spawn原全场一次部署标记继续保留，避免原炮销毁后自动重刷。
- 场景2的Act2Environment/Barricade现有物体已设置Wall标签并挂24点PolygonCollider2D，按木板/石块实体轮廓近似，不包括旗帜或透明画布。三个轨道y=-4.5/-2/.5均穿过该碰撞主体。
- 原Wall取消Wall标签、禁用原BoxCollider2D并禁用物体，原Transform留存供回退。三个Spawn.target全部由旧Wall Transform改指Barricade。GameManager.IsWall和敌人/敌弹/炸弹/炮弹碰撞接口无需改动。注意历史第7节等旧位置描述已过时，新的有效碰撞边界位于街垒图上。
- Scene2EnvironmentSetup已同步上述迁移，重跑街景配置也会将碰撞/目标保留在Barricade，不会复活旧Wall判定。CannonPrefabVerification新增尺寸参数、单一Wall、PolygonCollider24点和3个目标引用检查；只读附加场景2核对后关闭，不保存用户场景。
- 41份运行时源码和两份编辑器工具分别使用Unity2022引用集编译通过。57项实际Cannon/CannonShell源码行为检查通过（含尺寸还原、不同入口重复炮车、跨场景和80%入画停止），15项Spawn部署检查通过。测试中Unity接口为替身，未完成原生动画/画面/物理试玩。
- 离线核对场景引用、唯一Wall标签、旧墙失效及3条轨道交点；全部PNG和meta/GUID哈希保持，765个其他既有资源/设置文件保持。本轮只改Cannon/Spawn、Cannon.prefab、场景2和两份Editor工具，另追加本交接记录。
- 使用说明、离线结果与修改前文件：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/火炮与街垒修正；脚本与逻辑检查位于work/cannon_wall_fix。
- 部署时间仍用三个Spawn的Cannon Unlock Time调整。30秒炮击、25%最大血量伤害、拦截规则和音乐/心跳保持。

## 2026-10-19 街垒X轴水平镜像

- 按用户要求将场景2的Act2Environment/Barricade沿X轴翻转，localScale=(-1,1,1)。源图有大片左侧透明留白，因此以24点实体碰撞轮廓的水平中心为镜像轴，Transform.x由0补偿到8.59723698，使主体整体仍留在画面右侧、左右边界约2.051至6.546保持。
- 使用Transform负X缩放同时镜像SpriteRenderer与PolygonCollider2D，没有仅翻图而漏翻判定。Wall标签、三个Spawn.target引用、街垒Y/Z及原PNG/meta/GUID保持。
- Scene2EnvironmentSetup同步主体中心镜像逻辑，重新配置街景不会恢复原朝向；CannonPrefabVerification新增X镜像状态检查。两份编辑器工具按Unity2022引用集独立编译通过，离线核对镜像前后主体水平边界保持；本轮未做原生实机画面/碰撞核验。
- 修改前场景/工具/HANDOVER及边界核对结果：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/街垒水平翻转。

## 2026-10-20 第二幕新版像素建筑、街垒与路面接入

- 用户确认采用新版建筑/街垒，并修改新版石板路。Assets/Environment/Act2/Buildings.png、Barricade.png已原位替换为用户提供的新版PNG，图片字节保持；原meta/GUID保留。Cobblestone.png使用内置image_gen编辑新版路面：石块缩小、黑缝/亮边减弱、上密下疏的轻微透视，输出1672x941不透明PNG。
- 场景2中Buildings上提52/94.1世界单位，使建筑街边高度接近原版。新版街垒更高，改为等比0.85并保留X轴镜像，localScale=(-.85,.85,.85)，position约(8.16471838,-.828108395,0)，脚底落在原地面附近，主体中心保持画面右侧。历史上一节的scale=-1和x=8.597已被本节覆盖。
- 24点PolygonCollider2D已按新版木石主体重配，避开旗帜；碰撞随Transform等比缩放和镜像。三个Spawn.target仍指向Barricade，Wall标签唯一、旧Wall继续停用。y=-4.5/-2/.5三轨均与新主体相交。
- Scene2EnvironmentSetup同步新轮廓、建筑上提与街垒缩放/地面锚点；CannonPrefabVerification更新新版镜像比例检查。两份编辑器工具通过Unity2022引用集独立编译。
- 运行时玩法、火炮、刷怪、音乐与心跳脚本全部未改，所有原meta/GUID保持；765个其他既有资源/配置文件哈希保持。新图原位替换继续沿用既有Sprite引用。
- 本轮未完成Unity原生导入、实机画面或物理运行核验。输出预览为按素材/场景坐标离线重建，不含角色/UI，不应当作实机截图。原生资源与街垒检查入口仍是“街垒/检查火炮预制体”。
- 修改前素材、场景、工具和HANDOVER备份、最终路面PNG、布局预览、生成提示词及离线报告：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/第二幕像素美术更新；工作文件在work/art_refresh。路面使用内置image_gen编辑模式，未使用API/CLI回退。


## 2026-10-20 十章战役、过场接口与黑屏开始界面

- 按用户提供《街垒_剧情与关卡设计_20261020.md》配置10章，每章183拍，开场/结尾各1拍。用户明确选择保留现有0.5秒Perfect窗口；周期仍为等待+窗口，章节时长长于文档估算。音乐默认不参与判定。
- 新增Title.unity为Build Settings第一场景，黑屏任意键开始；UI为Assets/UI/Campaign/StartScreen.prefab，Silver中文字体。运行从Title开始；直开旧1/2仍保留调试流程。
- 新增Resources/Campaign/ChapterTable.asset（10章/214项波次）和CampaignRoot.prefab。StoryDirector持久化管理准备/防守/选择/抢救/结算/尾声，复用旧场景而非复制10份场景。进入战役时关闭旧100秒准备、随机刷怪和音乐判定驱动；旧说明中的28分钟火炮/30秒自动炮击只适用于非战役调试。
- 第4章护送、第5章间谍、第6章夜袭、第7章选择及后果、第8章火炮40拍部署与70/100/130/160拍开炮、第9章3弹无换弹/仅心跳/失败进10、第10章渐慢心跳和回忆文字已接入。护送采用现有无武器小型角色替身，专用儿童素材待补。
- 新增CampaignBeatClock、RunState、CampaignWaves、CampaignActor、CampaignProjectile；现有脚本接入3弹弹匣、按拍换弹、连击、三种建造资源、章节耐久600、一次最后一拍抢救及章末统计。战役不再被旧WallCharge/ConstructionStageFlow初始化覆盖。
- ChapterTransition为开场/结尾/每章entry/prepareToDefense/exit预留cinematicPrefab、Timeline、AudioClip、时长、跳过和完成信号。CampaignTransitionRelay可由动画事件或Signal调用完成/音效/字幕；尚无正式过场时显示文字占位。现有结尾预制体已绑定。
- 运行时50份与编辑器11份源码使用Unity2022引用集独立编译通过；实际时钟/章节表/状态源码7595项断言通过；321个序列化引用、214个波次和1830+2拍核对通过。既有场景1/2、纹理/音频/字体和原meta哈希保持。尚未做Unity原生导入与完整战役试玩。
- Unity核验菜单：街垒/战役/检查十章配置与开始界面；报告Library/CampaignVerification.json。实际音效仍以心跳为主，夜袭引信等需补音频。长按/切分尚未实现，本轮使用单键、休止、双键。开场音频延迟仍待实际设备校准，现有补偿保留。
- 完整使用/插槽说明、章节JSON、离线核验、开始界面离线布局预览、设计原件及修改前Script/ProjectSettings/HANDOVER备份：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/十章关卡与开始界面。工作脚本在work/campaign，部分为一次性补丁，请勿直接重复运行。


## 2026-10-20 文档核对、完整波次与现行机制落地

- 用户要求先核对桌面“街垒_工程文档”，申请所需权限后完成项目；已读14份文档，核对并记录数值/时间/状态错误。桌面文档、工程、Unity本地目录与许可证联网权限已获会话授权。
- 完整规则与运行说明见桌面“街垒_本轮实施与验收_20261020.md”，本节覆盖上一轮的资源、连击、190血、休止、9章3发、炮击160拍等说明。
- 新增16份WaveScript A/B资产、WaveDirector、WaveTiming/WaveThreat/WaveSquad/WaveAudit/WaveDefaults/WaveExitVisual；共202事件，A/B仅镜像轨道，按完整周期和实际几何生成，F4调试，上限一般8、第9章12。第8章70/100/130/152炮击，155末炮命中；9章章首恢复炮车。
- 第4章3次护送：30中20拍、70低25拍、110中25拍；104高轨小队仅与第三次交叠，exam标记；同轨士兵接触判伤，弹丸无害/不挡弹，不再发护送专用敌弹。3次全活×0.8、死亡×1.5封顶2，进入下一章重置。
- 删除所有建造道具/连击/连击穿透，保留分数与失误。双键为跨手、边沿、120ms、基础15/5；移除休止/长按/切分，保留3秒REST。ConstructionJudgement为纯输入状态，可直接测试。
- CampaignMagazine为纯弹匣状态；所有战役每拍一次尝试，完整2拍普通换弹，4章按秒/倍率。Perfect R可打断换弹且不占射击资格，HUD闪金色；最新Perfect实际命中仍可取消正在换弹。第9章无限弹与21兵/12小队/6炮弹，无胜利/最后一拍分支。
- 短章建造收益比例放大，满Perfect可达600，防守上限固定600；2次最后一拍补偿（9章除外）。第7章63拍超时放走，处决回血保留至8章起始。
- 最后4拍齐射100伤、拦空中弹丸、停步/转身/淡出；归零优先失败，不发Perfect/分数，禁玩家开火。末拍章节卡继续两拍心跳；实际总拍数包括转场/抢救/重试，不强填1832。
- 修复跨阶段与同拍判定覆盖、漏拍回弹、诱饵残留、玩家弹丸寿命、无目标重复尝试、延迟出生即伤、9章转场音乐、转场台词、信号等待超时等；开场增加可跳过的音量/延迟试听，标题F2重调，复用原补偿保存与心跳排程。
- 修正EnemyAnimatorSetup浮点曲线末帧持值，版本3，避免单帧Idle长度0，保留备份机制；尚未在Unity原生重建。新增导入/保存校验WaveValidation与ProjectVerification.Run/BuildWindows菜单/批处理入口。
- 62份运行时、14份Editor源码使用Unity2022引用集独立编译0错误/0警告；131365项实际源数据/时钟/判定/弹匣断言通过；16资产逐字段一致，421处引用通过，639既有图像/音频/字体/meta/场景哈希保持，Silver关键中文/∞/箭头覆盖通过。末端审计0错误，2警告是9章154拍双轨末兵，由齐射收场。
- 原生Unity启动退出140063，无指定日志；本地版本启动亦无输出，Windows事件日志读取无系统权限。没有原生导入、物理/音频设备/完整战役试玩或Windows构建，不应称为实机完成。没有写伪造Library成功报告。
- 输出：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/波次配置更新，包含实施/验收说明、原始问题逐项状态、数据JSON、离线引用/末端报告、修改前Script/Campaign/HANDOVER/桌面文档备份。工作脚本在work/waves，多数安装脚本为一次性，请勿重复运行。

- 最终补充：为保持第8章“至少拦截2炮”目标，A/B资产requiredShellInterceptions=2，HUD显示实际击落进度；抢救不会绕过目标，齐射不计入；未达成时收场前本章失败。其他章目标0。


## 2026-10-20 街垒生命三阶段

- 用户图一/图二分别导入Assets/Environment/Act2/Barricade_Stage1.png、Barricade_Stage2.png，PNG字节原样，原Barricade.png保留为第三阶段。新增BarricadeHealthVisual并绑定场景2 Act2Environment/Barricade，按WallHP/WallMaxHP即时切换：<33%图一，>=33%且<66%图二，>=66%原图。600上限阈值198/396，整数比较避免float边界误差。
- 两张新图继承原街垒负X缩放-.85，Renderer.flipX保持false，镜像一次。自定义pivot将可见主体中心与原图脚底对齐；第二图1671宽也已补偿。不改原图、Transform、24点PolygonCollider、Wall标签或Spawn目标。
- Scene2EnvironmentSetup同步新增素材与组件配置；重新配置街景也保留此功能。编辑状态满血图，运行时实时响应伤害/回血/抢救。
- 说明、图片哈希/锚点、核验及修改前备份：%USERPROFILE%/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/街垒生命三阶段。独立源码编译/阈值检查通过，未做Unity原生画面试玩。


## 2026-10-20：统一 Silver 字体与可编辑开始页

- 用户要求所有字体使用工程内字体，并让开始页易于编辑。实际字体为 Assets/TTF/Silver.ttf。
- 新 GameFont.cs 与 Resources/Campaign/GameFont.asset 引用原字体；准备场景 2 处默认字体替换为 Silver，战役/波次/心跳调试文字统一使用 Silver，移除 StoryDirector 的系统字体回退。
- Title.unity 放置 StartScreen.prefab 实例，编辑态即可调整。新 TitlePage/Prompt、Subtitle、Background 独立于关卡 MainText/Detail/HUD；StartScreenView 提供呼吸开关/频率/最低透明度。
- CampaignUI 首次进入时接管场景实例，通关返回重新接管并销毁旧画布；开始页原文案与样式不会被校准/章节文字覆盖。开始输入与 F2 校准保持原入口。
- CampaignVerification 允许自定义开始文字和背景，同时检查新界面绑定与 Silver 引用。
- 编辑说明见根目录 START_SCREEN_EDITING.md；备份/修改.diff/核验结果.json 在工作区 outputs/字体与开始页。
- 验证：65 个运行时脚本与 14 个编辑器脚本离线编译通过，8 个序列化 Text 均为 Silver、UI/场景引用静态检查通过。原生 Unity 导入/运行/视觉检查仍未完成，不能标为 Play 测试通过。


## 2026-10-20：建造场景右侧街景与向左搬运

场景 `Assets/Scenes/1.unity` 已放置 `ConstructionEnvironment`，包含石板地面、用户长街图和街垒。长街原 PNG 原样导入为 `Assets/Environment/Act2/StreetPanorama.png`，采用 Point、无 Mipmap、无压缩，未裁剪或镜像建筑。

街垒沿用场景2的世界位置 `(8.16471838, -0.828108395, 0)`、缩放 `(-0.85, 0.85, 0.85)`、Sprite 和 33% / 66% 生命阶段。建造阶段显示三阶段随生命增长的效果，仅复制视觉组件；战斗场景保持原样。

建造相机向右移至 `X=8.697083`，正交大小仍为5，以16:9为参考取长街右侧约 `669～2038` 像素范围。建筑可见高度、街边基线与战斗背景对齐，石板地面覆盖视口。

主角在画面约71%的取料点拿起材料，向左移动到约43%的放置点，紧邻街垒右缘，然后返回。街垒主体位于画面约12%～39%。原有主角大小、移动速度、搬运/放下动画、QTE判定和章节计时沿用原设置。

可在 Hierarchy 的 `HeroConstructionStage / MaterialPickupPoint` 和 `MaterialPlacePoint` 调整路径，在 `ConstructionEnvironment` 调整美术；相机是场景1内的 prefab 实例覆盖，不会改变共享相机 prefab 或战斗相机。主角动画工具完整重建时会保留已经编辑的搬运点。

验证：引用与空间布局静态核验通过，14个编辑器脚本离线编译通过；原图字节与战斗场景文件哈希保持。当前没有可连接的Unity Editor，原生导入、Play模式和实际画面效果仍待在Unity内确认。


## 2026-10-20：修复章节建造环节搬运动画不触发

根因：章节模式在 ConstructionStageFlow.Awake 中停用旧的独立计时流程，该流程 IsBuilding 始终为 false；HeroConstructionDriver 的输入回调却仍检查它，因此 Tap.NotifyCampaignInput 已发出事件、街垒耐久正常增长，主角搬运动画却从未启动。

修复：章节模式读取 StoryDirector 的 Preparation 状态；单独运行场景1继续使用旧的独立计时判断。正确的普通/Perfect 输入触发拿起、向左搬运、放下、向右返回。章节离开建造时清理待执行搬运、移动/搬运/休息状态和掉落箱，不继续在过场或尾声中搬运。保留原 Timeline 控制权及原有三秒错按休息逻辑。

验证：同一回归用例运行修复前源码，稳定停在“章节模式输入应启动 PickUp”断言；修复后使用实际 Tap、搬运驱动、HeroActor 和状态机等源码通过3003项断言，覆盖完整往返、朝向、Animator Motion参数、休息恢复、章节边界、单独场景及过场控制。Unity引擎API由确定性的.NET测试宿主代替，这证明逻辑链路，不代表原生动画帧渲染验收。65个运行时脚本和14个编辑器脚本离线编译通过。

没有改动上一轮的右侧长街构图、街垒位置、搬运路径、移动速度或QTE收益。当前未完成Unity内Play模式实际播放核验。

核验记录与修改前备份：工作区 outputs/建造搬运动画修复。


## 2026-10-21：用户提供的新建筑背景与地板

- 用户两张PNG原样替换：3072×768透明建筑图同时替换 Buildings.png（防守）及 StreetPanorama.png（建造）；2048×768石板图替换共用的 Cobblestone.png。
- 保留原GUID和两个场景文件。通过Sprite的PPU和自定义pivot保持旧建筑的可见高度、街边基线；建造背景保持右边界，地板保持10单位高度并横向覆盖视口。未裁剪、重绘或拉伸原图。
- 建筑最大导入尺寸设为4096，保留3072像素原宽；Point、无MipMap、无压缩保持。Scene2EnvironmentSetup同步了防守背景和地板的导入参数。
- 原文件备份和校验记录在 %USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/背景地板替换。静态字节、GUID、场景不变和覆盖范围核验不等于Unity原生画面验收。

## 2026-10-21：第一章四图、音乐入口与街垒连续生命（现行规则）

本节覆盖旧“每章183拍”“第一章建造”“进章清零/补满”“短章收益放大至600”和旧音乐说明。

- 第一章改为四图连续演出，图片原样存于 Resources/Campaign/ChapterOne/Shot01～04.png，顺序为送葬入街、红旗拦车、学生登车、人群起义。8/6/10/8拍，总32拍，固定1.35秒一拍，约43.2秒，0.6秒叠化。无QTE与章末按键等待，自动衔接第二章。
- 151拍余量加入建造/修补：第2章127拍；3/5章40拍；4/6章44拍；8/9章48拍。第7章没有建造，第10章尾声183拍。防守、选择和16份A/B波次资产保持原配置；总章节预算1830拍，HUD使用各章实际总拍数。实际总拍数另含边界拍、收场、抢救及重试。
- 标题静音，开始战役并完成校准后播放原完整 Do You Hear The People Sing.mp3。同一首歌跨1～8章切场不重播，第9章保留仅心跳，第10章恢复。旧OneDayMore与衔接切片文件保留在工程中，正式战役不再绑定/播放。过场额外音频使用独立声源。
- 删除章内叙事行和尾声滚动文案，保留操作提示、功能选择反馈、章前/章后过场及结算。原ShowCaption只在Transition阶段开放；玩法操作提示改用ShowGameplayHint。
- 新游戏街垒0/600，第二章正式建造，后续章节继承真实剩余生命并在此基础上修补。进章不清零、不补满。全章统一单键Perfect10/普通0、双键Perfect15/普通5，去除短章倍率。双键教学移至第二章49拍。
- RunState.ChapterStartHP记录章首生命；失败重试恢复此值而不是累加修补。第7章处决30%回血自然继承，第8章不再重复发放。版本2存档保留当前生命和章首生命；版本1不兼容且未删除原文件。
- ChapterTable.firstChapterFrames逐镜头提供Sprite、beats、subtitle、voiceOver、voiceDelaySeconds；字幕与配音默认空，另有ChapterSequenceView.FrameStarted事件。序列页面在既有Canvas内且低于暂停/倒计时页面，图片保持宽高比。Cinematic枚举追加在末尾以保留既有枚举值；第一章可暂停，镜头中途不开放存档。
- 新增CampaignRevisionRules和ChapterSequenceView。独立编译：74运行时/16编辑器，0错误0警告。实际纯逻辑源码10455项回归通过；该测试不模拟原生渲染/物理。
- 原生Unity启动退出140063且无日志，新版尚未完成原生导入、画面/音频设备及完整战役试玩。新增菜单“街垒/战役/检查四图第一章与跨章生命”（ChapterRevisionVerification.Run），校验成功后保存资产与场景；不要把历史报告当作本轮原生通过。
- 修改前快照、实施说明、拍数表、编译/逻辑/静态核验与启动记录位于 %USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/第一章与战役调整。用户要求完成保存与HANDOVER后自动关机；关机命令的实际安排/执行状态另记同目录，不以此文档声称关机已发生。

- 保存补充：建造/修补HUD现按阶段显示实际操作提示；本轮文件与HANDOVER均已保存到磁盘。关机采用延迟后的正常关机请求，不加/f；具体安排/命令返回见输出目录“关机状态.json”。


## 2026-10-21：集中修改章节名字的编辑器入口

- 菜单“街垒/战役/编辑章节名字”打开ChapterNamesWindow，使用UI Toolkit集中编辑10个名字；点击保存后只更新ChapterTable.chapters[].title，支持Undo，空名字会阻止保存。
- 原有章名与ChapterTable.asset保持原样，本轮未采用讨论中的新章名。运行时章节入口、HUD继续读title，最终结束页也改为读第10章title，避免尾声名字写死。
- 也可直接选择Assets/Resources/Campaign/ChapterTable.asset，在Chapters/Title中修改。使用说明见根目录CHAPTER_NAMES_EDITING.md；备份、核验及说明位于 %USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/章节名字编辑接口。
- 本轮编辑器窗口尚未在Unity原生环境打开验证；源码编译与配置不变核验结果见上述目录。


## 2026-10-21：第二章纯建造与63拍重分配（现行编排）

- 第二章保留127拍建造，取消63拍防守，建造完成后走章末过场/结算，继续到第三章；ChapterTable解除第二章A/B与Legacy波次引用，WaveDefaults不再生成第二章战斗。旧Chapter02_A/B资产保留未修改。
- 63拍全部加入第3～9章修补，增量12/8/8/10/8/10/7。现行准备拍数为0/127/52/52/48/54/8/58/55/0；防守为0/0/163/163/163/163/120/163/163/0。第7章选择63，第1章四图32，第10章尾声183，总章节1830拍。
- 第7章流程新增8拍修补→120拍防守→63拍选择。其余防守内部事件、A/B资产、章名、过场插槽、音乐及收益保持；街垒真实生命继续继承，重试还原章首值。拍数守恒不保证实际秒数固定。
- 默认准备/防守拍数统一经CampaignRevisionRules读取。ChapterTable.Validate拒绝第二章防守/波次误配；WaveVerification报告实际启用14份A/B配置，磁盘仍有16份历史资产。ChapterRevisionVerification增加防守拍数核对。
- 版本2存档继续使用；仅拒绝恢复已取消的第二章Defense/LastChance存档，提示开始新游戏并保留原文件，避免读档绕过取消的战斗。
- 74运行时/17编辑器独立编译0错误0警告，10604项实际纯逻辑检查与54项配置检查通过。原生启动与Play状态见输出目录记录；未完成原生试玩，不把历史日志作为本次运行验收。
- 调整、架构分析、备份与核验位于%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/第二章纯建造与架构分析。本轮源文件、资产与HANDOVER已写入磁盘；Unity原生SaveAssets/场景试玩尚未确认。


## 2026-10-21：章节结构编辑、自动试玩记录与恢复校音

- 新菜单“街垒/战役/编辑章节结构与内容”，UI Toolkit草稿窗口集中配置十章名字、阶段拍数、节奏、过场、第一章四图/字幕/配音、收益和A/B引用。支持临时副本校验、保存/Undo、波次检查器入口、导出已保存配置。阶段仍按当前顺序，未实现任意图式编排；第一章四图、第二章纯建造、第七章选择、第十章尾声约束保留。
- ChapterTable新增playtestLabel、recordPlaytests、calibrateEveryNewGame、targetBeatBudget、enforceBeatBudget。默认标签baseline-20261021，记录与每次新游戏校音开启，目标1830且不强制锁定。实际各章拍数沿用上一轮，不作平衡调整。诊断改为校验可编辑配置，不再强制历史拍数/事件常量。
- 校音缺失原因是CalibrationReady持久标记导致跳过；BeginCampaign默认每次新游戏先OpeningCalibration，读取已有音量和延迟，Enter保存/Esc跳过；F2标题校音保留。校音后进入游戏才播放People Sing。
- CampaignMetrics为可独立测试的统计聚合，CampaignTelemetry负责本地Playtests目录与配置快照/指纹、run.json、summary.csv、events.jsonl。每次新游戏/读档独立运行目录，逐章逐次尝试，5秒及关键边界刷新，汇总原子替换，写失败不阻断玩法。
- 指标含有效/暂停/过场/修补/防守耗时，首尾/最低/最高耐久、受伤/回复/溢出、首次满血与满血修补时长，总及阶段Perfect、空击/开火、换弹/Perfect换弹、抢救、REST、炮弹拦截、护送、选择与A/B变体。不把暂停和读档片段混成完整游玩。
- 菜单“街垒/测试/打开试玩数据目录”和“街垒/测试/查看试玩统计与对比”可打开本地记录、比较两次运行并导出CSV。对比排除未完成、退出与loaded_partial；日志不上传。
- 新存档记录配置指纹；编排变化或旧存档无指纹时拒绝恢复并保留文件，要求新游戏，避免调试期间套用过期阶段时钟/波次。
- 76运行时/19编辑器独立编译0错误0警告；1861项实际纯逻辑/配置/存档/持久化检查与CSV往返通过。合成样本明确标记，不是真实玩家记录。原生Unity启动/Play结果见输出目录，不声称已完成原生窗口、声卡和战役验收。
- 根目录CHAPTER_EDITING_AND_PLAYTESTS.md为使用说明；工作区outputs/章节编辑与试玩数据包含说明、备份、差异、校验和启动记录。源代码、资产与HANDOVER已写入磁盘。


## 2026-10-21：第七章结尾约10秒限时选择（覆盖旧63拍选择）

- 第7章choiceBeats从63改7，当前每拍1.35秒，名义上限9.45秒；本章预算135拍，十章1774拍。缩短的56拍未重新分配，其他章节拍数与波次不变。ChapterTable默认值、CampaignRevisionRules预算、资产targetBeatBudget同步1774，测试标签choice7beats-20261021。
- AfterDefense在章末两拍卡之前直接进入选择；移除选择内Clock.Opened>=2输入门槛，首帧即可Q/E。倒计时显示剩余秒数，选定后立即CompleteChapter；章末过场显示已放走/已处决沙威。正常章末反馈与结算保留。
- 到期默认Released，同帧到期优先于新按键；ResolveChoice用Phase、flowBusy、Unchosen防重复提交和处决回血。E仍回复30%上限，Q/E对第8章后果不变；新增choice日志事件，区分timeout_released。
- 原有暂停/恢复冻结继续适用。配置变化需新游戏，原存档保留。根目录CHAPTER_EDITING_AND_PLAYTESTS.md更新当前拍数。
- 76运行时/19编辑器独立编译0错误0警告，实际选择方法与真实时钟的15项流程断言、1861项统计/配置/存档/持久化回归通过。原生Unity此前启动超时，本轮不声称Play通过。
- 修改、备份、源码差异、合成样本和核验见工作区outputs/第七章限时选择；工程文件与HANDOVER已保存到磁盘。


## 2026-10-21：重新分配选择缩短的56拍（现行预算恢复1830）

- 按用户要求，将第7章选择63→7所释放56拍分给第3～9章，每章防守+8拍。准备仍0/127/52/52/48/54/8/58/55/0；防守现0/0/171/171/171/171/128/171/171/0；第7章选择7、总143拍，第1章四图32、第10章尾声183，章节合计恢复1830。
- 没有延长修补或选择。StoryDirector源码保持上轮哈希，选择仍名义9.45秒、首帧输入、立即提交、超时放走、暂停冻结。
- 每份启用A/B波次末段新增一名普通士兵，标记choice-time-redistributed，alignTo为防守总长-10，参考beat为总长-24，advance9；B镜像轨道。原有所有事件保持。新末尾四拍齐射随防守长度顺延；护送、间谍、炮击原事件时机不变。
- 同步14份波次资产、章节表Legacy索引、防守总长默认规则、WaveDefaults新增事件、ChapterTable目标与默认预算1830。测试标签choice7-redistributed-20261021；旧配置存档指纹不匹配时保留文件并提示新游戏。
- 76运行时/19编辑器独立编译0错误0警告；实际14资产通过359项真实WaveScript/WaveAudit/WaveTiming断言，66项配置/原事件保持/总预算检查通过。未做原生Play或实际难度验收。
- 备份、说明、差异和保存校验见工作区outputs/选择缩短拍数重分配；根目录CHAPTER_EDITING_AND_PLAYTESTS.md同步。文件与HANDOVER已保存到磁盘。


## 2026-10-21：第3～7章难度、护送效果跨章与弹药像素UI

- 新ChapterDefinition.combatDamageMultiplier，3～7章默认2.5/3/2/2.2/3；GameManager实际受伤统一应用倍率，理论计划伤害上界为725/690/820/902/720。8～9章倍率1，1830拍总预算、波次、敌人血量、玩家伤害与第7章7拍选择保持。章节编辑器可调倍率，配置快照schema2含倍率，测试标签combat-v2-escort-carry-20261021。
- RunState.ResetChapter不再清护送计数/ReloadMultiplier；NewCampaign显式重置。EnterChapter保存/重试恢复ChapterStartEscortSuccesses/Failures/ReloadMultiplier，防第四章重试累积惩罚，后续章重试保留传入效果。实际CampaignStateCodec保存所有新字段。
- 第4章普通装填秒数、5～8章普通2拍装填均乘护送倍率；成功×0.8、失败1.5/2、上限2。Perfect瞬补保留，第9章无限弹不使用装填倍率。CampaignMagazine锁定装填总长，新增Progress用于真实进度；时长字段随嵌套状态存档，结束时间沿既有校正，读档进度测试通过。
- 新Resources/UI/AmmoHUD.prefab与AmmoHudModel/Geometry、AmmoHudGraphic、AmmoHudView，CampaignUI自动挂载到现有Canvas右下角、暂停页下方。原生像素矩形网格绘制暗铁框、旧纸包弹与铅灰细节，Silver字体；余弹/低弹/空弹、装填时间/进度、Perfect高亮、护送倍率、第9章∞。只防守阶段显示，不挡输入；缓存并重取实际SBullet。移除顶端重复弹药文字。
- 统计新增carriedEscortSuccesses/Failures、reloadMultiplier，本章escortSuccesses/Failures改为新增量，避免跨章重复计数。章首立即记录传入倍率，波次变体只读取匹配当前章的调度器。
- 79运行时/19编辑器独立编译0错误0警告；24661项规则/实际反射存读档/UI模型检查（多数为几何边界组合）及159项配置/预制体引用检查通过。离线UI预览已查看，不能当成Unity截图。原生启动40秒超时且无日志，未完成Play与实际难度验收。
- 根目录COMBAT_BALANCE_AND_AMMO_UI.md和CHAPTER_EDITING_AND_PLAYTESTS.md更新；工作区outputs/战斗强化与弹药UI含备份、预览、说明、差异、编译与核验。旧配置存档会被指纹拦截并保留，请新游戏测试。

## 2026-10-21：巴黎像素全界面改版与第四章携弹扩容

- 按新版 paris-retro-pixel-art skill 继续。旧金属弹匣网格由真实皮革材质与圆铅弹替代；标题、校音/选择/抢救/结算卡、HUD/提示、暂停/存读档、恢复倒数统一旧纸、炭褐墨色和深褐皮革。建造字母/计数及心形血量也接入样式；准星保留现有像素轮廓，进度改为读取真实装填过程。
- 第四章每次成功护送 +1 携弹上限，当前三次护送使基础3发最多扩至6发。扩容不凭空补当前余弹，完成装填补满；新防守场景按已解锁上限装满。六个显式槽位、余量/容量、永久扩容标签及一次救援提示均已接入。
- RunState.AmmoBonus不再在ResetChapter清零；新增ChapterStartAmmoBonus检查点，跨章/存读档保持，重试第四章回滚章首，后续章节重试保留，新游戏清零。已结算护送对象仍通过resolved/Alive防重复恢复发奖。
- 护送装填加成/惩罚继续跨章沿用原规则，但玩家HUD和提示不再公开倍率或对应颜色；内部试玩统计保留倍率，并新增ammo_capacity列及ammo_capacity_reward事件。
- 修改Assets/UI/Campaign/StartScreen.prefab和Resources/UI/AmmoHUD.prefab；Resources/UI/Paris1832/Style.asset统一材质/配色。ParisUITheme默认useAuthoredLayout=true，保留预制体编辑的位置和字号。缺少预制体时才走兼容布局。四张生成母版与实际提示词位于Assets/Art/UI/Paris1832；使用版Point、无mipmap、无损，尺寸384×256/384×112/24×24/144×64。
- 新增Unity菜单“街垒/界面/检查巴黎像素界面与护送扩容”和“街垒/界面/将巴黎样式保存到开始界面预制体”。根目录UI_REVISION_AND_REWARDS.md详述接口、保存、奖励与验证方法。
- 82运行时/20编辑器实际源文件独立编译零错误零警告；821项逻辑回归及1537项资源/预制体检查通过，CSV列往返通过。章名、拍数、伤害倍率、四图、过场/波次引用与本轮前完全一致；仅更新测试标签paris-ui-v3-escort-capacity-20261021、配置指纹schema=3，旧测试存档提示新游戏并保留原文件。
- 原生Unity批处理ParisUIVerification.Run启动退出140063、未生成日志，未完成原生UI渲染/点击/Play验收。工作区outputs/巴黎UI与第四章奖励中PNG均为离线排版预览，不是Unity截图。需在正常Editor从Title执行校音、救援扩容、暂停存读档和跨章重试验收。
- 本轮工程脚本、预制体、资源、说明与HANDOVER已直接写入磁盘；备份、哈希清单、编译/回归/资源报告和启动记录存于%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/巴黎UI与第四章奖励。



## 2026-10-21：第四章同一小孩三段完整护送，只奖励一发

- 根据用户更正，第四章三次出场为同一个小孩的三段护送。仅三段全部成功、章内无失败时，在第三次结算一次性奖励 +1 携弹容量，基础3发变4发。第一、二段成功不提前扩容；任一段失败，整次救援不奖励容量。本条覆盖上一轮每次 +1、最多6发的说明。
- RunState.RegisterEscortResult返回本次是否真正发放容量奖励；容量奖励最高1，CampaignActor仅在该返回值为真时记录ammo_capacity_reward事件与显示扩容提示，其余返回只显示分段护送反馈。原有resolved保护、跨章保存、反射存档、章首检查点和新游戏清零继续适用，后续重试不叠加奖励。
- 装填倍率的隐藏效果保持上一轮规则与跨章行为，玩家UI仍不公开。弹药界面按真实容量显示3或4个圆铅弹位置，永久扩容文字为+1发；当前装填弹数不会因扩容自动增加。
- 同步修改编辑器验证、UI_REVISION_AND_REWARDS.md和测试配置，标签paris-ui-v4-escort-allthree-20261021、配置指纹schema=4；旧规则存档保留并提示重新开始，避免继承旧的逐次扩容数值。
- 82运行时/20编辑器实际源文件独立编译零错误零警告；545项逻辑检查及CSV容量=4往返通过。覆盖八种三段成败组合、奖励仅在最后一段发放一次、分段读档、第四章重试回滚、5–10章继承/读档/重试、新游戏清零及原隐藏换弹规则。此次未重复启动已知退出140063的Unity原生验证，仍未声称原生试玩通过。
- 更新预览为完整护送后3/4弹、扩容+1发，均为离线排版。备份、编译/逻辑报告、CSV和修订预览在%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/第四章完整护送奖励。项目和交接已写盘。


## 2026-10-21：去除装饰纹理、隐藏拍数/HP、合并心脏与弹药

- 用户指出上版UI与人物/场景不一致，改为简洁炭黑/板岩半透明底及浅色文字，所有实际玩家界面不再使用旧纸/皮革/金属面板。保留原功能节点和菜单动作绑定；新增浅色标题字图，未标为已获用户美术确认。
- 游戏HUD移除章节拍数与耐久数字，建造合拍计数隐藏；菜单存档摘要、结算分拍图、结尾总拍数和耐久恢复百分比也移除。内部时钟、HP和试玩统计保留。选择剩余秒数、恢复倒数、校音参数保留。抢救文字不再按Perfect窗口显示“现在按空格”，交给心脏搏动/静止提示。
- Resources/UI/AmmoHUD.prefab改为408×144右上角统一组，偏移(-32,-24)，心脏在左、铅弹与余量/容量在右。建造隐藏弹药部分只保留心脏；战斗显示两者。HeartPulse的红色生命层与灰底为24×24新素材，母版同框裁切，实测alpha阈值128轮廓差异0像素。Point、无mipmap、无损导入。
- AmmoHudView在DefaultExecutionOrder(200)读取原场景Heartbeat(100)的缩放和HeartFill比例；不新建计时或声音组件，不移动原场景对象到常驻Canvas。原场景心脏Image在战役中隐藏，计时/音频仍保留，避免重复显示/跨章叠音。独立调试场景继续显示新心脏素材。读档后立即RefreshNow生命显示，并清除/重建旧HUD与结果/选择/抢救提示，防止旧数字回流。
- 第四章三段全成功才3→4发、隐藏装填效果、跨章/读档/重试均沿用。RunState、章节资产、配置指纹及原场景文件经哈希确认未变，当前同规则存档可继续使用。
- 83运行时/20编辑器实际源文件独立编译0错误0警告；590项逻辑检查和CSV往返、1658项资源/预制体/呈现检查通过。含心脏/弹药同父级、搏动不重叠、底部向上填充、点击遮挡、无重复心跳以及第四章奖励守恒。此前Unity原生启动退出140063无日志，本轮未重复尝试，仍未完成原生渲染/点击/声画同步/Play验收。
- 根目录UI_HEART_AND_AMMO.md为现行说明，Assets/Art/UI/Paris1832/PROMPTS_COMPACT.md为实际内置生图提示词和母版记录。工作区outputs/心脏与弹药统一界面包含10张离线排版预览、备份、编译/回归/呈现检查和变更哈希；均不是Unity运行截图。工程与HANDOVER已保存到磁盘。


## 2026-10-21：根据“UI过于粗糙简陋”精修

- 前版纯色矩形被用户指出粗糙；新增Frame-v3独立木/暗铁窄边框素材，板岩深底、克制材质、米灰文字与深红细线。统一主菜单、暂停、校音/选择/结算卡片、恢复倒数、建造提示和战斗HUD。按钮补齐普通/悬停选中/按下/禁用呈现，主操作用淡深红色调。此版为已接入候选版，不是用户确认终稿。
- 直接修改既有StartScreen及AmmoHUD预制体，保留功能节点和菜单动作绑定；整理标题/正文/次级提示的字号与留白，收窄左上提示，弹丸下增加托架。新版原始2172×724母版保存在Art/UI/Paris1832/Masters，288×80运行素材采用Point、无mipmap、无损、九宫格border(14,12,14,12)。PROMPTS_REFINED.md记录内置生图原提示词和裁切/导出事实。
- 保留心脏/弹药同组、无拍数/耐久数字；第四章三段全成功才+1容量、隐藏装填效果、章节/波次/存档指纹均不变。没有重建原场景心跳与音频组件。
- 83运行时/20编辑器实际源文件独立编译通过；590项现有逻辑回归及1908项资源/预制体/呈现检查通过。编辑器原检查菜单已纳入frame导入检查。11张离线预览已查看，包含菜单选中示例、战斗/危急/扩容、校音/暂停/选择/结算/建造/倒数；不是Unity运行截图。此前原生Editor启动问题仍未解决，尚未实机验证点击/渲染/声画同步。本轮没有重复失败启动。
- UI_HEART_AND_AMMO.md为当前编辑说明。工作区outputs/精修像素界面保存修改前备份、编译/回归/资源检查、离线预览与最终差异哈希。全部项目文件及本交接已写盘。


## 2026-10-21：庄重宽幅UI候选版

- 用户要求消除廉价感并增强史诗感。取消上一版木/铁外框和小牌式按钮：场景铺满、左侧暗场、大型文学宋体风格米灰字标、深红竖线、文字菜单和细线焦点。暂停/校音/选择/结算改宽幅暗场和灰红分隔线，恢复倒数取消矩形卡片。游戏HUD去除木框、保持心脏和弹药同组。未标为用户确认终稿。
- 新增ParisBackdropGraphic（uGUI.Image子类，32段顶点透明度暗场）和ParisMenuAccent（鼠标/键盘焦点、禁用颜色、细线）。普通Button继续负责原动作、导航和按下过渡；原功能对象、组件ID和动作引用保留。修正主题应用时次级文字颜色被覆盖的问题，重复应用复用组件/装饰，不叠加。
- 新TitleEpic-v4字标母版和320×160运行精灵、Point/无mipmap/无损；内置生图先石刻再压平，只有第二次输出进入项目。PROMPTS_EPIC.md保留两次原指令、实际裁切/最近邻导出事实。frame/paper/leather等旧资源保留为历史，不再用于当前样式。
- UI_HEART_AND_AMMO.md为最新编辑说明。心脏/弹药位置、无拍数/HP数字、第四章三段全成功才3→4发、隐藏装填奖励及原章节/波次/音乐/存档指纹不变，无新心跳/音频调度器。原1/2场景及规则资产哈希保持。
- 85运行时/20编辑器实际源文件对Unity程序集独立编译零错误/零警告；590项现有纯逻辑回归与CSV检查、2585项资源/预制体/呈现检查通过。含原菜单动作引用守恒与装饰点击穿透。既有编辑器检查已增加焦点和暗场组件检查，但尚未在Unity内执行。
- 工作区outputs/史诗感界面包含11张离线排版预览、修改前备份、编译/回归/资源检查和最终哈希。另有320×180/4倍图仅对比字标和场景材质，不作为UI最低可读尺寸；参考画布仍是1920×1080。预览不是Unity截图，字体/渐变是近似模拟。此前Editor退出140063无日志问题未解决，尚未原生Play、点击/键盘导航/Canvas字体渲染/声画同步验收，本轮未重复失败启动。全部项目文件与本交接已写盘。


## 2026-10-21：局内UI延续庄重风格

- 用户要求局内UI也调整。顶部改成全宽向下渐隐信息底色与灰/深红细线，左侧章节42/操作26/任务26分层，右侧心脏与弹药统一480×152（偏移-40,-20），余弹46号。取消局内重复黑底、木框与托架框，底部短提示使用全宽渐隐底色。
- 新CampaignGameplayHud保留旧ChapterHUD作为不可见的数据源，新建3个可编辑Text呈现。StoryDirector只将第8章炮弹拦截目标改为换行，规则/数值不变。标题/暂停等上一版菜单布局和原按钮动作保留。
- 新Resources/UI/ConstructionCue.prefab与ConstructionCueView，底部中央显示行动、当前键、灰红细线。保留原Tap/KeyPulse与音频，隐藏旧文字重复绘制，只镜像现有文本/脉冲并订阅InputAccepted，接受输入后显示合拍/继续提示，REST沿用原休息秒数。脉冲1.12倍/0.16秒，字号取新预制体，避免旧存档originalPromptSize使新UI缩小。建造阶段外隐藏；场景卸载后source为空则隐藏，重进场景复用并绑定新Tap。未读取Perfect时机或新增判定。
- 解决原Silver缺少「垒」导致异体字形的问题：新增独立Silver-BarricadeUI.ttf，只补简体U+5792，100单位像素网格绘制19轮廓；family独立命名，原Silver.ttf不动，13343原轮廓/13340原Unicode映射及版权许可记录经检查完整保留。GameFont.asset和3个活动UI预制体改用此副本。没有改写章节名字或改用繁体字。
- 心脏仍读取原场景生命/搏动，无重复心跳音频。无拍数/耐久数字，第四章完整三段护送+1容量及隐藏装填效果保留，章节时长/波次/难度/音乐/配置指纹不变，原1/2场景文件哈希一致。
- 87运行时/20编辑器实际源文件对Unity程序集独立编译零错误零警告；590项现有逻辑回归/CSV与2978项资源/预制体/呈现检查通过。字体守恒/校验和、Windows字体加载与离线排版通过。编辑器检查菜单已加分层HUD和建造资源存在性，但尚未在原生Editor执行。此前Editor退出140063无日志问题未解决，本轮未重复失败启动，仍须原生字体/Canvas、动态绑定、暂停/读档/跨章、声画同步与Play验收。
- GAMEPLAY_UI_EDITING.md为最新局内编辑说明；工作区outputs/局内界面统一有14张离线预览（场景背景为共用示意，不代表各章关卡）、修改前备份、编译/回归/资源/字体检查及最终差异哈希。全部工程文件与HANDOVER已写盘。


## 2026-10-22：原版风格心形与边缘配色

- 用户连续明确：不要仿真心脏，参照最早PixedHeart，黑色边缘像素突兀。采用Heart-Classic-v4 / HeartEmpty-Classic-v4，以粗方块、宽厚心形为基准，黑边改为接近阴影的暗红/灰色。解剖、修长印记及双底脚尝试均未接入；未标为用户确认终稿。
- v4两张1254×1254母版保存在Art/UI/Paris1832/Masters，整幅同坐标最近邻采样为16×16，Point/无mipmap/无损。16×16可见轮廓在alpha阈值128时红灰差0像素；alpha并非完全相同，99像素差异/最大11。与最早原图同尺度轮廓差26像素，不宣称逐像素复刻。可见近黑像素（最大RGB<65）为0。原始PixedHeart和GreyPixedHeart未修改。
- Style.asset与AmmoHUD的两层引用改为v4，heartTint/heartEmptyTint为白色。上一中断回合的ParisUIStyle着色字段、AmmoHudView/ParisSceneUI读取及编辑器检查一并保留并完成编译。旧v3原图副本资源留作回退。心跳/音频计时、生命比例、填充方向、HUD位置与弹药规则未改。
- 87运行时/20编辑器源文件对实际Unity程序集独立编译零错误零警告；资源/导入/布局守恒/垂直填充/源图保护/红灰轮廓检查通过。PNG为离线排版/局部放大，不是Unity运行截图；原生Editor此前退出140063无日志问题未解决，尚未Play验证动态填充、搏动及实际渲染。
- PROMPTS_HEART_EDGE.md为实际选用母版指令与导出事实；GAMEPLAY_UI_EDITING.md补充当前引用。outputs/心形边缘柔化包含中断后修改前备份、编译/资源/轮廓报告、14张离线页面预览与心形状态局部对照、最终差异哈希；outputs/原版心形精修为此前中断回合的备份/中间记录，不代表最终资源。全部工程文件及本交接已保存。


## 2026-10-22 过场分镜与主角命名接入

- 从桌面“过场图片”复制34张选用PNG至Assets/Art/Cinematics/Chapter02～Chapter10，SHA256逐张一致；第九章使用街垒修正版，第十章E/F/G使用想象滤镜版。源目录不修改。
- 新增Assets/Resources/Campaign/Cinematics.asset及CampaignCinematics/CampaignCinematicView/CinematicPlaybackState。共14条播放路线，章首、成功章末、选择前及结尾分开；第七章按实际选择互斥播放。
- 第一章四图结束进入第二章A/B/C，默认16秒，B结束后不限时命名。空格跳过前段进入命名，输入时不全局跳过；确认或不命名后继续C。叙事字幕/配音均为空，操作标签可编辑。
- RunState新增PlayerName和ProtagonistIntroduced，使用原存档Codec自动保存恢复；新游戏清空，跨章/重试保留，旧存档缺字段不继承别局姓名。不自动覆盖唯一存档位。
- 名字模板支持{playerName}；命名状态不展示常驻HUD。配音可在配置资产绑定，镜头自动延长容纳配音。当前为叠化/整数逻辑像素横移的静态分镜，尚无角色逐帧行走抬板动画。
- 第二章仍为127拍纯建造，第一章32拍，全战役1830拍；章节表资产未修改，护送全部成功仅+1弹药容量、隐藏装填效果、生命值跨章及当前心形UI均保留。
- 菜单：街垒/战役/编辑过场图片、文案与命名；街垒/战役/检查过场图片与主角命名。使用说明见CINEMATICS_EDITING.md。
- 验证：90运行时+21编辑器源码独立编译0错误0警告；交互/实际存档Codec/路线110项、原规则回归590项、资源接入143项通过。原生Unity启动仍返回140063，无日志；未完成Play模式、输入法和音频验收。
- 输出与修改前备份：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/过场接入/。主角命名_离线预览.png不是Unity运行截图。


## 2026-10-22 操作阶段音乐快速渐弱

- 用户要求正式进入可操作画面后快速、平滑降低音乐，突出心跳。当前默认过场0.65，操作0.16，降低0.45秒，恢复0.6秒。
- 新增Assets/Script/Audio/MusicVolumeEnvelope.cs与CampaignMusicFader.cs。统一管理BGM AudioSource音量，SmoothStep渐变；歌曲进度、心跳/音效/总音量和第九章静音保持。
- StoryDirector公开四个Inspector配置，CampaignRoot.prefab已保存默认值。低音量覆盖建造/修补、防守、选择、尾声、抢救与两拍结束提示。
- CampaignCinematicView不再直接恢复缓存的music.volume，改为提交配音压低请求，消除覆盖新渐弱的冲突。暂停冻结渐变；读档在播放歌曲前按恢复阶段设置音量。
- 验证：92运行时+21编辑器源码独立编译0错误0警告；194项渐变时长/多帧率/中断重定向/暂停/配音/阶段策略检查通过。未完成Unity实际音频试听；此前原生启动140063，本轮未重复启动。
- 参数与试听说明：AUDIO_BALANCE.md。备份和检查报告：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/音乐快速渐弱/。


## 2026-10-22 背景音乐单次播放

- StoryDirector的背景音乐及读档恢复均设置loop=false；同一clip已经播完或主动停止时，后续切章/重试不会从头重播。返回标题并开始新游戏会清空此播放标记，允许新一局重新播放。
- 读取已播完的存档时仍绑定该clip但不播放，防止下一次切章误启动。仅musicPlaying为true且不在第九章时按保存进度续播，仍保持不循环。
- 新增musicPlaybackActive运行时状态区分播放请求与暂停；移除原先尾声只要存在clip就强制保存为正在播放的逻辑，避免尾声读档重新响起已经结束的音乐。
- 先前0.45秒渐弱到0.16的音量控制保留。第九章静音后，第十章不自动重播同一首歌；没有新增结尾歌曲。
- 92运行时+21编辑器源码独立编译0错误0警告；检查当前音乐启动、停止和读档路径均无loop=true。尚未完成Unity音频实测。
- 备份、编译和保存报告：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/音乐单次播放/。


## 2026-10-22 开场黑屏字幕与第一章报幕

- 修复第一章cinematicBeats分支绕过普通entry造成的报幕缺失。顺序为校音→纯黑屏居中字幕5秒→“第一章/当前第一章名字”报幕3秒→原四图32拍。
- Cinematics.asset新增openingSubtitle（空）、openingSubtitleSeconds（5）、firstChapterAnnouncementSeconds（3）、openingAllowSkip（true）。同一过场编辑菜单可修改，字幕完全由用户填写，留空只显示黑屏。
- CampaignCinematicView新增居中纯文字页。隐藏图片、底部渐变遮罩、姓名页和辅助文字，保证背景纯黑；字幕44号、报幕64号并支持自动缩小，沿用Silver字体与现有暖灰文字。
- 第一章报幕直接读取Chapter.title，不硬编码“葬礼”。字幕页与报幕不推进心跳时钟；暂停冻结，空格可逐页跳过且有输入隔离。音乐及BoundaryBeat延后至四图开始，避免在黑屏停留时消耗音乐。
- 原1830拍、四图及后续章节分镜和命名未修改；新增约8秒非拍数时间。未新增配音。
- 92运行时+21编辑器源码独立编译0错误0警告；资源与流程静态接入检查通过，未完成Unity Play验收。使用说明见CINEMATICS_EDITING.md新增章节。
- 输出与备份：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/开场黑屏与第一章报幕/。


## 2026-10-22 第二至十章报幕检查与补齐

- 检查发现：第2～10章均因分镜路线清空原entry文字而缺少独立报幕，局内HUD章名不是章首报幕。现已在首次进入各章时、分镜之前统一显示黑屏居中章号和名字。
- CampaignHudPresentation.Announcement统一第1～10章中文数字章号与当前Chapter.title。没有更改任何章节名字。
- Cinematics.asset新增chapterAnnouncementSeconds=3、chapterAnnouncementAllowSkip=true，可在原过场编辑菜单的Inspector“第二至十章的章首报幕”中改；第一章独立3秒设置保留。
- 重试不显示后续章节报幕；读档不经过BeginChapter，不重复报幕。分镜仍按原路线播放；禁用图片或改用Timeline时也先报幕，备用entry卡移除重复章号标题。
- 报幕事件使用实际RunState.Chapter，修复原先固定第1章的事件参数。报幕冻结心跳并支持暂停/空格逐页跳过。
- 后续九章每章新增3秒，共27秒，分镜时长另计，1830拍预算不变。未修改单次音乐与快速渐弱、命名、护送/弹药和跨章HP规则。
- 92运行时+21编辑器源码独立编译0错误0警告；逐章标题与入口静态检查通过，Unity Play仍未验收。
- 备份/检查输出：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/后续章节报幕检查/。说明见CINEMATICS_EDITING.md新增章节。


## 2026-10-22 战斗密度、画外入场、掷弹兵与心跳修复

- 第3～9章计划士兵/掷弹兵人数由17/11/17/17/12/11/58增至51/59/65/65/55/59/122，同屏上限12/12/12/12/14/14/16。增加多轨pressure-v3小批次，保留原特殊事件与伤害倍率；实际出场还受减兵奖励、同屏节流与末端齐射影响。
- 用户确认“中间”指画面中段，不是中轨。Spawn与到达反推统一使用左侧画外入口，仅第3章第0拍opening允许画内起点。其他advance归零，并在WaveScript校验；三轨仍保留。
- 战役WaveSquad改为每个时点所有存活者齐投，默认三轮，每轮最多3枚、共最多9枚；炸弹飞行3拍。个体EnemyHP独立，杀一人不连带其他人；去掉投弹后整组销毁，幸存者继续行进。
- 章首图片文件/GUID均存在。报幕与分镜拆为独立BlackCardLayer，启动图片时恢复启用、位置及布局。Unity原生检查第2～10章报幕→首图切换均有有效图片、非零尺寸及可见状态。
- 场景1原生确认1个Tap、0个TapSec；场景2反之。Heartbeat战役时直接读CampaignBeatClock，声音预测也读NextWait；修复全局Time.time>5导致新阶段首帧误报，改为累计实际观察时间，暂停/过场/禁用重置。
- 新增enemy_spawn与grenadier_volley日志，供实际出场数和齐投人数调整；常驻UI不增加调试数字。
- 当前Unity窗口已实际完成资源/组件校验，波次通过编辑器内AssetDatabase保存。442项原生编辑器检查通过；1842项规则/小队/时钟回归及590项原奖励/跨章状态回归通过；94运行时+22编辑器编译0错误0警告。仍未完成全战役Play试玩，不能称难度已最终验收。
- 章节名字/1830拍/伤害倍率、跨章HP、第四章3次全成功仅+1容量、隐藏装填效果、单次音乐与快速渐弱保留。测试标签combat-v3-offscreen-volley-20261022，旧存档文件保留但跨配置读档会被拒绝，请开始新游戏。
- 说明：COMBAT_V3_FIXES.md。菜单：街垒/测试/检查战斗密度、画外入场与分镜。备份/原生报告/兵力表：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/战斗密度与入场修复/。



## 2026-10-23 沿用原街垒字标改字

- 使用内置 image_gen 参考原 TitleEpic-v4.png 改成一行《第1832次心跳》，保留浅石色和宋体雕刻像素轮廓。最终透明 PNG 原样保存为 TitleHeartbeat-OriginalStyle.png；未采用此前被用户否定的金属或红白两行方案。
- ParisUIStyle.titleArtwork 接入现有 GameTitleMark；GameTitle 文本作为无字图时的备用，不重复显示。三版封面左/右构图、随机抽图和按钮动作保留。原生保存 Style、开始页预制体和 Title 场景。
- 106 运行时+31 编辑器编译零错误零警告，发布条件编译通过。原生 Play 57 项通过，帧1→49，三版截图均已查看。详见 TITLE_ARTWORK_20261023.md。输出 outputs/原版街垒字标改字；备份 work/title-original/backup。


## 2026-10-23 中法双语卷首语

- 用户选用“我们走进的，是一座浸透曙光的坟墓。”，同时显示法文原文节选及中法书名/作者，注明《悲惨世界》第五部《冉阿让》第一卷第五章。中文为据法文所作译文，法文省略号表示截取原段。
- 通过原生Editor保存 Cinematics.asset，开头纯黑页12秒并保留空格跳过。开头专用Noto Serif SC Bold和分级字号覆盖法文重音；后续章节报幕恢复Silver纯文本，顺序和音乐逻辑保留。
- 106运行时+32编辑器源码编译零错误零警告，发布条件编译通过；原生Play验证双语字形、纯黑、文本区域完整以及卷首语→第一章报幕，已查看实机截图。输出 outputs/双语卷首语，备份 work/opening-epigraph/backup。说明见 CINEMATICS_EDITING.md。


## 2026-10-23 卷首语字体按用户要求统一

- 中法双语卷首语及出处统一改为 GameFont.Silver（游戏内 Silver 像素字体），撤销开头专用 Noto Serif SC。文案、12秒停留和空格跳过保持；根据Silver的实际字面放大为中文72/法文48/出处36，维持阅读尺寸。
- 编译零错误零警告，发布条件编译通过；原生Play确认全部中法字符/重音、完整排版和下一页章首报幕，帧1→10，截图已查看。输出 outputs/双语卷首语。


## 2026-10-23 本机首次进入封面

- 用户葬礼图原样导入 FirstLaunchFuneral.png 并经原生Editor赋值Style.firstLaunchCover，与原三版随机池隔离。本机首次实际ShowTitle显示并立即持久保存已看标记；后续返回和重启只用原三版。首次图右侧排版，不修改原图或游戏流程。
- 106运行时+32编辑器编译零错误零警告，发布条件编译通过。Title存在用户未保存修改，保留现场，不进入Play或保存场景。隔离预制体调用真实ShowTitle/ApplyTitleCover的23项检查通过，测试后偏好标记和脏场景状态均恢复。离屏截图纯黑，已移除，未称本次Play视觉验收完成。详见 FIRST_LAUNCH_COVER_20261023.md；输出 outputs/首次进入封面，备份 work/first-launch-cover/backup。


## 2026-10-23 章节配乐全局修正

- 第一至八章先前统一People Sing属于全局配置，不是控制台独有。现1/2连续People Sing、3/6 The First Attack、4 The Death Of Gavroche、5 Javert At The Barricade、7 Bring Him Home、8 The Final Battle；9静音、双结局既有歌曲保持。新增五首使用用户现有E:/music/悲惨世界录音，保留原件。
- 选曲使用显式目标章节号；空配置停止并清空旧曲，不回退默认曲。读档chapter-N与旧people标识按章恢复，未知标识静音。移除新游戏提前硬编码默认曲调用，修复旧补齐工具再次把3至8章覆盖成People Sing的风险。
- 106运行时+32编辑器源码编译零错误零警告，发布条件编译通过；原生资源保存及隔离CampaignRoot实际方法29项检查通过。当前Title脏场景保留，未Play完整试听。音频映射改变现有配置指纹，旧配置存档按原规则提示新游戏，文件保留。详见CHAPTER_MUSIC_20261023.md；输出 outputs/章节音乐修正，备份 work/chapter-music-fix/backup。


## 2026-10-24 用户选定音效接入

按用户JSON导入40条固定音效，52条不选用。普通命中仍存活手榴弹也播放explosion_02；致命命中只响一次。禁用随机变体和音高抖动，明确关闭未选受击声，接入对象引信、补弹、落地、环境与UI，保留第九章仅心跳。129项原生绑定/实际事件检查通过，源码与发布编译通过；Title未保存编辑已保留，尚未Play混音试听。详见SELECTED_AUDIO_20261024.md。


## 2026-10-24 手榴弹普通命中纠正

普通命中不再扣血，补充固定CC0音效hit_unexploded_01；Perfect可击爆，实际爆炸仅响一次。原选40条加本轮授权1条，共41条。136项原生检查通过，编译无错误警告，保留Title未保存编辑，无Play听感验收。详见GRENADE_HIT_20261024.md。

## 2026-10-24 第二章章首演出（openingSequence）

- 新增 `ChapterDefinition.openingSequence`（类型 `ChapterTransition`），位置在「章首报幕」之后、「`entry` 图片」之前，用于插入一段游戏内演出。
- `StoryDirector.BeginChapter` 的插入点：`cinematicBeats` 分支之后、`Transition(Chapter.entry,...)` 之前。
  ```csharp
  if(!retry && Chapter.openingSequence!=null &&
     (Chapter.openingSequence.cinematicPrefab!=null || Chapter.openingSequence.timeline!=null))
      yield return Transition(Chapter.openingSequence,"chapter-opening",Chapter.openingSequence.caption,number!=9);
  ```
- 行为约定：未配置 prefab/timeline 时整段跳过（不影响现有章节）；`retry` 时不重播，与章首报幕一致；第 9 章仍强制静音。
- 配置方法：`Assets/Resources/Campaign/ChapterTable.asset` → 目标章 → `Opening Sequence` → 指定 prefab。prefab 需挂 `PlayableDirector` 与 Timeline；Timeline 播完时 `Transition` 会收到 `director.stopped` 并继续，无需手写完成调用。若演出结束需要等待逻辑（例如等一句旁白），给 prefab 挂 `CampaignTransitionRelay` 并勾选 `waitForSignal`，由动画事件调用 `CompleteTransition()`。
- 参数建议：`placeholderSeconds=0`、`allowSkip=true`、`waitForSignal=false`、`caption` 留空（避免文字盖住画面）。
- telemetry 记录 key 为 `chapter-opening`；若统计脚本按已知 key 过滤，需要同步加入。
- 验证：工程内 109 个运行时源文件离线编译零错误（`EXITCODE=0`）。Unity 编辑器当时正在运行，需切回编辑器触发重编译后实机确认。
## 2026-10-24 场景3酒馆 UI 动画工作台

- 按用户“使用 UI 来制作”的要求，新增 `Assets/Art/Cinematics/TavernUI` 和 `Assets/Cinematics/TavernUI`。32张酒馆部件、5张人物原图、48张精确提取动作帧；Point/无压缩/无MipMap。没有改写桌面原图。
- 完整 uGUI 预制体 `Assets/Cinematics/TavernUI/Prefabs/TavernUI_AnimationStage.prefab`，ScreenSpaceOverlay/参考320×180，分离墙地、昼夜窗景、家具、人物、前景和门。无SpriteRenderer或游戏控制脚本。
- 25个 UI Image 动画：主角17、昂若拉待机/行走、格朗泰尔待机/行走/喝酒、门关闭/开/关。原有Hero SpriteRenderer clips保留；UI只用新UI_Hero_*。
- `Timelines/TavernUI_Workbench.playable` 已绑定3组人物动作/位移轨道和门轨道；人物默认4秒待机占位，Director不自动播放。没有接入章节流程或修改其他线程新增的openingSequence。
- 已向当前打开的场景3加入完整舞台实例，保留原Main Camera/Canvas/EventSystem/Grid；支持Undo。场景原本dirty，故未保存/关闭，用户检查后Ctrl+S；生成的prefab/clip/controller/Timeline已原生保存。
- Native bridge: `Assets/Script/Editor/Cinematics/TavernScene3Import.cs`，Temp/TavernScene3Import.request 导入（仅新资产路径/场景3，无重复覆盖）；Temp/TavernUIReview.request 只渲染已存预制体的隔离预览。
- 472项原生导入/绑定/真实UI动画采样检查通过，另有可见像素渲染检查。109 runtime +35 editor源文件独立编译零错误零警告；没有进入Play。交付与原3.unity磁盘备份：`%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/场景3_酒馆动画工作台_20261024`。


## 2026-10-24 空白对话 UI

- 新增 Assets/Cinematics/TavernUI/Prefabs/DialogueBlank.prefab，并嵌入酒馆完整预制体和当前场景3实例。Dialogue/Line01/Name 与 Content 留空，沿用 Silver-BarricadeUI；Line01 默认隐藏。
- TavernUI_Workbench.playable 新增“对白_Line01” Activation Track，0–4秒占位，绑定Line01，PostPlaybackState=Inactive。未新增等待输入/逐字播放/剧情内容。
- 保留场景现有未保存修改；用户需Ctrl+S。原生核验空字段、字体、默认隐藏、绑定和根物体保留均通过。109 runtime +36 editor 编译零错误零警告。
- Native editor bridge TavernDialogueSetup.cs，Temp/TavernDialogue.request JSON mode inspect/create + output；create拒绝覆盖已有资产。交付及备份 outputs/空白对话UI_20261024。


## 2026-10-24 酒馆姓名交互接入

- 新增 NameResponseTable.cs、TavernNameDialogue.cs；Data/NameResponses.asset 含两组别名及 default/unnamed 分支，叙事字段全空。UI父Dialogue新增NameEntry/NameReply，保留Line01。
- TavernUI_Workbench 的“姓名输入（Signal）”轨道第1秒 AskPlayerName.signal，经SignalReceiver持久UnityEvent调用AskName；仅Play模式交互。暂停自身Director，确认名字保存RunState原字段，非空回应等待确认后Resume；空回应直接Resume。
- 第二章openingSequence原未配置，现使用酒馆完整prefab，allowSkip=false；旧2章entry的nameAfterShot关闭1处，原图片保留。Chapter fingerprint前后同为e614afd6e70d250219555b0469746a958f62850cb8ed782df04dba83f9a7b04f。
- StoryDirector过场空格跳过增加姓名输入捕获及帧屏蔽判断；CampaignSession的Esc暂停增加新输入法组合保护。没有改Time.timeScale/音乐/存档schema。
- 48项原生隔离验证通过，含实际UI按钮/暂停恢复/同帧防连按/分支/存档字段；111+37源码编译零错误零警告。没有由本任务启动完整章节Play；不覆盖用户当前Play或未保存场景。
- 原生配置桥 TavernNamingSetup.cs（Temp/TavernNamingSetup.request 输出目录），已存在本功能时保留配置值，只复查。交付/备份 outputs/酒馆姓名交互_20261024。场景3仍dirty，请用户Ctrl+S。


## 2026-10-24 昂若拉新待机

- 用户 Downloads/昂若拉idle.png 是256×192、4×3格，实际11帧，第12格全透明。精确键掉#3F0000/#400000两种红底，保留4394个人物像素；原文件保留。
- 原位更新 UI_Enjolras_Idle.anim 为11帧6fps循环1.833秒，GUID保持10c1860184195a54594e0eb5f164ace9，单人及酒馆预制体Image换新首帧。行走不改。
- Unity PPtr帧动画长度包含最后一个采样区间，本轮不补重复首帧端点。
- 58项原生核验通过，含45056像素比较、11次Image采样、循环时长/绑定/命名组件及dirty场景保留。111+38编译零错误零警告。Native EnjolrasIdleSetup.cs，Temp/EnjolrasIdleSetup.request输出目录。
- 输出及备份：outputs/昂若拉待机动画_20261024；场景3仍dirty，请用户Ctrl+S。


## 2026-10-24 昂若拉待机去背补正

用户指出红底残留；确认之前漏掉#3C0202/#440805/#4C0001三种深红，共218像素，位于头顶、外套及帽子边缘。现五色键清理共40880背景像素，余4176人物像素；原始11帧、6fps、脚底及GUID不变。已覆盖原帧PNG并Unity原生重新导入，58项检查通过，浅色底逐帧目视核验通过。前一轮“4394人物像素”包含这218个残留，已纠正使用说明；旧帧备份在输出/红边修正前。


## 2026-10-24 酒馆完整演出

- 用户指定：G喝酒→Hero入门→G拦住切Idle→对白1→姓名回答→按名对白2→Enj走来→对白3/4（均Enj）→三人依次离场。已制作 TavernIntroduction.playable，20.5秒动作时长，保留旧Workbench资产文件。
- 新增TavernDialogueLines.cs及Data/TavernDialogue.asset，First=G、Third/Fourth=Enj，Text全空；第二段继续NameResponses.asset（文件哈希未改）。
- TavernNameDialogue新增对白入口及独立第二段Signal模式，名字确认后可先echo主角名字，再由下一个Signal选姓名回应。源兼容旧模式bool默认false；完整舞台启用两个bool。
- Source完整预制体Director改为新Timeline，当前场景3已绑定；章2原openingSequence仍引用该完整prefab无需换章节表。旧blankLine01不在新Timeline自动显示，仍保留为可编辑UI。
- 3个位移/朝向/CanvasGroup透明度片段，原动作资源复用；开门/关门/尾部淡出；G/Hero/Enj依次在14.8/16.65/18.9前完成离场。
- 78项原生检查通过，112+40源码零错误零警告；Unity逐帧导出164帧，动作MP4在outputs/酒馆完整演出_20261024，不含交互等待和音乐。未启动新Play。原Play按用户明确授权退出；场景3仍dirty请Ctrl+S。
- Native TavernIntroSetup（Temp/TavernIntroSetup.request）及TavernIntroPreview（Temp/TavernIntroPreview.request）输出目录；旧配置桥不要再跑生成。


## 2026-10-24 酒馆门框、新格朗泰尔待机与原站位恢复

- 按用户确认退出 Play。保留场景3未保存修改，不自动保存整个场景；请在 Unity 中 Ctrl+S。
- 恢复编辑站位：主角 (132,-130)，格朗泰尔 (239,-130)，昂若拉 (192,-112)。演出中主角仍从门口入场；两位同伴的开场站位及行走朝向同步修正。
- 格朗泰尔新待机：原图本身包含透明背景；原样提取4×3共12个64×64帧，以6fps循环，时长2秒。更新原 UI_Grantaire_Idle.anim，GUID保留，行走/喝酒不变。
- 门口遮挡改为分层 Canvas。Timeline 经 TavernActorDepth 动画控制人物排序：室内20，门内5；墙8、家具9、门10、前景桌椅30、木柱40、对话100、淡出200。避免直接动画原生 Canvas 排序不生效；对话补独立 GraphicRaycaster。
- 三位角色进出门时跨门槛切换遮挡；路径不改。现有门扇位置及酒瓶等场景修改保留。
- 第三、第四段仍均由昂若拉说，台词、姓名分支内容不改。
- 52项 Unity 原生检查通过；113个运行时文件、42个编辑器文件独立编译零错误零警告。
- 酒馆演出_修正预览.mp4 为20.5秒动作预览，960×540；不包含对白/输入等待与音乐。
- 临时检查结束后退出动画预览，恢复真实编辑站位并记录预制体实例覆盖。旧的 TavernIntroSetup 生成桥不要重跑，会恢复旧站位和旧排序。


## 2026-10-24 特殊姓名 JSON

- 新建 Assets/Cinematics/TavernUI/Data/Resources/SpecialNameResponses.json，含原有 enjolras / grantaire 两组中英文别名，speaker/reply仍空白。用户只需编辑 JSON 即可增删特殊回应；输出副本及说明在 outputs/特殊姓名JSON_20261024。
- NameResponseTable 增加可选 TextAsset specialNamesJson；未指定时自动 Resources.Load<TextAsset>("SpecialNameResponses")，兼容构建，无需更改 .asset 绑定。仅特殊名字从 JSON 来；默认/匿名回应继续使用原资产。JsonUtility读取并缓存原文本，非法格式告警一次并回退原列表。匹配及格式化逻辑保留。
- 113 runtime +42 editor 编译零错误零警告，JSON语法与四个别名唯一性检查通过。Unity进程当前未运行，后台启动退出码140063，未完成实际Unity运行核验；下一次正常打开工程会自动导入。没有更改或保存场景。未保留临时 TavernNameJsonSetup 编辑器桥。


## 2026-10-24 特殊姓名改纯文本与第三段前可选对白

- 用户要求配置更易修改并增加bool控制的新增对白。改为 Data/Resources/SpecialNameResponses.txt，UTF-8 BOM，分组块+中文键，不再在工程保留同名JSON（旧JSON在本次输出备份）。迁移原2组名称/回复，新开关false、文本空白。
- 字段：名称（|分隔）、说话人、回复、插入对话（true/false/是/否/1/0）、插入说话人、插入内容。缩进的行续接上一回复，支持字面量\n与{playerName}。重复名称、未知键、错误bool或缺少名称会报格式警告并回退原资产。
- NameResponseTable.Response 新增 extraBeforeThird/extraSpeaker/extraReply，Resources自动读取同名txt，保留旧显式TextAsset JSON兼容；原默认/匿名回复不改。
- TavernNameDialogue.ThirdDialogue 使用原9.4秒Signal（昂若拉走路7–9秒）先播启用且非空的extraReply；pendingThird排队原第三段，两个确认后再Resume，保持原UI alpha与暂停归属。Hide/Stop/Disable清理队列，同帧输入屏蔽保留。不改Timeline、预制体、场景、角色站位。
- 113 runtime +42 editor编译零错误零警告。29项实际源码独立逻辑检查（UnityAPI替身）通过；没有Unity Play运行核验。输出：outputs/特殊姓名文本配置_20261024，含3份备份、txt副本、检查和使用说明。


## 2026-10-24 酒馆离场顺序更新

昂若拉最后一句确认后，昂若拉先离开，主角随后，格朗泰尔留在原地最后喝一口，再最后离开。

不含玩家对白等待的 Timeline 时间：
- 最后一句 Signal：9.8秒。运行时非空对白会暂停 Timeline，确认后才继续。
- 昂若拉：10.1秒开始走，13.9秒到门口，14.1秒完全隐藏。
- 主角：12.1秒开始走，15.5秒到门口，15.75秒完全隐藏。
- 格朗泰尔：11.7–13.8秒完整喝一次酒；13.8秒开始走，17.2秒到门口，17.4秒完全隐藏。
- 开门：9.9–10.4秒；17.8–18.3秒关门。原入场开门不变，尾部淡出和20.5秒总动作时长保留。

通过原生 API 修改现有 Timeline 与三个位移片段，未改预制体、已有台词、姓名配置及喝酒源动画。门框排序、角色隐藏同步重排。

验证：113 runtime +43 editor编译零错误零警告；24项Unity原生检查通过，含离场顺序、最后一口酒的动画采样/单次循环、三人过门遮挡、隐藏、资源内容及未保存场景保留。
视频为编辑器逐帧导出的动作预览，20.5秒、960×540，不含对白/输入等待及音乐；没有进入Play播放完整章节。

输出：酒馆演出_新离场顺序.mp4。
旧 TavernIntroSetup / TavernRevisionSetup 生成桥不要再执行，会覆盖新离场排布；本次 TavernExitSetup.request 已执行完成，重复只复查。


## 2026-10-24 结局快捷收尾、整轮手榴弹连锁、撞垒虚化、音效平衡与音乐强段

- 结局1：最后一张图进入0.3秒后，可用新按键提前结束剩余镜头/音乐，直接保留末图、虚化并显示“重新开始本章 / 返回标题 / 结束游戏”。早期图片不会跳过；暂停、调试输入与同帧输入保护继续生效。不按键则沿用原流程，等音乐自然结束。
- 手榴弹：每一轮齐射共用独立组号，波次版与旧刷兵版均处理。完美命中引爆一枚会同步引爆同轮其他弹；普通命中仍不扣血；其他轮次不连锁。整轮撞垒只结算一次。
- 撞垒：实际扣血后，主摄像机产生0.32秒虚化并恢复；爆炸强、身体撞击中、子弹轻。叠加有上限；HUD和暂停菜单不参与相机虚化。
- 音效：固定音频和拒绝的静音事件不变，不随机轮换、不随机调音调。总体音效增益0.85，另外压低爆炸、敌枪、引信与环境循环；心跳单独保留原音量。单次声和循环声使用同一份增益。
- 章节3—8：保留原选曲对应，另外截取强段并在章节入口直接从新片段开头播放。章节表共用，因此正常流程与控制台跳章使用同一设置。1、2、9、10章音乐不改。

## 第3—8章音乐截取

| 章节 | 原曲 | 原录音选取范围 | 新片段长度 |
|---|---|---|---:|
| 3、6 | The First Attack | 00:50–01:38 | 48秒 |
| 4 | The Death Of Gavroche | 01:47–02:35 | 48秒 |
| 5 | Javert At The Barricade | 00:50–01:36 | 46秒 |
| 7 | Bring Him Home | 02:12–03:00 | 48秒 |
| 8 | The Final Battle | 00:32–01:20 | 48秒 |

这些录音含剧情配乐，未必有流行歌曲式的固定副歌。片段按波形中持续较强的段落定位，边界取附近能量低点，尚未人工逐句确认副歌位置；附试听供核对。新文件做了40毫秒淡入、2.5秒淡出及响度统一（-19 LUFS目标、-2 dBTP上限），五份PCM音频均没有削波采样。原始录音完整保留在 Assets/Audio/CampaignRecordings。

章节音乐保持单次播放，不改心跳时钟或章节拍数；片段结束后不自动循环。第3和第6章仍共用 The First Attack 的同一片段。

## 后续调音位置

`Assets/Resources/SFX/Selected/MixLevels.json`

master 为音效总体增益（目前0.85）；clips 中每个 gain 为该文件的额外线性增益。0静音，1保持该层原音量，0.5约为-6dB。心跳独立保留，不受这次音效整体压低影响。实际播放还乘以各事件原调用音量和 SfxPlayer.MasterVolume。

主要额外增益：玩家枪声0.9，敌枪0.6，手榴弹爆炸0.6，连锁碎片0.5，引信0.3，炮击0.58，炮弹撞垒0.62，环境循环0.55，UI0.8。原音效文件没有重写。

## 验证与保留

115个运行时、45个编辑器脚本编译零错误零警告。36项原生组件/资源检查和13项Play集成检查通过；Play实际推进158帧，提前结束通过与按键相同的请求API触发，没有注入硬件键盘。检查完已退出Play。场景原有编辑未保存、未覆盖；没有修改敌人伤害值。

编辑器原生桥 GameplayPolishSetup.request 绑定音频及隔离检查，GameplayPolishPlay.request 执行本轮Play集成核验。复查无需重生成音频。


## 2026-10-24 保护期反馈、两秒渐弱和原曲剪辑HTML对照

1. 建造换字母的180毫秒保护期内，旧按键不再无反馈：返回独立 Grace 结果，由 Tap 发出普通敲击音、角色 InputAccepted(false) 动作事件及 GraceInputReceived 事件。现有 ConstructionCue 显示“切换保护 · 按键”、字母短暂金色闪动，并复用 KeyPulse 脉冲。不额外加血、不计分、不消耗当前字母判定，不进入休息；新字母命中会立即覆盖保护提示。普通流程和旧独立建造场景均接入。

2. 进入实际操作或 Timeline / 动画预制体时，音乐从当前音量在2秒内平滑渐弱至原操作音量0.16。保留原过场0.65、恢复渐强0.6秒、配音压低、暂停行为、曲目位置和节拍。CampaignRoot.prefab 的 gameplayMusicFadeSeconds 已原生保存为2；代码默认也为2。新增动画安静状态在过场 finally 清理，防止退出动画后残留。读档采用重定向渐变，不再直接 Snap 降音。

3. 原曲与剪辑版对照试听.html：五首完整原录音、五个当前游戏剪辑；覆盖第3—8章（第3与6章共用）。支持原曲从头、原曲截取处、剪辑从头、按片段内相对时间切换A/B、循环片段及统一试听音量。任一时刻仅播放一个音频。HTML直接引用工程内本机音频，无上传/下载或网络请求；移动工程路径后需重新生成页面。

音乐截取和原始音频本轮均未再修改；页面标明原曲截取范围，原曲保留原响度，剪辑版保留此前响度处理。

验证：115 runtime +47 editor 编译零错误零警告；9项原生配置/判定/包络检查、16项实际Play检查通过。Play验证实际运行了酒馆Timeline、场景1建造提示，并测得动画/操作音量渐变至0.16。按键通过与实际输入相同的判定与通知API触发，未注入硬件按键。HTML十个音频文件路径全部有效，JS语法检查通过；后台浏览器渲染未完成，不宣称做过浏览器播放测试。

没有自动保存场景；Play核验后退出，场景编辑保留。当前章数据、姓名回复、敌人伤害、音乐截取不变。

后续入口：
- Assets/Resources/Campaign/CampaignRoot.prefab → StoryDirector → Gameplay Music Fade Seconds（当前2）
- Assets/Script/Core/ConstructionJudgement.cs：保护期仍0.18秒。
- Assets/Script/UI/ConstructionCueView.cs：保护提示0.35秒。
- Temp/InputMusicReviewSetup.request / InputMusicReviewPlay.request：本轮配置和原生Play检查桥。


## 2026-10-24 撞垒仅虚化敌人自身，移除整屏效果

用户最终明确：仅敌人本体撞到街垒时，对该敌人角色自身虚化；背景、摄像机和UI不虚化。敌弹、手榴弹、炮弹都不触发虚化。

已从 GameManager 所有伤害入口移除摄像机效果。旧 WallImpactBlur 类保留用于已有类型引用兼容，但没有 OnRenderImage，OnEnable 会禁用自身，Trigger/Pulse 均无动作，无法再影响整屏。

WallDamage.ResolveWallHit 只在 EnemyHP 存在的本体分支生成 EnemyWallImpactVisual；手榴弹分支仍提前返回。原敌人立即隐藏并销毁，碰撞伤害仍由 resolved 只结算一次。局部效果只复制当时可见的 SpriteRenderer，使用独立角色材质在0.3秒内模糊、淡出，不包含碰撞体、射击、移动或血量脚本。

为掷弹兵骨骼部件复制独立 Sprite，避免原 rig 清理时使残影丢失；使用 UnitySprites.cginc 保留SpriteRenderer的颜色、透明度和翻转。源角色先隐藏，再销毁，防止其OnDisable恢复基础Sprite而覆盖残影。临时Sprite与Material随效果销毁。

116 runtime +48 editor编译零错误零警告；22项真实Play检查通过，含连续子弹、手榴弹与炮弹不触发效果、士兵与掷弹兵局部虚化、逐像素比较角色范围外画面不变、视觉拷贝与材质清理。截图是同帧相机渲染对照。通过现有碰撞处理API核验，没有注入硬件输入。

已退出Play，原场景未保存或覆盖。旧 GameplayPolishSetup.BlurCheck 已更新为角色着色器与摄像机效果退役检查，不能再恢复旧整屏效果。

本轮原生Play桥：Temp/EnemyBodyImpactPlay.request，内容为输出目录绝对路径。


## 2026-10-24 伽弗洛什出程渐淡与返回局部虚化

- 出程不透明度从100%平滑降至35%，保留可辨识轮廓；拾弹时保持35%。
- 返程前段平滑恢复至100%，回来接触街垒时使用角色局部虚化淡出（0.3秒），不影响背景、UI或摄像机。
- CampaignActor 增加 EscortOpacity / VisualOpacity，GavrocheVisual 每帧按此设置alpha，避免原先强制Color.white覆盖渐变。
- 出发时与街垒的重叠不会结算返回；仅返程（progress>=0.58）接触Wall才成功。原旅程时限结束仍保留成功兜底。ResolveEscort的resolved锁保证奖励只一次，抵达不会扣街垒HP。
- 安全返回复用 EnemyWallImpactVisual 的独立Sprite副本；源角色立即隐藏并销毁。士兵拦截仍走原失败路径，不误播安全返回效果。
- 116 runtime +49 editor编译零错误零警告。17项实际Play检查通过：出程/拾弹/返程alpha、出发街垒重叠不结算、返回成功一次、局部材质与清理、无整屏效果、士兵拦截失败不变。旅程进度与碰撞通过真实组件API加速采样，不是完整时长的人工护送。

输出包含本轮源码备份和原生Play报告。已退出Play，未保存或覆盖原场景。

## 2026-10-24 伽弗洛什透明度方向修正
用户确认：出程先淡后正常，返程先正常后淡。CampaignActor.EscortOpacity 已调整为出程 35%→100%，拾弹保持100%，返程100%→35%；接触街垒后仍仅角色自身局部虚化淡出，背景和UI不受影响。本节替代此前关于相反方向的描述。116 runtime +49 editor 独立编译无错误警告，原生Play18项检查通过（真实组件进度与碰撞API采样）。已退出Play，未保存或覆盖用户场景。


## 2026-10-24 第3至8章已审定配乐映射
|章节|配乐片段|时长|
|---|---|---:|
|3|One Day More · 沙威（v4平滑淡出）|8.21秒|
|4|Paris – Look Down · 伽弗洛什第一段主唱|27.96秒|
|5|Javert At The Barricade · 开头沙威汇报|17.68秒|
|6|Drink With Me · 马吕斯开口前|35.02秒|
|7|Javert At The Barricade · 问话与沙威回应|20.06秒|
|8|The Final Battle · 24秒去人声版|24秒|

音频导入 Assets/Audio/ChapterHighlights；Unity原生AudioImporter保存为Streaming/Vorbis（质量0.85）、保留立体声与采样率。绑定保存到 Assets/Resources/Campaign/ChapterTable.asset 各章 openingMusic；导入的WAV与已审定输出SHA256一致。

保留第1、2、9、10章配乐、已有2秒音量渐变和不循环播放规则。第8章采用原曲去人声版，没有采用后来候选的One Day More革命者合唱。

验证：116 runtime、50 editor源文件独立编译零错误警告；17项Unity原生导入和持久化检查通过；50项实际Play检查通过，涵盖第3—8章普通进入、控制台硬跳章、存档曲目ID与旧people标识解析。测试通过实际组件API加速切章，不代表逐章通关或人工全曲听审。测试后退出Play，未保存或覆盖用户场景。


## 2026-10-24 敌弹基础伤害降为0.5
GameManager.BulletBaseDamage = 0.5f。章节倍率仍生效，敌弹使用小数累计，1倍下两发合计1点。整数街垒HP、UI和存档版本保持兼容；小数余量加入存档，缺少该字段的旧存档按0恢复。章节进入/重试、外部重设HP和满血修复清空余量；普通修复、身体撞垒、手榴弹和炮弹整数伤害不吞掉已有余量。

116 runtime、51 editor源文件独立编译零错误警告；21项原生Play检查通过，包括真实BulletHit碰撞、1/2/2.2/2.5/3倍率、无敌、其他伤害不变、回血、小数存读档及旧字段兼容、章节进入清理。测试以组件API加速执行，未人工完整通关。已退出Play，未保存用户场景。


## 2026-10-24 开场图片后的机制介绍及轻度降难
机制介绍页位于对应章节的 openingSequence、entry 开场图片/演出结束之后，切入操作场景之前。适用第2、3、4、5、6、8、9章。第1、7、10章没有额外说明页；章节重试不重复显示，控制台普通跳章会显示，硬跳防守仍直接进入防守。

使用原有uGUI Canvas、Silver字体与巴黎UI配色。玩家点击“明白了，继续”或按空格/Enter确认；前350ms防误触。阅读期间处于Transition，不推进心跳、敌人或血量；暂停/控制台输入不误触继续。机制页不调用音乐切换或播放函数，保留现有音乐流程。

介绍文案：Assets/Script/Core/ChapterMechanicIntroduction.cs。布局：Assets/Script/UI/MechanicIntroductionView.cs。章节接入：StoryDirector.Mechanics.cs及BeginChapter中entry之后的调用。

上一条降低难度请求也已完成：建造双键容差由120ms放宽到140ms，相关HUD和提示从配置读取。第3至9章伤害倍率以改动前的实际配置为基准乘0.9；当前均由1降至0.9。子弹基础伤害仍0.5，倍率后平均0.45，按原小数累计规则结算。没有改动章节拍数、敌人密度、心跳时间或音乐绑定。第1章和终章是演出，不增加无意义的难度参数。

119 runtime +53 editor源文件独立编译零错误警告；14项原生配置检查及28项Play检查通过。验证了开场图片先于介绍、阅读等待、输入保护、暂停、继续按钮、所有文案/字体、重试跳过、音乐clip不替换。输出1280×720与960×540原生Canvas渲染截图并已查看。测试后退出Play，未保存或覆盖用户场景。


## 2026-10-24 补字与密度降难纠正
- 游戏实际引用的 Silver-BarricadeUI.ttf 补齐 U+62E6“拦”，用同一字体“打”的提手旁与“栏”的右部组合，维持100单位像素网格、1200字宽。原Silver.ttf未动；全部介绍文案汉字已检查，无缺字。
- 第5章改为“弹药管理”，移除辨别目标、是否射击沙威等过时介绍。
- 取消上一轮以战斗伤害降难：第3至9章倍率恢复到那次改动前的实际配置（均1.0）。敌弹基础伤害仍0.5，小数累计保留。双键容差140ms保留。
- 降难改为普通士兵密度90%：每累计10名普通士兵跳过1名；保留每章opening阵型。护送、三人掷弹兵组、诱饵和炮击的脚本事件均不改变，不额外降低伤害或射击频率。密度计数复用已有reducedIndex，随存档保存，不依赖随机概率。参数位于 Assets/Script/Enemies/WaveDirector.cs 的 SoldierDensityPercent。
- 原生40项检查通过：伤害恢复、Unity字形可渲染、每100名普通士兵减少10名、开场不减员等。28项机制介绍Play检查通过，输出新截图并查看第8章“拦”字及第5章文案。119 runtime +54 editor源文件独立编译零错误警告。未运行完整控制台回归或全战役人工通关；控制台旧密度断言已按新规则修正并编译通过。

介绍页仍在开场图片后出现，音乐配置不变。已退出Play，没有保存用户场景。前一轮说明中“降低战斗伤害10%”由本说明取代。


## 2026-10-24 后续章节开场图片贴合配乐，撤下夜袭介绍
按用户纠正，第1、2章保持不变；撤下尚未实装的第6章夜袭机制介绍，第6章开场图片后直接进入准备阶段。

第3—8章的 chapter-entry 图片播放前，读取当前章节音乐的剩余时长，按原有镜头时长比例分配额外展示时间。保留每张图的原有最短时长与配音时长，不缩短已有镜头。报幕是否跳过、前置演出耗时等都由实际音乐游标体现。音乐已经结束或剩余时间更短时，保持原有镜头时长，因此短曲不保证精确同帧结束。

不改变音乐片段、音乐起点、音量渐变或循环设置。不调整第1、2、9、10章、章末图片和结局。空格跳过图片仍生效；机制介绍继续位于图片之后。章节拍数、敌人密度和伤害均未更改。

代码：StoryDirector.OpeningPictures.cs 负责时长分配，StoryDirector.cs 的图片播放分支传入 plannedDurations；原资源 seconds 保留为最短值，不直接覆写。ChapterMechanicIntroduction.cs 移除第6章分支。

120 runtime +55 editor源文件独立编译零错误警告；51项原生Play检查通过，涵盖第3—8章实际播放时长分配、各镜头不缩短、播放结束交接、跳过、音乐引用不变、第6章无介绍以及第1、2章排除。图片进度通过实际CinematicPlaybackState组件加速采样，不代表完整歌曲人工听审。ChapterTable.asset 和 Cinematics.asset 前后SHA256完全一致。已退出Play，未保存用户场景。


## 2026-10-24 敌人密度继续下调至80%
普通士兵密度从原配置90%降到80%，即每累计5名普通士兵跳过1名。开场opening阵型保留；护送、三人掷弹兵组、诱饵和炮击脚本均不改，伤害及章节音乐不变。沿用已有确定性计数及存档规则。120 runtime +55 editor源文件独立编译零错误警告。本次数值调整未重复进行整场Play测试。


## 2026-10-24 游戏图标
已将用户图片原样导入 Assets/UI/GameIcon/GameIcon.png（1254×1254，SHA256与输入一致）。Unity原生TextureImporter按Default、无压缩、关闭mipmap、Point过滤、保留透明度导入。PlayerSettings默认图标及Standalone的1024/512/256/128/64/48/32/16尺寸槽位均已引用该资源，且已核对ProjectSettings.asset磁盘引用。

通过Unity原生PlayerSettings API配置，使用非持久化PlayerSettings快照经Unity原生序列化保存；项目设置差异仅涉及m_BuildTargetIcons。未保存用户场景。120 runtime +56 editor独立编译零错误警告。未重新构建已有exe，新图标在下次构建时生效。


## 2026-10-24 试玩反馈逐项实施
用户确认实施桌面文本全部条目，首条按键建议仍不直接改默认键。普通建造+5；人物/准星换轨平滑移动，原禁射保持；标题和暂停新增uGUI设置，支持键位映射（冲突/保留键保护、游戏提示联动）、分辨率/全屏、动态难度。偏好存本机PlayerPrefs。普通士兵密度简单50%（第9章80%）、普通80%、困难100%，开场阵型和特殊编排保留，战斗伤害不降低。
炸弹实测始终有20点伤害；问题为心脏16x16素材上下透明2像素，600→580原先0像素变化。HeartHealthPresentation映射有效填充区，新增数值血量；BombHP记录撞垒前后血量，同组伤害仍仅一次。
取消最后四拍自动齐射清场/冻结。到时停止新增，等待活敌、护送、敌弹、炸弹、炮弹结算与炮车退场；收尾仍能开火、换轨、补弹并受伤，晚拦截有效，允许额外拍数，存读档保留收尾状态。保留FinaleStarted字段名以兼容存档，但语义已为收尾中；SBullet不再由该字段禁射。
校音加入现有心脏精灵，使用同一音频时钟双脉冲。123 runtime+58 editor独立编译零错误警告；手榴弹8项、综合74项原生Play检查通过，查看1280x720和960x540设置及校音截图。按真实组件API加速验证，未全战役人工通关/硬件键盘模拟/重构建exe，实际分辨率切换需构建版确认。用户偏好恢复、磁盘存档未覆盖，未保存用户场景。说明及备份在 %USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/试玩反馈改进_20261024/实施说明.md。


## 2026-10-25 标题设置重叠与样式修正
根因：StartScreenView.ApplyTitleCover仍按旧三行布局排布退出，覆盖新增设置的位置。新增GameSettingsView.LayoutTitle统一四行（开始/继续/设置/退出），在创建设置和每次封面布局时调用，跟随封面左右列；存档状态下移。设置入口（标题/暂停）及设置页控制按钮复用ParisUI/ParisMenuAccent的字体、轻量背景、红线焦点样式。
123 runtime+59 editor独立编译零错误警告；121项实际Play检查通过，覆盖3封面+首次封面、首次提示、悬停/开关、从防守返回标题、节点唯一、屏幕Rect不重叠和样式统一。已查看左右封面和设置页截图。未覆盖用户场景、磁盘存档，未重新构建exe。输出：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/标题菜单修正_20261025。


## 2026-10-25 建造UI居中与满血提前结束；转场/配文仅建议
用户最终明确：句中“考虑”之前两项直接配置，其后仅提供建议。曾临时撤回两项，用户澄清后已重新落实，最终生效状态如下。
ConstructionCueView.Bind将整体提示置于Canvas中心（anchors x .35-.65,y .415-.585），沿用原尺寸、字体、脉冲；满血隐藏提示。StoryDirector Preparation在满血或原拍数结束时调用原AfterPreparation，满血不再接受额外QTE输入；第3-9章走原prepare-to-defense，第2章走原章末。增加preparation_full日志。原转场时长、图片、音乐、Timeline和出兵编排均未改。
转场及逐图配文为待审建议，尚未实施。已只读核对43条图片配置记录/38张不同图片，提供40条现用候选旁白，3条旧Executed支线标为不启用；提示第6章现图是饮酒休息、希望结局暖色群像应解释为记忆/愿景。输出带图审阅HTML、Markdown和配文JSON于 %USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/建造调整与剧情配文_20261025/。
123 runtime+60 editor独立编译零错误警告；23项实际Play检查通过（居中两分辨率、满血存档、3-9章交接、暂停恢复、实际普通+5填满、第2章纯建造提前结束）。初次测试等待帧数不足，按实际时间等待后通过。未人工全战役通关、未覆盖用户磁盘存档或保存用户场景、未重构建exe。


## 2026-10-25 默认建造到防守短转场
用户明确授权尝试配置此前短转场建议；图片配文仍未授权写入，本次未修改字幕资源。默认AfterPreparation接入新partial StoryDirector.PreparationTransition.cs：0.4秒保留合拍/+5与完成反馈，搬运时Place，切场前RaiseGun；0.25秒淡黑，黑幕加载2，0.35秒淡入；“准备迎战”期间可换轨和瞄准但无敌人且禁射，留足可见一个周期并在实际周期结束后ResetClock/创建waves。第2章仅完工反馈后走原章末。已有prefab/Timeline/waitForSignal配置保留原路径。
为连续心跳，将场景1的实际Heartbeat/HeartFill对象临时挂在StoryDirector下跨LoadScene，再放回场景2心脏父节点，抑制并移除新场景重复心跳，不重建原音频排程。仅过渡期间Gate有效；取消时清理脱离场景的心跳，正常完成后同对象归属场景2随其卸载。音乐游标/clip不重置，转场保持quiet gameplay volume；枪械机构音效固定选择音量0.2，第9章原静音规则保留。暂停/设置/控制台暂停均冻结转场。没有调整两场景相机、街垒朝向或人物站位。
124 runtime+61 editor独立编译零错误警告；25项转场专项+23项满血回归原生Play通过。实际查看带相机画面的完成反馈、就位截图；通过组件API加速，未硬件按键模拟或人工全战役通关，未保存用户场景/覆盖磁盘存档，未重构建exe。报告与备份：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/建造防守转场_20261025/实施说明.md。
