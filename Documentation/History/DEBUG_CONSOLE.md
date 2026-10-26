# 街垒 · 调试控制台

2026-10-22。按用户提供的《街垒_调试控制台指令_20261022.md》接入。

## 使用

从 Title 进入 Play，在 Game 视图按键盘 Esc 下方的 **` / ~** 打开或关闭（不需要按 Shift）。Esc 也可以关闭。
编辑器和 Development Build 默认启用；普通发布版只在启动参数带 `-console` 时创建控制台。无需向场景手工挂组件。

半透明面板保留最近20行；鼠标滚轮滚动，“复制输出”复制未加颜色标签的原文。
↑/↓ 浏览最近100条命令并保留正在编辑的草稿，Tab 补全命令名。中文输入法组合文字时，Enter 不会提交命令。
控制台打开及关闭后的保护帧会拦截建造、开火、瞄准、换轨、选择、跳过与暂停输入；底层界面也不会响应鼠标点击。
**打开控制台会暂停游戏、声音和 Timeline，关闭时恢复打开前的时间倍率与声音状态。** 原本已暂停时，关闭控制台仍保持暂停。控制台内可直接 `step 3` 逐帧；`time 0` / `time 1` 设置关闭控制台后的倍率，输入时仍保持暂停。不要用编辑器工具栏 Pause 代替控制台暂停，那会停止整个运行时输入。

命令参数用空格分开，含空格或分号的文本加引号。命令名不区分大小写；参数使用帮助中列出的形式。
批量命令用分号连接，场景加载完成后才执行下一条。输入错误会红字提示帮助并停止当前待执行批次。
单独输入的 `skip/time/beat/help/confirm/cancel` 可以在长过场或等待确认时执行；批量中的这些命令仍遵守原有顺序。

```text
ch 9 hard; hp max 600; hp set 600; beat
time 0
cap 16; kill; spawn soldier 1 2; spawn squad 2 at 100
step 3
time 1
set PlayerName "让 巴蒂斯特"
help spawn
```

## 全部命令

- **`help`**：help [命令] — 查看语法和示例。如 help ch。
- **`ch`**：ch <1—10> [hard] — 正常开章；hard 直接防守。例：ch 9 hard（第二章没有战斗）。
- **`retry`**：retry — 按章节起点重开。例：retry。
- **`next`**：next — 将本章记为调试完成并正常开下一章。例：next。
- **`fail`**：fail — 触发失败分支，第9章进入尾声。例：fail。
- **`phase`**：phase <title/prep/defense/choice/epilogue/results> — 切到本章已配置阶段；重建对应场景。例：ch 7 hard; phase results。当前战役已取消choice阶段。
- **`skip`**：skip — 跳过当前黑屏、整段图片过场、第一章或Timeline；命名段按匿名继续。例：skip。
- **`beat`**：beat — 拍号、阶段、Wait、Window、Period及Perfect剩余。例：beat。
- **`tempo`**：tempo [now] <秒> — 持续覆盖拍前等待；now立即生效。例：tempo now 0.2。窗口另用 window。
- **`window`**：window <秒> — 当前及后续节拍窗口。例：window 0.5。
- **`time`**：time <0—10> — 关闭控制台后的时间倍率；控制台内保持暂停。例：time 0。
- **`step`**：step [1—240] — 控制台暂停中运行指定模拟帧，然后恢复暂停。例：step 3。
- **`freeze`**：freeze — 冻结/解冻敌人，节拍继续。例：freeze。
- **`hp`**：hp [set|max|add 数值] — 查看/设值/上限/加减血；不会自动重开。例：hp max 600; hp set 600。
- **`god`**：god — 开关无敌。例：god。
- **`lastchance`**：lastchance — 防守中触发最后一拍。例：lastchance。
- **`mult`**：mult <0.1—10> — 换弹倍率，仅运行时。例：mult 0.8。
- **`ammo`**：ammo [set 数量] — 查看或设弹药，设置会取消当前换弹。例：ammo set 2。
- **`infinite`**：infinite — 开关无限弹，覆盖第9章默认。例：infinite。
- **`reload`**：reload — 等价R，弹满/无限弹/齐射时不换弹。例：ammo set 0; reload。
- **`aim`**：aim <up/down> — 空中/地面瞄准，保留调整延迟。例：aim up。
- **`lane`**：lane <1—3> — 切到从下至上轨道，保留调整延迟。例：lane 2。
- **`kill`**：kill — 清屏敌人、掷弹兵和弹丸；保留后续波次及护送对象。例：kill。
- **`crit`**：crit — 下一次有目标且实际射出的弹强制Perfect。例：crit。
- **`wave`**：wave — 章/变体/拍号/待生成事件。例：wave。
- **`spawn`**：spawn <soldier/squad/shell/decoy/escort> <1—3> [count | at 到达拍] — 从画外生成；at：士兵到墙/炮弹命中/小队首次齐投命中。例：spawn squad 2 at 40。
- **`density`**：density <0—4> — 缩放尚未执行的普通士兵事件数量，不改护送/诱饵/炮弹。例：density 1.5。
- **`cap`**：cap <1—64> — 临时屏上实体上限，齐次事件仍遵守上限。例：cap 16。
- **`finale`**：finale — 立即开启结尾齐射。例：finale。
- **`clear`**：clear — 需confirm：清世界并重建本章波次；已过期事件可能重新生成。例：clear，然后confirm。
- **`state`**：state [字段] — 查看RunState公共字段；完整内容也写入报告。例：state Decision。
- **`set`**：set <字段> <值> — 仅允许公开标量白名单；Chapter须用ch。例：set Decision 1 / set PlayerName "让 巴蒂斯特"。
- **`save`**：save <slot> — 保存独立调试槽，含调试标记；不覆盖主存档。例：save siege9。
- **`load`**：load <slot> — 需confirm：读独立调试槽（含覆盖值）。例：load siege9，然后confirm。
- **`reset`**：reset — 需confirm：清运行覆盖，回标题并按正常校音流程开新局。例：reset，然后confirm。
- **`confirm`**：confirm — 执行最近一个待确认危险命令，再继续后续批次。例：confirm。
- **`cancel`**：cancel — 取消待确认及尚未开始的批量命令，不中断正在加载的场景。例：cancel。
- **`verify`**：verify [wave] — 编辑器调用原有CampaignVerification/WaveVerification；构建使用共享Validate规则。例：verify wave。
- **`report`**：report — 打开最近报告所在目录。例：report。
- **`assert`**：assert <字段> <== != > >= < <= > <值> — 比较公开RunState或hp/maxhp/ammo/phase/time；文本加引号。例：assert hp == 600。
- **`hud`**：hud <on/off> — 截图隐藏/显示心脏弹药与游戏提示。例：hud off。
- **`debug`**：debug wave <on/off> | debug beat [on/off] — 复用F4；中央节拍信息。例：debug beat。
- **`fps`**：fps — 当前平滑帧率和托管堆占用。例：fps。
- **`music`**：music <title/people/none/off> — 单次播放指定曲；title未配置时明确提示。例：music people。
- **`sfx`**：sfx <id> — 试听已有音效（含第9章调试试听），不循环。例：sfx SFX-QTE-01。
- **`heartbeat`**：heartbeat <on/off> — 仅切心跳声音，不改节拍或心脏动画。例：heartbeat off。
- **`exec`**：exec <脚本文件> — 读取UTF8；每行/分号命令，#注释；仅Scripts目录内，最多256条，禁止嵌套。例：exec "siege9.txt"。

## 与正常规则的边界

- 第2章保持纯建造，`ch 2 hard` 会报错；`phase` 只允许进入本章实际配置的玩法段，不能凭空给第2章添加战斗。
- 从标题 `ch n hard` 跳过建造时，零耐久会补至设计上限，避免下一帧立即失败；因此这是调试运行，会被标记。
- `next` 将本章记为调试完成，然后正常开下一章；`retry` 则走原有重试与奖励回滚规则。
- `skip` 可跳黑屏、图片路线、第一章及Timeline；命名过场跳过后按匿名引入，不填造一个名字。
- `tempo` 是持续覆盖拍前等待，不包含 Perfect 窗口；`tempo 0.2` + 原窗口0.5意味着周期0.7秒。需要一起加速可用 `window 0.1`。回标题/新局清除覆盖。
- `freeze` 是既有敌人冻结规则的手动开关，节拍继续，恢复后仍按当前拍结算。
- `crit` 保留到下一次有目标且可实际发射的射击；不绕过弹药、换弹或瞄准调整。
- `density` 只缩放后续普通士兵，保护剧情护送/诱饵与炮击结构；`cap` 为运行时上限。两者不会写入波次资产。
- `spawn squad 2 at 40` 指首次三人齐投的命中拍；如果来不及从画外行走到投弹位置，会提示延后到达拍。`at` 支持 soldier/squad/shell，不适用于护送/诱饵。
- 手动生成炮弹立即生成实体，方便 `time 0` 观察；正常波次仍由火炮动画出弹。手动刷兵仍从左侧画外进入。
- `kill` 保留后续波次和护送对象；`clear` 需确认，会清世界并重建本章波次，可能重新生成已到期事件。
- `music people` 是显式试听，会从头单次播放；不会把正常游戏改为循环。`music title` 因标题曲按现有设计留空而报未配置。
- `heartbeat off` 只静音心跳声，心脏图形和节拍继续。第9章正常音效静音规则仍保留，`sfx` 可显式单独试听素材。

## 二次确认、脚本、存档和报告

`reset`、`load <slot>`、`clear` 会暂停后续批次，要求另行输入 `confirm`；`cancel` 取消待确认及尚未开始的队列。
不要在脚本里预置 confirm；应在检查提示后手动确认。

Windows当前项目的数据目录（路径会随公司名/产品名改变）：

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\My project (1)\DebugConsole\
  Scripts\       exec 的UTF8脚本，只能访问此目录内文件；不支持嵌套exec
  Saves\         save/load 的独立调试槽，不覆盖 barricade-slot.sav
  RunState.txt    state 全字段快照
  WaveValidation.json / CampaignVerification.json
```

