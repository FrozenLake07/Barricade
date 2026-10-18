# 《街垒》项目交接文档

> 面向接手的 AI 代理或开发者。读完这一份应该能独立改这个工程。
> 核对于 2026-10-14 · Unity **2022.3.62f3c1** · 23 个脚本 · 2 个场景 · 4 个预制体

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
├─ Script/             全部脚本，扁平存放，无子目录
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

