# 街垒 / Barricade

跟着音乐节拍、守住街垒的 2D 节奏防守游戏。灵感来自《悲惨世界》与 1832 年巴黎共和党人起义。

- 引擎：Unity **2022.3.62f3c1**
- 场景：`Assets/Scenes/1.unity`（建造）、`Assets/Scenes/2.unity`（防守）
- 上手与设计说明：见 [HANDOVER.md](HANDOVER.md)

## 打开方式

1. 用 Unity Hub 添加本目录，选择 2022.3.62f3c1 打开。
2. 首次导入会生成 `Library/`（已忽略，不纳入版本库）。
3. 场景 1 可直接运行；Build Settings 已包含场景 1 与 2。

## 音频

音效播放器为 `Assets/Script/SfxPlayer.cs`，从 `Assets/Resources/SFX/` 按编号加载（如 `SFX-PLY-01.wav`），目前仓库内没有音频文件。