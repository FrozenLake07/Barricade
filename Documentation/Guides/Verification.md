# 验证与维护

## 工程入口

- 菜单“街垒/工程导航/检查工程结构与引用”：检查脚本导入、四个场景和关键预制体的Missing Script，保留当前编辑场景状态；结果写入Logs/ProjectStructure。
- Assets/Script/Editor/Diagnostics/Assets：资源与配置检查。
- Assets/Script/Editor/Diagnostics/PlayMode：实际Play验证。
- PlayMode/Feedback：通用桥接、共享夹具及歌词、结局、准星、受击等分项检查。
- Editor/Tools：生成/配置工具，按Animation、Audio、Cinematics、Art、Campaign分组。

## 反馈验证桥接

编辑器停止Play后，在工程Temp/PlaytestFeedbackPlay.request写入一个UTF-8文本行，现有轮询桥接会进入Play、执行检查并退出。格式为“模式|绝对输出目录”。支持lane、hope、lyrics、marker、hit、persistence；未指定模式时为原有综合反馈检查。

检查会切换运行时场景，编辑场景不保存。输出目录需提前建立。报告记录覆盖范围、帧推进、通过项和错误，截图不替代帧推进检查。部分历史检查对应旧设计，检查失败时先核对其预期是否仍符合当前机制。

## 目录和资源约定

运行时代码放Runtime，UnityEditor工具放Editor。当前未引入asmdef或命名空间迁移，保持存档类型身份。新文件按功能归入现有模块，避免回到UI或Core大目录堆放所有内容。

资源移动通过Unity API保留GUID。生成的Library/Temp/IDE文件不作为源码维护。Legacy代码与历史报告保留用于回查。

本次整理证据见Documentation/Verification/REFACTOR_20261025.md。日常修改选择与改动相关的检查，完整试玩仍需在打包版进行。

新增presentationoptions模式：实际验证声音/字幕偏好、设置内校准、暂停恢复与存档保留。模式入口仍为Temp/PlaytestFeedbackPlay.request，格式presentationoptions|绝对输出目录。测试恢复包含声音/字幕和声音延迟在内的玩家偏好，监听器静音，不写用户存档槽位。设置控件目前位于SettingsPage/ControlsPage与SettingsPage/AudioPage；旧检查中直接查找Difficulty等子对象时需使用ControlsPage前缀。
