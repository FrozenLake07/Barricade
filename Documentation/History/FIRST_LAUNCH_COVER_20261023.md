# 首次进入封面

用户提供的葬礼图片原样导入 Assets/Resources/UI/Paris1832/Covers/FirstLaunchFuneral.png（1672×941），没有重绘、裁剪或调整图像。Point、无MipMap、Uncompressed、Clamp、Single Sprite，源文件和工程图片 SHA-256 一致：A690A95BED0AA7B497519E78BB08F15C40EAB327E05C4348E01D51DFB0BD4123。

原生 Editor 将图片赋给 ParisUIStyle.firstLaunchCover。它独立于 titleCovers 三图随机池。第一次实际 ShowTitle 时显示它，同时写入并立即保存 PlayerPrefs 的 Barricade.Title.FirstLaunchCoverSeen.v1=1。该记录独立于章节存档；开始新游戏、返回标题、退出重启均不会清除。每个本机用户配置显示一次，清除游戏偏好或换系统用户后视为首次。

单次停留标题界面时保持该图，主题刷新不消耗或提前替换；离开后再显示标题即从既有三图中抽取，排除上一张普通封面。标题、提示和菜单放在右侧，避开左侧前景人物；等比铺满，画面比例不同时允许边缘裁切。主标题字标、Silver正文、按钮行为、音乐和存档标识保持。

验证：106运行时+32编辑器源码编译零错误零警告，发布条件编译通过。Title 场景存在用户未保存修改，因此没有保存该场景或启动Play。隔离预制体检查调用实际运行时 ShowTitle/ApplyTitleCover，检查首次图、持久标记、主题刷新、后续12次不连续随机、销毁重建视图和会话重置后仍不再出现，23项通过。测试后恢复原首次标记，未消耗用户首次体验；原场景和脏标记均保留。

报告与导入原图：%USERPROFILE%/Documents/Codex/2026-10-20/new-chat/outputs/首次进入封面/。修改前备份：同工作区 work/first-launch-cover/backup/。

隔离编辑场景的离屏Canvas截图输出纯黑，未作为视觉验收，已移除该无效截图。没有据此修改游戏渲染方式；当前脏场景保留，因此尚未进行本次实际Play画面验收。
