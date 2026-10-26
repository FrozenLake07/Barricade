# 章节配乐修正

先前把第一至八章统一绑定为 People Sing，是全局配置问题。正常 BeginChapter、控制台普通跳章均调用 PlayChapterMusic；hard 跳章也调用同一方法。现按章节内容配置完整录音。

| 章节 | 曲目 |
|---|---|
| 1 葬礼、2 筑垒 | Do You Hear The People Sing，跨章连续 |
| 3 第一次进攻 | The First Attack |
| 4 加弗罗什 | The Death Of Gavroche |
| 5 沙威 | Javert At The Barricade |
| 6 夜袭 | The First Attack |
| 7 冉·阿让 | Bring Him Home |
| 8 火炮 | The Final Battle |
| 9 最后总攻 | 静音，仅心跳 |
| 10 终章 | 哀悼 Empty Chairs At Empty Tables；希望在既有D镜头触发Finale |

五首新增工程录音来自用户已有 E:/music/悲惨世界/025、028、030、031、032.m4a，转换为 PCM 44.1kHz stereo WAV，原件不改动；工程导入 Streaming、Vorbis 0.75。来源和原件哈希见 outputs/章节音乐修正/音频来源.json。

PlayChapterMusic 按显式目标章节号读取表，不依赖跳章前 Chapter 属性。第3至8章章首/重试从头播放对应曲目，不循环。缺少章节配乐时停止旧曲、清空clip并提示日志，不再回退People Sing。第9章同样清空clip；终章继续由独立结局流程控制。新游戏的提前硬编码默认曲调用已移除，交给第一章选曲。

ResolveMusicTrack 对chapter-N只恢复该章配置，旧people标识按当前章配置恢复；未知标识或空配置保持静音。旧 CurrentIssuesSetup 补齐工具不再覆盖第三至八章的人工配置。

106运行时+32编辑器源码编译零错误零警告，发布条件编译通过。原生Editor导入保存并在隔离CampaignRoot上调用实际选曲/恢复方法，29项通过，涵盖八章绑定/恢复、显式目标章节号、旧标识、缺失/未知曲目、第9章静音、双结局身份。当前Title有用户未保存编辑，保留现场，未运行Play完整试听或逐章通关。

音乐属于现有配置指纹的一部分。本次指纹从14f33193655e1e8d1f5b1cabe88e357aeb87c03e57595ea70a52eb6b8d6e4eeb更新为e614afd6e70d250219555b0469746a958f62850cb8ed782df04dba83f9a7b04f。旧配置存档将按既有规则提示开始新游戏，原文件保留，不删除或自动覆盖。

报告：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/章节音乐修正/章节配乐原生验证.json；备份：同工作区 work/chapter-music-fix/backup/。
