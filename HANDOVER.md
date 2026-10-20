# 《街垒》项目交接文档

> 面向接手的 AI 代理或开发者。读完这一份应该能独立改这个工程。
> 最近整理于 2026-10-19 · Unity **2022.3.62f3c1** · Assets/Script 内 47 个脚本（39 个运行时、8 个编辑器）

---

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

- 本次以“随音乐速度变化，每拍开放Perfect”作为尝试。原音乐强拍模式36／50处与每侧0.12秒窗口的4份文件已备份到 `C:\Users\ASUS\Documents\Codex\2026-10-14\e-unity-project-my-project-1\outputs\动态BPM试玩版\调整前备份_20261016_123549`，位于Unity工程外。
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

- 来源C:/Users/ASUS/Desktop/杂项/Art的主角、士兵、掷弹兵、手榴弹、子弹PNG，原图字节保持。导入Assets/Charactor/Basic_*.png；Sprite Single、Point、关闭Mip Maps、无压缩、透明背景、Full Rect，关闭自动物理形状。
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
- 备份与导入清单：`C:\Users\ASUS\Documents\Codex\2026-10-14\e-unity-project-my-project-1\work\enemy_animation_import`。新手使用说明在同工作区 `outputs/敌人动画导入说明.md`。

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

- 需求文档：`C:\Users\ASUS\Desktop\杂项\temp\街垒音效资产制作要求_v1.2.docx`。56 条，编号体系为 `HRT / SYS / QTE / BLD / PLY / ENM / BMB / WAL / ART / VOX / AMB / END`，含技术规范、验收清单、代码接入状态、资源预算和实施顺序。
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
- 本轮安装备份：C:\Users\ASUS\Documents\Codex\2026-10-14\e-unity-project-my-project-1\outputs\主角动画导入_16帧与音效检查\Backup-20261018-115953-504；原生资源备份：E:\Unity Project\My project (1)\AnimatorBackups\Hero-20261018-120255-466。


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
- 本轮安装前备份：C:\Users\ASUS\Documents\Codex\2026-10-14\e-unity-project-my-project-1\outputs\心跳声音画同步\Backup-20261018-232912-207。


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
- 修改前场景与HANDOVER、配置说明、离线核验与美术叠层预览：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/第二幕场景配置。
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
- 说明、素材预览、原始来源清单、离线报告、修改前SBullet/BulletHit/HANDOVER备份：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/火炮预制体。工具与行为检查源码在work/cannon。

## 2026-10-19 火炮接入第二幕实际刷出流程

- 用户要求“把火炮写入项目”，本轮将既有独立Cannon.prefab接入现有Spawn运行流程，覆盖上节“未自动刷出”的状态。没有增加场景物体或修改场景文件，场景2现有三个Spawn通过新增字段默认值启用此分支。
- 默认enableCannon=true，cannonUnlockTime=1680秒，按策划第28分钟部署。三个Spawn共用cannonDeployed标记，随现有场景局部计时重置；只部署一门，不受普通兵概率、数量上限及Leader锁影响。场景中已有启用火炮则视为已部署，不叠加。只在场景名2运行。
- 从启用Spawn中按y排序选择中间轨道，脚底偏移-0.59375。若本场景存在正交Camera.main，初始x至少为镜头左边缘向内1单位；保持原炮车部署1.5单位、启用后30秒首发和每30秒炮击规则。Wall碰撞体由中轨Spawn.target或同场景Wall查询获得后注入。
- Spawn组件右键新增“立即部署终局火炮（运行中，全场一门）”，跳过阶段等待和自动开关但仍防重复，只对场景2生效。生成后可在Cannon组件右键“立即试射（运行中）”快速测试。正式解锁时间可改三个Spawn的Cannon Unlock Time，保持数值一致。不要Reset整个Spawn组件，以免覆盖原刷怪调优。
- 缺少Cannon资源或Wall时全场只警告一次，不占部署名额；Wall之后出现可恢复部署。Resources预制体按enemy/Cannon名字加载，无LoadAll下标依赖。
- 仅修改Assets/Script/Enemies/Spawn.cs，原meta/GUID不变，场景、两份Cannon prefab、动画、音频及其他现有玩法代码未改。
- 全部41个Script运行时源文件使用Unity2022引用集独立编译通过；15项实际Spawn源码部署检查通过，Unity接口使用替身，不等同于原生实机通过。本轮未运行Unity原生编辑器检查，既有编辑器启动/审批限制仍未解除。
- 使用说明及修改前Spawn/HANDOVER备份：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/火炮接入第二幕；实际源码行为检查位于work/cannon_integration。

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
- 使用说明、离线结果与修改前文件：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/火炮与街垒修正；脚本与逻辑检查位于work/cannon_wall_fix。
- 部署时间仍用三个Spawn的Cannon Unlock Time调整。30秒炮击、25%最大血量伤害、拦截规则和音乐/心跳保持。

