# 常用编辑入口

| 修改内容 | 配置/代码 |
| --- | --- |
| 章节名、拍数、开场音乐、弹药启用 | Assets/Resources/Campaign/ChapterTable.asset |
| A/B波次、敌人编排 | ChapterTable中对应WaveScript引用 |
| 剧情图片、字幕、每图时长、结局分支 | Assets/Resources/Campaign/Cinematics.asset |
| 希望结局音乐及转折时间 | CampaignRoot.prefab的finaleMusic / hopeFinaleCueSeconds |
| 双语歌词与英文人物名 | Assets/Resources/Campaign/MusicLyrics.json |
| 游戏字体 | Assets/Resources/Campaign/GameFont.asset；字体文件位于Assets/UI/Fonts |
| 标题封面、艺术字与界面主题 | Assets/Resources/UI/Paris1832/中的样式资产及Assets/UI/Campaign/StartScreen.prefab |
| 伤害规则 | Assets/Script/Runtime/Combat/Rules/GameManager.cs |
| 玩家开火、目标选择 | Runtime/Combat/Player/SBullet.cs |
| 换轨与瞄准 | Runtime/Combat/Player/CapsuleTrack.cs、TapSec.cs |
| 准星外观 | Runtime/UI/HUD/TargetMarker.cs、TargetMarkerContrast.cs |
| 敌人移动/射击 | Runtime/Combat/Enemies/EnemyMove.cs |
| 受击动画与停步 | Runtime/Animation/Enemies/CampaignReactionVisual.cs |
| 建造输入和宽限判定 | Runtime/Construction/ConstructionJudgement.cs、Runtime/Campaign/Flow/StoryDirector.Construction.cs |

表中的Runtime路径相对于Assets/Script。可用“街垒/工程导航/配置”直接定位常用资产。

歌词时间以对应音频文件的秒数为准，start/end为句子起止，en/zh为英文与中文，speaker使用英文名字。新增文字后执行字幕资源检查，检查同时读取TTF字符映射；动态Font.HasCharacter单独使用可能漏报缺字。

素材在Art/Audio/UI/Cinematics中按用途编辑；Resources中的名称参与运行时加载。移动Unity资源使用AssetDatabase.MoveAsset或Unity Project窗口，保留.meta与GUID。


## 难度与暂停菜单（2026-10-25）

暂停按钮顺序为：继续、存档、读档、设置、退出。运行时菜单位置由 GameSettingsView.LayoutPause 统一设置，主题刷新也会重新应用该位置。

噩梦是第四档难度，偏好值为3；已有简单/普通/困难的值不变。GameDifficultyRules 集中管理规则：取消换字时旧按键的0.18秒宽限；普通建造/修补不回血，Perfect及双键Perfect奖励保留。旧独立建造场景的Tap也应用相同限制。

敌兵密度以困难/原始编排100%为基准，噩梦普通士兵为125%（每4个原始普通士兵名额实际生成5人，开场固定队形保留），默认屏上容量同时乘1.25向上取整。第3—9章各额外生成一队3名掷弹兵；避开护送窗口，受屏上容量、每拍事件数和同轨冷却限制，可顺延。额外小队是否已生成写入WaveDirector.Progress，恢复进度后不会重复追加。第1、2章及结局没有防守波次，不增加小队。战斗伤害不随难度改变。

## 修改希望结局制作人员表

1. 退出Play模式，在Project中选择 Assets/Resources/Campaign/Cinematics.asset。也可使用“街垒 → 工程导航 → 配置 → 剧情分镜与结局”定位。
2. 在Inspector找到“结局二制作人员表”。修改 Credits Text 多行文本，直接回车换行，空行分隔职位、姓名与致谢。名字、顺序和改编说明都在这里修改，无需修改脚本。
3. Credits Seconds 是制作人员表的预留时长，目前18秒；实际显示会覆盖剩余结尾音乐，直到音乐结束。该参数也参与前面图片的时间分配，改动后应检查配乐同步。
4. Ctrl+S保存资产，再播放希望结局检查。制作人员文字显示于虚化的最后一张图片上，使用游戏字体；新增生僻字后检查缺字。


## 2026-10-25 建造预算、校音及标题更新

第3、4、6章建造拍数均为48，与第5章一致。相对原始52/52/54各减4/4/6，合计14拍加到第7章，第7章建造8→22。其他阶段不变，全战役配置1830拍。配置资产与CampaignRevisionRules.PreparationBeats默认值已同步。满血提前结束建造仍有效。