用 `exec "siege9.txt"` 执行 Scripts 内脚本。每行或分号都是一条命令，空行及以 # 开头的行忽略。
一次最多256条、输入16384字符、脚本64KiB，防止误操作卡住控制台。
工程内 `DebugConsoleExamples/siege9.txt` 提供示例，复制到上述 Scripts 后使用。

调试槽保存当前世界、弹匣、RunState、节拍钟及 god/infinite/cap/density/tempo/window 等运行覆盖。`Time.timeScale`、HUD叠加窗口及尚未执行的命令队列不是存档内容。
主存档若在调试运行中保存，同样携带“非设计值”标记，不伪装成正常测试数据。存档仍沿用已有校验和及配置指纹。
回标题/新局清理覆盖，读档恢复该槽自己的覆盖。设计 ChapterTable/WaveScript 不随控制台改动。

每个调试运行的 `Playtests/<run>/run.json` 含 `nonDesign=true`、`debugLabel="非设计值"` 和当前覆盖快照；events.jsonl 记录操作，summary.csv 的 non_design 列明确标记。
“查看试玩统计与对比”会标出并排除这些记录，避免把无敌/加速样本计入正式难度评估。

编辑器中 `verify` 和 `verify wave` 直接调用已有 CampaignVerification.Check / WaveVerification.Check；后者复制报告到 WaveValidation.json 供控制台使用。
构建中使用同一套运行时 Table/Script.Validate 规则，报告明确标记 scope；配置检查不等于完整通关测试。`report` 打开最近报告所在目录。

## 维护和验收

- 注册/帮助/白名单：Assets/Script/Debug/DebugConsole.cs。
- 面板与键盘：DebugConsole.View.cs；纯解析/历史：ConsoleSyntax.cs。
- 运行覆盖：CampaignDebug.cs；流程入口：Core/StoryDirector.Debug.cs；刷怪入口：Enemies/WaveDirector.Debug.cs。
- 不通过反射操作私有字段；set 只允许公开的标量白名单，集合及章节检查点只读。
- 自动原生检查菜单：街垒 → 测试 → 检查调试控制台（Play模式）。先打开并保存Title，它会短暂进入Play再退出，不覆盖未保存场景。
- 自动检查通过后仍需人工确认实体键盘、输入法、历史补全、听感和显示效果；报告不冒称已验证物理键盘输入或全章节通关。

2026-10-22 暂停调整：打开控制台不再计入有效游玩时间，而计入暂停时间；原有倒数、心跳、自动战斗进度及使用 UnscaledGameTime 的 Timeline 均暂停。暂停期间保存不会丢失背景音乐的播放标记。显式 skip、跳章与存读档仍可执行。

