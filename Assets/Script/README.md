# Script目录使用说明

按功能整理，共47个脚本：39个运行时脚本、8个编辑器脚本。

| 文件夹 | 放什么 | 脚本数 |
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

## 常用修改位置

- 场景1按键与三秒休息：`Gameplay/Construction/Tap.cs`。
- 场景2瞄准/开火：`Gameplay/Combat/TapSec.cs`、`SBullet.cs`。
- 刷敌和敌人AI：`Enemies/Spawn.cs`、`EnemyMove.cs`。
- 心跳和Perfect显示：`Rhythm/Heartbeat.cs`。
- 音乐计时与蓝牙延迟设置：`Rhythm/RhythmManager.cs`。
- 心跳提前排程：`Audio/HeartbeatAudioOutput.cs`、`HeartbeatAudioTimeline.cs`。
- 主角动作：`Animation/Hero/`；敌人动作：`Animation/Enemies/`。
- 动画自动配置：`Editor/Animation/`；核验与诊断：`Editor/Diagnostics/`。

## 后续添加脚本

按用途放到对应目录。运行时脚本放在Editor之外；使用UnityEditor的工具保持在Editor下面。
本次保留全部原脚本文件名、类名与.meta/GUID，只有主角动画配置器的两处固定路径作必要更新，没有新增程序集定义。
Legacy只表示历史实现/占位，不代表可以直接删除；删除前仍应确认未来用途和引用。
Assets/Test1.cs在本次Script目录整理范围之外，保持原位置。

项目待办：游戏开头的调音／声音延迟校准界面尚未实现，见工程根目录HANDOVER.md。