校音心跳：Runtime/UI/Flow/OpeningCalibration.cs 的 HeartbeatVolume=0.85，原为0.55，约提高3.78dB；跟随用户主音量，第二声保持主声55%的相对比例，战斗心跳及背景音乐不变。

标题沿用原艺术字及透明度，通过UI/TitleFrenchTricolor着色，资源位于Assets/Resources/UI/Paris1832/TitleTricolor.shader和TitleTricolor.mat。第：#000091；1832次：#FFFFFF；心跳：#E1000F。色值参考法国政府品牌配色 https://www.info.gouv.fr/marque-de-letat/les-couleurs 。保留浅色浮雕边缘，字形交叠处按原图2172×724坐标划分；更换艺术字时需同步更新分界。StartScreen.prefab和运行时主题均引用材质，原PNG未改变。

第5章章末：移除旧沙威选择分支的独立文字卡（“他没有开枪。这让一些人很不高兴。”／“酒馆里的人，没有再出来。”），直接进入原有结算页面。逻辑在StoryDirector.Combat.cs / CompleteWithExit。其他章节图片过场保持原逻辑。


## 2026-10-25 标题描边与1832拍

标题三色材质增加细描边与轻微浮雕：TitleTricolor.mat 的 Fine outline width（_OutlinePixels）默认3原图像素，Relief depth（_ReliefPixels）默认5原图像素。按标题原图透明轮廓渲染浅灰上左边缘、深色下右阴影，主字面蓝白红保留。没有更换艺术字PNG。

第8章建造58→48，第9章55→48，共17拍转给第7章；第7章22+17+2=41拍。额外2拍使全战役配置1830→1832。ChapterTable.targetBeatBudget、ChapterTable默认值、CampaignRevisionRules.ChapterBeatBudget、PreparationBeats及相关验证均已同步。第3、4、5、6、8、9章建造均48；第7章防守135拍不变。满血仍提前结束建造。


## 2026-10-25 恢复原色标题

标题恢复原始 TitleHeartbeat-OriginalStyle 艺术字，默认UI材质；蓝白红着色、额外描边及浮雕不再使用。StartScreen.prefab与ParisUITheme均已同步。第7章41拍、第8/9章48拍、全战役1832拍保持当前配置。三色材质文件仅留作备选，当前标题没有引用。


## 2026-10-25 建造分段倒计时（方案B）

建造/修补阶段在屏幕顶部中央显示“建造剩余 N 拍”及分段条，每格4拍，余量不足4拍也显示一格。已消耗的格子从右侧依次熄灭，最后8拍使用暗红色。读取StoryDirector.Clock.Completed与Chapter.preparationBeats，不改动游戏时钟、建造判定或耐久。暂停时随原时钟停止；街垒满血提前结束、转场、防守及隐藏HUD时隐藏。

实现：Assets/Script/Runtime/UI/HUD/ConstructionCountdownView.cs，由CampaignGameplayHud.Bind挂接。定位使用现有Canvas的相对锚点，复用GameFont.Silver字体，控件不拦截鼠标输入。每格拍数可修改BeatsPerSegment常量（当前4）。章节时长仍在ChapterTable.asset配置。


## 2026-10-25 沙威章末旧文案确认

用户指定删除的是旧选择分支“他没有开枪。这让一些人很不高兴。”，不是统一的两拍“第5章结束”。普通章末卡片与结算页保留。CompleteWithExit不再包含旧SpySpared叙述分支，第5章不再调用chapter-exit文字过场。已检查Assets内C#、JSON及Unity序列化资产（含Unicode转义），没有该旧文案残留。旧打包版本需要重新构建才能同步代码改动。


## 2026-10-25 街垒布告专用UI

为设置、暂停、恢复倒数、校音、章节EndCard与结算增加统一的印刷布告样式：暖炭灰底、静态低对比度纤维、旧纸色双细框、切角、暗红印刷分隔和PARIS / 1832落款。设置及暂停按钮使用细框和原来的红色焦点标记；暂停面板收拢居中。游戏字体、控制逻辑、章节拍数、伤害和存档格式不变。校音标题移到心脏上方，离开这些界面会恢复原文字RectTransform。剧情的BlackCardLayer（卷首语、报幕、制作人员表）保留原逻辑和纯黑/末图表现；原非三色艺术字标题保留。未重新启用任何已撤下的机制介绍页，仅提供对应样式。

