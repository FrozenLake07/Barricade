# 目录地图

```text
Assets/
  Script/
    Runtime/             游戏运行时代码
      Campaign/          生命周期、章节流程、会话和配置类型
      Combat/            玩家、敌人、波次、弹道和街垒规则
      Construction/      建造QTE与判定
      Rhythm/            节奏与拍点
      Audio/             音乐、心跳、音效与歌词数据
      Animation/         主角、敌人和友军的动画驱动
      UI/                公共样式、菜单、HUD、过场和操作界面
      Persistence/       存档、运行状态与世界恢复
      Settings/          游戏选项
      Telemetry/         试玩统计和配置指纹
      DebugConsole/      游戏内调试控制台
      Common/            共享小组件
      Cinematics/        分镜状态和时长规则
      Legacy/            保留的历史兼容实现
    Editor/
      Tools/             资产配置、导入与编辑工具
      Diagnostics/       资产验证、Play验证与分项检查
      Project/           工程导航与目录迁移工具
  Art/
    Characters/          人物美术
    SharedSprites/       原Charactor中的基础精灵
    Environment/         街景、地面、街垒
    Cinematics/          剧情分镜原图
    UI/                  界面美术
  Audio/
    CampaignRecordings/  章节/结局录音
    ChapterHighlights/   已选章节唱段与伴奏
    SceneMusic/          独立场景使用的音乐资源
  UI/
    Campaign/            开始页预制体
    Fonts/               Silver、游戏补字版与标题字体
    Scene2Crosshairs/    准星状态图片
  Cinematics/TavernUI/   酒馆场景、人物、Timeline、动画和对白资产
  Resources/             运行时按名称加载的资产，路径保持原样
  Scenes/                原场景文件名保持原样
  StreamingAssets/       随构建分发的数据
Documentation/           当前指南、架构和历史记录
DebugConsoleExamples/    调试命令示例
```

`Library`、`Temp`、`Logs`、`obj`、`.vs`、生成的 `.csproj/.sln` 属于Unity/IDE工作文件，不是游戏内容目录。`AnimatorBackups` 保留工具生成的动画备份。

Resources、场景文件名、类型全名和默认程序集没有改名，保持现有运行时加载、序列化和存档兼容。Legacy保留供历史兼容，不作为新增功能的默认位置。
