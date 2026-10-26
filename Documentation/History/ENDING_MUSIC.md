# 双结局与音乐配置 · 2026-10-23

## 2026-10-23现行规则

以下覆盖旧版的“第三至八章不重播”和“183拍尾声”规则：第1/2章People Sing连续，第三至八章在章首播放各自openingMusic，已按章节主题配置，第八章为The Final Battle；缺失配置保持静音，不回退People Sing。完整映射与读档规则见CHAPTER_MUSIC_20261023.md。终章不进入普通场景：结局1末图等待Empty Chairs结束，虚化后三按钮；结局2将D/E/F/G延长至Finale最后18秒，黑屏制作人员表结束后回Title。详细接口、验证限制及名单填写见CURRENT_FIXES_20261023.md。

下面保留2026-10-22实现记录作为历史参考，旧尾声和旧原生Play报告不用于验收现行结局。

## 已接入的播放规则

- 标题与校音静音。校音结束后（无需校音时即点击新游戏后）开始 Do You Hear The People Sing；首张黑屏、第一章报幕和分镜都已在音乐内。
- 第一章进入第二章沿用同一 AudioSource / clip / 播放位置。命名等待不重播音乐；全部音乐 loop=false，自然播放完后保持安静。
- 本次没有替第三至八章另选曲；保留现有同曲单次播放策略，已播完不会因切章重新开始。第九章继续静音。
- 第九章以前最终失败（包含最后一拍补救失败）直接进终章，不显示重试页：结局一 Mourning，播放 Empty Chairs At Empty Tables。
- 第九章失败、或正常守到结束，均进入终章：结局二 Hope。终章报幕和 A/B/C 镜头保持静音；D 镜头（马里于斯转向门外亮光）开始 Finale。
- 哀悼路线只显示 A/B/C 现实画面，末尾停留 C；不使用希望路线 D/E/F/G，不执行希望结局的主角 Timeline。
- 希望路线沿用 A/B/C/D 章首及 E/F/G 结尾。跳过包含希望触发镜头的路线会补发一次触发，防止整段静音；重复 Signal 或切换镜头均不重播。
- 所有章节的原有拍数保持不变：全战役设计预算 1830 拍。终章仍保留原有 183 拍尾声。本次未缩短尾声来适配歌曲长度，音乐自然结束后安静。

## 音频来源

源目录 `E:\music\悲惨世界`，只读，不改动原件。

| 源文件 | 工程显示名 | 时长 |
|---|---|---|
| 023 Do You Hear The People Sing.m4a | Do You Hear The People Sing | 129.89 秒 |
| 036 Empty Chairs At Empty Tables.m4a | Empty Chairs At Empty Tables | 195.88 秒 |
| 042.m4a | Finale | 167.07 秒 |

工程副本：`Assets/Audio/CampaignRecordings/*.wav`，PCM 44.1kHz stereo；Unity 导入为 Vorbis 0.75 / Streaming / preload，避免整段 PCM 解压常驻内存。保留既有过场音量0.65、可操作阶段0.16、0.45秒快速降低音量的设置。

## 修改入口

1. 章节名字：菜单 **街垒 → 战役 → 编辑章节名字**；第10章当前为“终章”，报幕和HUD不重复附上“第十章”。
2. 音乐引用：`Assets/Resources/Campaign/CampaignRoot.prefab` 的 StoryDirector：chapterMusic、mourningMusic、finaleMusic。
3. 分镜及字幕配音：菜单 **街垒 → 战役 → 编辑过场图片、文案与命名**。终章路线的 ending 选 Mourning 或 Hope；None 为通用。各镜头仍可编辑 seconds / subtitle / voiceOver。
4. 希望触发：第10章 Hope / chapter-entry 的第4张镜头勾选 **triggerHopeFinale**。需要移到其他画面时，取消原镜头勾选并勾选新镜头。
5. 若改用 Timeline，在马里于斯重燃希望的时间点发 Signal，SignalReceiver 绑定持久 CampaignRoot / StoryDirector 的 **PlayHopeFinale()**。自定义 Timeline 优先于图片路线。若 Timeline 使用相同路线并跳过，会按该路线配置补发希望触发；若不需要，请移除图片路线里的触发标记。

## 保存与调试