核心：Assets/Script/Runtime/UI/Common/ParisPrintGraphic.cs。kind：0设置、1暂停/倒数面板、2校音/结算、3机制说明、4按钮。颜色Ink/Paper/Line/Red与粒度可在该文件调整。底板使用CanvasRenderer的矢量网格，纹理是固定的稀疏像素细线，不依赖外部贴图，不参与射线检测。几何仅在布局/颜色变化时重建。原有PaperRule/WideRule/RedRule运行时隐藏，避免双重分隔线。

接入：ParisUITheme负责暂停/倒数及按阶段显示的StoryPaper；GameSettingsView负责设置页及新建按钮；ParisMenuAccent保留原交互状态并同步新按钮装饰。所有原有uGUI引用保留，无需迁移预制体。

验证：140个运行时、79个编辑器脚本编译通过，发布条件编译通过；实际Unity Play 639帧、12项检查通过，含已生成的自定义网格、交互、暂停恢复、原色标题和纯黑叙事卡片。已检查1920×1080与960×540截图。用户偏好恢复、未写存档槽位、未重新构建exe。


## 2026-10-26 修正底板边缘与暂停按钮对齐

暂停/恢复倒数面板的印刷底色与纤维仅绘制在纸张范围内，清除PausePaper原图层的底色，取消细框外的深色矩形。StoryPaper的纹理也限制在纸张范围内。继续、存档、读档、设置、退出五个按钮统一横向锚点0.38—0.62、零偏移及0.08高度，垂直间隔一致；GameSettingsView.LayoutPause在重建和主题刷新后应用相同布局。

更新原自包含HTML预览及1920×1080、960×540截图。编译与发布条件编译通过；Unity实际Play 604帧、18项检查通过，覆盖五个按钮的共同边界、透明底板、交互与原有页面切换。无需修改存档或章节配置。


## 2026-10-26 开火与换弹冲突：卡壳

启用弹药系统的战斗中，开火与换弹在同帧产生按下事件，且弹匣仍有至少一发时，优先触发卡壳，本帧不发射子弹、不执行Perfect瞬间换弹。满弹匣也可触发；若普通换弹中仍有弹药，同时按下这两个键同样转为卡壳。空弹匣、尚未启用弹药的章节及无限弹药调试模式不触发。

排障复用换弹状态、动画和进度条：普通装填两拍，卡壳四拍，沿用RunState.ReloadMultiplier。保留弹匣内现有弹药，完成后补满。卡壳期间不能开火，Perfect换弹、旧Perfect子弹命中、重复按键均不能提前解除或重新延长；暂停冻结排障进度。存档仍是v2，CampaignMagazine新增jammed字段随现有嵌套状态自动捕获，读档恢复剩余进度。

反馈：已有weapon_reload_mechanism音效；底部“卡壳！正在排除故障，换弹时间加倍。”；弹药栏显示红色“卡壳 · 排障中”和“换弹时间 ×2”，保持游戏字体（字符映射检查无缺字）。

实现入口：StoryDirector.Combat.cs / CombatInput在普通换弹和开火前读取同帧边沿，调用SBullet.TryJam；CampaignMagazine.Jam管理卡壳状态和四拍计时；CampaignMagazine.Reload与SBullet.OnPerfectHit阻止提前取消；AmmoHudModel/AmmoHudView呈现状态。可在CampaignMagazine.Jam修改四拍基准。

验证：140个运行时、80个编辑器脚本编译无警告，发布条件编译通过。32项检查通过，包含四种换弹倍率的边界、实际SBullet/HUD、完美窗口优先级、延迟Perfect命中、暂停，以及内存中的存档JSON往返和世界恢复。Unity实际Play推进601帧；输入标志直接提供给CombatInput调用的同一TryJam方法，未模拟硬件键盘。未写用户存档槽位、已恢复用户偏好、未重新构建exe。


## 2026-10-26 建造倒计时隐藏拍数

ConstructionCountdownView不再创建或显示剩余拍数文字，保持每格4拍的分段倒计时及末8拍暗红提示。收紧底板高度以去除原文字空白，分段条的屏幕位置和高度保持原值。RemainingBeats等内部计时属性继续使用，暂停、隐藏、满血提前结束逻辑不变。


## 2026-10-26 第九章正常音效与终幕连续炮击