## 2026-10-19 街垒X轴水平镜像

- 按用户要求将场景2的Act2Environment/Barricade沿X轴翻转，localScale=(-1,1,1)。源图有大片左侧透明留白，因此以24点实体碰撞轮廓的水平中心为镜像轴，Transform.x由0补偿到8.59723698，使主体整体仍留在画面右侧、左右边界约2.051至6.546保持。
- 使用Transform负X缩放同时镜像SpriteRenderer与PolygonCollider2D，没有仅翻图而漏翻判定。Wall标签、三个Spawn.target引用、街垒Y/Z及原PNG/meta/GUID保持。
- Scene2EnvironmentSetup同步主体中心镜像逻辑，重新配置街景不会恢复原朝向；CannonPrefabVerification新增X镜像状态检查。两份编辑器工具按Unity2022引用集独立编译通过，离线核对镜像前后主体水平边界保持；本轮未做原生实机画面/碰撞核验。
- 修改前场景/工具/HANDOVER及边界核对结果：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/街垒水平翻转。

## 2026-10-20 第二幕新版像素建筑、街垒与路面接入

- 用户确认采用新版建筑/街垒，并修改新版石板路。Assets/Environment/Act2/Buildings.png、Barricade.png已原位替换为用户提供的新版PNG，图片字节保持；原meta/GUID保留。Cobblestone.png使用内置image_gen编辑新版路面：石块缩小、黑缝/亮边减弱、上密下疏的轻微透视，输出1672x941不透明PNG。
- 场景2中Buildings上提52/94.1世界单位，使建筑街边高度接近原版。新版街垒更高，改为等比0.85并保留X轴镜像，localScale=(-.85,.85,.85)，position约(8.16471838,-.828108395,0)，脚底落在原地面附近，主体中心保持画面右侧。历史上一节的scale=-1和x=8.597已被本节覆盖。
- 24点PolygonCollider2D已按新版木石主体重配，避开旗帜；碰撞随Transform等比缩放和镜像。三个Spawn.target仍指向Barricade，Wall标签唯一、旧Wall继续停用。y=-4.5/-2/.5三轨均与新主体相交。
- Scene2EnvironmentSetup同步新轮廓、建筑上提与街垒缩放/地面锚点；CannonPrefabVerification更新新版镜像比例检查。两份编辑器工具通过Unity2022引用集独立编译。
- 运行时玩法、火炮、刷怪、音乐与心跳脚本全部未改，所有原meta/GUID保持；765个其他既有资源/配置文件哈希保持。新图原位替换继续沿用既有Sprite引用。
- 本轮未完成Unity原生导入、实机画面或物理运行核验。输出预览为按素材/场景坐标离线重建，不含角色/UI，不应当作实机截图。原生资源与街垒检查入口仍是“街垒/检查火炮预制体”。
- 修改前素材、场景、工具和HANDOVER备份、最终路面PNG、布局预览、生成提示词及离线报告：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/第二幕像素美术更新；工作文件在work/art_refresh。路面使用内置image_gen编辑模式，未使用API/CLI回退。


## 2026-10-20 十章战役、过场接口与黑屏开始界面