- RunState 保存 Ending、EndingSourceChapter、HopeMusicStarted；存档增加 musicTrack 和 musicTime，按曲目身份恢复，不再把终章音乐误恢复为 People Sing。自然结束的歌曲不复播。
- run.json 增加 ending / endingSourceChapter / hopeMusicStarted；events.jsonl 记录 ending_selected 和 ending_hope_music。
- 配置指纹版本已更新，旧编排存档保留原文件并明确提示开始新游戏。
- 控制台 `ch 3 hard; fail` 或 `ch 8 hard; fail` 检查哀悼路线；`ch 9 hard; fail` 检查希望路线；`ch 9 hard; next` 检查正常结束的希望路线。打开控制台会暂停游戏，可用 `skip` 完成黑屏及过场，关闭后按正常时间推进。
- 编辑器暂停（包括后台回归逐帧）也视为音乐暂停，不再误当作歌曲自然结束。

## 第六章评估与备选（仅建议，未改机制）

当前名“夜袭”，文案“有人把火把扔了过来”。实现是固定0.62夜幕遮罩、普通士兵及三人掷弹兵；掷出的仍是炸弹，没有火把、燃烧、灭火或照明变化。放过第五章间谍会隐性降低该章部分士兵密度。这是玩法设计，不宜直接称为原著情节。

原著确有无声夜袭；马里于斯手持火把威胁引爆火药桶、迫使进攻者退却也是明确情节，但火把由守方使用。音乐剧的夜间休整、Drink With Me、冉阿让祈祷又提供了另一种节奏依据。因此夜战主题成立，“敌军投掷火把”与当前炸弹机制并未对上。

### A. 暗巷夜袭（建议优先，改动较小）

保留战斗强度与夜袭结构，改叙事为逼近街垒的脚步和枪声。保证敌人轮廓清晰，开火时局部闪光，画外入场前给方向音效。将难度放在接敌节奏、优先打掷弹兵和弹药规划，而不是全屏变暗。无需引入新资源条；心跳继续承担节拍和街垒状况提示。

### B. 马里于斯与火药桶（原著贴合度最高，改动较大）

先抵挡短促突袭，护住火药桶并为马里于斯争取接近时间；在指定合拍窗口点燃火把，逼退一波敌军，代价是街垒暴露和后续反扑。应表现为威慑，成功时不实际炸毁街垒。用火把、桶、撤退动作和脚本波次实现一个独立高潮，避免复制第七章道德选择。

### C. 夜间警戒与修补（适合改善全局节奏，改动中等）

夜间围困中安排修补、有限补给与数次短袭；由脚步/口令预警切入战斗。将战斗压力集中到有预警的短时间，保留总拍数，区别于后面的炮击和总攻。若采用，需要重新测每段收益与敌军压力，避免变成无风险补满血。

### D. 守方火光与暴露（明确为改编，改动较大）

火把或路灯由守方掌握：点亮时能辨认优先目标，但引来敌军火力；熄灭后用枪口闪光和声音定位。需要灯光状态、敌人目标权重、动作与视觉反馈，保证黑暗中仍能读取威胁。敌方炸弹保持炸弹外观，不再靠文字把它称为火把。

参考原著：[第四部第十四卷第四章](https://en.wikisource.org/wiki/Les_Mis%C3%A9rables/Volume_4/Book_Fourteenth/Chapter_4)、[第五部第一卷第二十一章](https://en.wikisource.org/wiki/Les_Mis%C3%A9rables/Volume_5/Book_First/Chapter_21)；授权音乐剧情节梗概：[MTI](https://www.mtishows.com/print/node/1293)。音乐剧梗概用于结构参照，不冒充电影逐镜时间顺序。

## 本次验证

- 101个运行时脚本、24个编辑器脚本：Unity2022 DLL独立编译无错误/警告；发布版条件编译通过。
- 原生Play结局回归41项通过，实际帧1→117；覆盖校音前后、第一二章音乐连续、三/八/九章失败、九章正常结束、D镜头触发、重复Signal、跳过、两条结尾分镜、音乐存读档与控制台暂停。
- 既有控制台原生Play回归72项通过，实际帧1→121；包括Unscaled Timeline暂停、逐帧、波次、弹药、存读档及配置检查。
- 战役资源70项检查通过，设计拍数仍1830；源音频SHA256前后相同。
- 回归临时缩短的是运行时分镜配置副本，正式资产仍保留原时长；自动化按API驱动，未替代完整十章手动通关、真实键盘/IME与现场听感验收。
- 测试结束已退出Play回到Title，恢复原runInBackground设置；最终检查时编辑器已退出，资产已保存在磁盘。