第9章只关闭背景音乐，不再屏蔽SfxPlayer。恢复枪声、命中、爆炸、装填、UI及循环引信音效；Timeline/章节转换的非音乐音频提示也不再因第9章被拒绝。章节音乐继续由PlayChapterMusic/ChapterMusicClip控制，第9章无背景音乐，进入第10章才按原规则立即播放希望结局配乐。

第9章防守配置拍数结束后，由AfterDefense启动FinalArtilleryBarrage，取消直接“守住后进入结局”的路径。火炮不再在第9章章末撤退；结束调度不再等待所有普通敌人与炮车离屏。连续至少4次实际开炮，间隔0.65秒，每发剧情炮弹造成最大耐久25%的撞垒伤害，直到街垒为0；保留末次撞击尾音后进入希望结局。满血默认600→450→300→150→0；低血也保留四次连续炮击演出。

这轮是不可拦截的剧情炮击，前面的普通炮弹仍可正常拦截。剧情弹不参与准星选取，忽略玩家子弹伤害；只有活动中的第9章终幕炮击可以绕过调试无敌，其他伤害和章节不变。提示“火炮集中轰击！街垒即将失守。”，字体映射无缺字。暂停及恢复倒数冻结炮击；炮击演出期间不能保存，避免存入未支持恢复的协程中间态。Debug跳章/清理场景会清除演出活动标记。若前段战斗提前失守，仍沿用原有希望结局入口。

实现：Runtime/Audio/SfxPlayer.cs；Runtime/Campaign/Flow/StoryDirector.FinalBarrage.cs（间隔FinalBarrageInterval、最少4次）；StoryDirector.Combat.cs / AfterDefense；WaveDirector.PrepareFinalBarrageCannon及章末调度；Cannon.FireFinalBarrage；CannonShell.IsFinalBarrage与撞垒结算。原Cannon与CannonShell资源复用，没有手动修改Unity YAML。

验证：141个运行时、81个编辑器脚本编译零警告，发布条件编译通过。实际Unity Play推进1596帧，28项检查通过：7类一次性音效实际AudioSource、循环引信音、非音乐过场提示、普通炮弹拦截、满血/25血两种终幕、实际炮击与命中声音至少4次、无敌调试下仍逐次击破、暂停冻结、希望结局音乐接入，以及第8章撤炮保持原行为。自动验证中监听器静音，未做人耳试听；用户偏好已恢复，未写存档槽位，未重新构建exe。

## 2026-10-26 第1—9章章首语

播放顺序为报幕 → 章首语 → 原图片/章节过场；第10章不播放。章首语使用当前游戏字体、纯黑背景、居中文字，不显示作者或原文出处；第5、7章采用最近确认的替换文字。统一显示5秒，空格可以提前跳过；重试与已有存档恢复不补播。新增页面期间隐藏背景音乐歌词，避免与章首语叠加；未改动音乐播放规则。

编辑文件：E:/Unity Project/My project (1)/Assets/Resources/Campaign/ChapterOpeningWords.txt。UTF-8文本，使用[1]至[9]分节，秒数=5控制各章节持续时间，其余行直接作为正文。读取及校验在Runtime/Campaign/Data/ChapterOpeningWords.cs；播放接入在Runtime/Campaign/Flow/StoryDirector.OpeningWords.cs和StoryDirector.cs。

游戏字体Silver-BarricadeUI.ttf补入“挣、贯”，沿用现有像素字形部件和1200字宽；原有字形坐标、映射和度量均已逐项核对保留，原始Silver.ttf未修改。实际Unity Play通过49项检查，推进1351帧：九章完整配置、报幕后图片前顺序、字体字符覆盖、无歌词叠加、自动5秒结束与提前跳过、重试和第10章排除。143个运行时/82个编辑器脚本及发布条件编译通过，无编译警告。未重新构建exe。

## 2026-10-26 第3—8章章首语后起音乐

第3—8章正常进入时先停止上章音乐，报幕与5秒章首语期间无背景音乐；章首语自动结束或按空格提前跳过后，才调用PlayChapterMusic，从对应曲目的开头进入原图片/过场。BeginChapter使用deferOpeningMusic控制此时机，普通流程和控制台正常跳章共用。重试不播章首语，仍直接启动音乐；控制台直接防守、读档恢复沿用各自原路径。第1、2章连续音乐、第9章无背景音乐和第10章即时结局音乐规则不变。