- 按用户提供《街垒_剧情与关卡设计_20261020.md》配置10章，每章183拍，开场/结尾各1拍。用户明确选择保留现有0.5秒Perfect窗口；周期仍为等待+窗口，章节时长长于文档估算。音乐默认不参与判定。
- 新增Title.unity为Build Settings第一场景，黑屏任意键开始；UI为Assets/UI/Campaign/StartScreen.prefab，Silver中文字体。运行从Title开始；直开旧1/2仍保留调试流程。
- 新增Resources/Campaign/ChapterTable.asset（10章/214项波次）和CampaignRoot.prefab。StoryDirector持久化管理准备/防守/选择/抢救/结算/尾声，复用旧场景而非复制10份场景。进入战役时关闭旧100秒准备、随机刷怪和音乐判定驱动；旧说明中的28分钟火炮/30秒自动炮击只适用于非战役调试。
- 第4章护送、第5章间谍、第6章夜袭、第7章选择及后果、第8章火炮40拍部署与70/100/130/160拍开炮、第9章3弹无换弹/仅心跳/失败进10、第10章渐慢心跳和回忆文字已接入。护送采用现有无武器小型角色替身，专用儿童素材待补。
- 新增CampaignBeatClock、RunState、CampaignWaves、CampaignActor、CampaignProjectile；现有脚本接入3弹弹匣、按拍换弹、连击、三种建造资源、章节耐久600、一次最后一拍抢救及章末统计。战役不再被旧WallCharge/ConstructionStageFlow初始化覆盖。
- ChapterTransition为开场/结尾/每章entry/prepareToDefense/exit预留cinematicPrefab、Timeline、AudioClip、时长、跳过和完成信号。CampaignTransitionRelay可由动画事件或Signal调用完成/音效/字幕；尚无正式过场时显示文字占位。现有结尾预制体已绑定。
- 运行时50份与编辑器11份源码使用Unity2022引用集独立编译通过；实际时钟/章节表/状态源码7595项断言通过；321个序列化引用、214个波次和1830+2拍核对通过。既有场景1/2、纹理/音频/字体和原meta哈希保持。尚未做Unity原生导入与完整战役试玩。
- Unity核验菜单：街垒/战役/检查十章配置与开始界面；报告Library/CampaignVerification.json。实际音效仍以心跳为主，夜袭引信等需补音频。长按/切分尚未实现，本轮使用单键、休止、双键。开场音频延迟仍待实际设备校准，现有补偿保留。
- 完整使用/插槽说明、章节JSON、离线核验、开始界面离线布局预览、设计原件及修改前Script/ProjectSettings/HANDOVER备份：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/十章关卡与开始界面。工作脚本在work/campaign，部分为一次性补丁，请勿直接重复运行。


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
- 输出：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/波次配置更新，包含实施/验收说明、原始问题逐项状态、数据JSON、离线引用/末端报告、修改前Script/Campaign/HANDOVER/桌面文档备份。工作脚本在work/waves，多数安装脚本为一次性，请勿重复运行。

- 最终补充：为保持第8章“至少拦截2炮”目标，A/B资产requiredShellInterceptions=2，HUD显示实际击落进度；抢救不会绕过目标，齐射不计入；未达成时收场前本章失败。其他章目标0。


## 2026-10-20 街垒生命三阶段

- 用户图一/图二分别导入Assets/Environment/Act2/Barricade_Stage1.png、Barricade_Stage2.png，PNG字节原样，原Barricade.png保留为第三阶段。新增BarricadeHealthVisual并绑定场景2 Act2Environment/Barricade，按WallHP/WallMaxHP即时切换：<33%图一，>=33%且<66%图二，>=66%原图。600上限阈值198/396，整数比较避免float边界误差。
- 两张新图继承原街垒负X缩放-.85，Renderer.flipX保持false，镜像一次。自定义pivot将可见主体中心与原图脚底对齐；第二图1671宽也已补偿。不改原图、Transform、24点PolygonCollider、Wall标签或Spawn目标。
- Scene2EnvironmentSetup同步新增素材与组件配置；重新配置街景也保留此功能。编辑状态满血图，运行时实时响应伤害/回血/抢救。
- 说明、图片哈希/锚点、核验及修改前备份：C:/Users/ASUS/Documents/Codex/2026-10-19/e-unity-project-my-project-1/outputs/街垒生命三阶段。独立源码编译/阈值检查通过，未做Unity原生画面试玩。


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
