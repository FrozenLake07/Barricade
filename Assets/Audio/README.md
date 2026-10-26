# 音频占位符说明

本目录下的 `.wav` 均为**静音占位符**，用于保证工程可导入、可运行、时间轴长度不变。
原始音频（《悲惨世界》音乐剧录音）因版权原因**不纳入仓库**。

## 占位符规格

- 格式：PCM WAV，8 kHz，单声道，8 bit
- 时长：与原文件一致（占位符长度 = 原曲长度）
- 内容：静音

## 如何恢复真实音频

1. 将原始音频放回同名路径（覆盖占位符）
2. 在 Unity 中重新导入
3. 若替换的是 `Hope_MourningAndHope.mp3`，需在 `CampaignRoot.prefab` 的 `finaleMusic` 字段重新指定该音频

## 需要自行提供的文件

| 文件 | 原时长 |
| --- | --- |
| CampaignRecordings/Do You Hear The People Sing.wav | 129.9 s |
| CampaignRecordings/Empty Chairs At Empty Tables.wav | 195.9 s |
| CampaignRecordings/Bring Him Home.wav | 211.7 s |
| CampaignRecordings/Finale.wav | 167.1 s |
| CampaignRecordings/Javert At The Barricade.wav | 103.0 s |
| CampaignRecordings/The Death Of Gavroche.wav | 181.4 s |
| CampaignRecordings/The Final Battle.wav | 148.2 s |
| CampaignRecordings/The First Attack.wav | 162.6 s |
| CampaignRecordings/Hope_MourningAndHope.mp3 | 结局 B 配乐（需自备） |
| ChapterHighlights/CH03…CH08 共 11 段 | 8–48 s |
| SceneMusic/OneDayMore_OpeningToConstruction.wav | 50.9 s |
| SceneMusic/PeopleSing_ConstructionToDefense.wav | 73.1 s |
| SceneMusic/Do You Hear The People Sing.mp3 | 备用（未被引用） |

## 版权

以上曲目均来自音乐剧《悲惨世界》。公开分发前请替换为原创或已授权素材。