验证：143个运行时、82个编辑器脚本及发布条件编译均通过；Unity实际Play推进1364帧，66项检查通过，覆盖六章报幕/章首语无音乐、旧曲停止、完整5秒与跳过、图片开始时新曲从头播放及其余章节边界。监听器在自动测试中静音，验证的是AudioSource状态和播放游标，未做人耳试听。未重新构建exe。

## 2026-10-26 机制说明与声音字幕设置

机制介绍现在按当前难度生成第2章建造说明：普通难度保留普通成功增加5点生命及旧字母短暂保护；噩梦明确只有完美建造恢复生命、无换字保护。第4章标题及正文统一使用Gavroche。第5章介绍保持现有弹药说明，不新增卡壳说明。修改入口：Assets/Script/Runtime/Cinematics/ChapterMechanicIntroduction.cs。

标题与暂停共用的设置页增加“操作与难度”和“声音与字幕”标签，沿用街垒布告样式和游戏字体。声音与字幕页可调总音量、音乐、音效、心跳（0—100%），以及字幕大小（100—150%）；显示数值和文字预览，并提供恢复本页默认。设置即时保存到PlayerPrefs，独立于存档槽位，读档不会覆盖。默认沿用原混音：总音量0.8，三个分类音量1，字幕倍率1；不改变章节音乐播放时机或既有两秒压低规则。

音量链路：总音量经AudioListener；音乐偏好在CampaignMusicFader的原阶段包络之后相乘；音效、循环引信、图片配音和非音乐过场提示经EffectsVolumeGain；心跳在HeartbeatAudioOutput中相乘，解除与SfxPlayer.MasterVolume的绑定，恢复倒数也使用独立心跳音量。声音与字幕选项及键名见Runtime/Settings/GamePresentationOptions.cs。

字幕大小作用于图片剧情、章首黑屏文字、双语歌词及酒馆回复；菜单、按键提示和战斗HUD不随之放大。SubtitleSizePreference保存基础字号，避免反复缩放累积；歌词区域随倍率增高。入口：Runtime/UI/Common/SubtitleSizePreference.cs和各Cinematics View。

重新校准在设置内部进行，不改变章节/阶段或重新开局。校准使用原HeartbeatAudioOutput DSP排程；A/D或加减按钮每次调整10ms延迟，左右方向键调整心跳音量，Enter/Esc或“保存并返回”回到设置。校准期间暂停并静音其他声源，单独运行试听；返回恢复原声源与暂停状态，防守阶段仍经过原恢复倒数。GameSettingsView拆分Preferences/Calibration部分文件，原主菜单及暂停按钮位置保持。

验证：148个运行时、83个编辑器脚本及发布条件编译通过；Unity实际Play推进627帧，34项检查通过，含独立音量、当前播放的音效/循环声音、双语字幕放大、校准DSP实际推进与音乐游标不动、暂停恢复倒数、内存存档JSON往返和实际恢复。界面在1920×1080与960×540下检查，新增文字无缺字。自动验证监听器静音，未做人耳混音试听或硬件延迟测量。用户偏好已恢复，未写存档槽位、未保存编辑场景、未重新构建exe。

## 2026-10-26 战斗主角街垒站位

场景2的Capsule/HeroActor位置改为(5.09375,-1.3125,0)，相较原运行时(6,-2,0)向左上调整，落脚点靠近街垒中部木梁与石块。角色尺寸、HeroVisual本地位置、Sorting Order 6、街垒Sorting Order 2与实体碰撞均不改。修改通过Unity EditorSceneManager保存，原场景备份在本次输出目录。

CapsuleTrack.Start仅初始化逻辑攻击轨道，不再把角色Y强制对齐中间轨道。切轨仍只改变准星与目标筛选，S1/S2/S3保持-4.5/-2/0.5。枪口Transform继续随HeroVisual移动，SBullet引用不变。重建主角Animator资源不会覆盖场景根站位。

CampaignSaveWorld恢复世界时，对带HeroCombatDriver的固定战斗主角保留场景中设置的位置，避免旧存档中的(6,-2)覆盖新构图；其他物体位置及主角动画、朝向、弹药、攻击轨道等状态照常恢复。存档格式与配置指纹未改，用户存档槽位未写入。

验证：148个运行时/86个编辑器脚本与发布条件编译通过。Unity实际Play推进44帧，15项检查通过：开场不吸附轨道、上下换轨位置不变、地面/空中瞄准、枪口引用、三阶段街垒、模拟旧站位存档实际恢复、其他敌人位置恢复及第8章重新加载。已查看满/中/低耐久下的实际画面。未重新构建exe。
