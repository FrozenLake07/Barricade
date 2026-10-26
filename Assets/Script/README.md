# 脚本导航

- Runtime：游戏运行时代码，按Campaign、Combat、Construction、Rhythm、Audio、Animation、UI、Persistence等职责分组。
- Editor/Tools：资产配置、动画、音频与剧情制作工具。
- Editor/Diagnostics：资产检查、Play检查与分项验证。
- Editor/Project：工程导航、目录整理和结构检查。

常用入口：Runtime/Campaign/Flow/StoryDirector.cs。主控部分文件按音乐、时钟、建造、战斗、过场、结局、存读档拆分，类名与字段身份不变。

Unity菜单“街垒/工程导航”可以定位场景和配置。完整说明在工程根目录Documentation/README.md。

Resources路径与场景ID保留原值；Runtime/Legacy保存历史兼容实现。本次没有引入新程序集或删除玩法资产。
