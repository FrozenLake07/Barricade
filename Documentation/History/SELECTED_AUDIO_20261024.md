> 手榴弹规则已于2026-10-24再次修正，以GRENADE_HIT_20261024.md为准；下方129项为首次接入时的历史验证。

# 已选音效接入 · 2026-10-24

依据用户导出的《第1832次心跳_我的音效选择.json》配置，40个选用、52个不选用，无待定或备注。

## 已实现

- 导入原选40个WAV及本轮授权补充的1个未爆炸命中声。资源：Assets/Resources/SFX/Selected。Bindings.json是事件到固定音频的唯一映射。
- 每事件一个固定版本；PlayVariants兼容旧调用，但不随机选片、不抖动音高，也不消耗战斗随机数。
- 最新修正：普通命中手榴弹不扣血，固定播放补充的threat_grenade_hit_unexploded_01；Perfect命中保留击爆与一次爆炸声。原选40条另补1条，共41条。详见GRENADE_HIT_20261024.md。
- 未选中的身体受击声保持静音；移除战斗命中借用建造Perfect提示的声音。倒地声对齐倒地动画接近落地的末段。
- 引信跟随每颗存活的飞行炸弹循环，爆炸立即停止；暂停不推进，跳章/返标题/读档清理旧音。
- 第五章起普通补弹依次播放纸料、延后机械层和完成声；第三四章不开启虚假补弹。Perfect补弹采用完成声，保持同一音高。
- 木箱、石街脚步、捡拾、投弹预备/释放、敌军开火、爆炸及火炮使用选定事件。
- 街垒跨2/3、1/3血量阈值分别播放轻裂、重裂，归零播放坍塌；单次大伤害只播放最高严重程度。补血后再次跨阈值可以重新响。
- 当前街垒是一张混合材质图，弹着在碰撞体底部28%以内使用固定石料声，其余使用木料声；不随机混用。
- 炮车只在可见移动/撤退时周期播放选定链条细节，保持原本火炮机制与发射时序。完整开炮声已选，不导入主体/尾音备选层。
- 第一章真正分镜开始时播放车马与低音量远钟；首次静态封面、标题和校音保持静音。
- 第六章操作阶段低音量风声；CH06灯火分镜低音量局部火声，黑屏报幕和分镜结束后关闭。
- 游戏内按钮确认、返回/恢复与无效命名有对应反馈；标题和结局菜单保持既定静音。
- 第九章继续仅心跳，普通音效与循环全部屏蔽。调试sfx命令可用语义事件或旧编号明确试听。
- 署名随构建保存在Assets/StreamingAssets/AudioLicenses/SelectedAudioCredits.txt。

## 验证与限制

原生AudioImporter导入40个资源；129项原生检查通过：固定映射、明确禁用、真实BulletHit非致命/致命手榴弹回调、无重复爆炸、木石分区、血量阈值、第九章静音、标题与结尾UI静音、读档音频计时重基准、RNG不变。
109个运行时和33个Editor源码编译零错误零警告，发布条件编译通过。40个导入WAV与选定处理文件SHA-256一致。
当前Title存在用户未保存编辑，验证使用隔离Editor预览，保留现场；没有运行Play完整试听或验证耳机/音箱混音。暂停循环的实际听感、密集战斗并发与动画落点仍需Play试听。
章节表配置指纹不变，没有修改存档schema、音乐配置、战斗伤害或现有选择结果。

## 40个选定文件

| 事件 | 固定使用的文件 |
|---|---|
| action_build_normal | action_build_normal_01.wav |
| action_build_wrong | action_build_wrong_01.wav |
| action_build_perfect | action_build_perfect_01.wav |
| action_rest_breath | action_rest_breath_01.wav |
| action_aim_adjust | action_aim_adjust_01.wav |
| weapon_blocked | weapon_blocked_01.wav |
| weapon_reload_paper | weapon_reload_paper_01.wav |
| weapon_reload_mechanism | weapon_reload_mechanism_01.wav |
| weapon_no_target | weapon_no_target_02.wav |
| weapon_reload_ready | weapon_reload_ready_02.wav |
| weapon_enemy_musket_fire | weapon_enemy_musket_fire_01.wav |
| weapon_player_musket_fire | weapon_player_musket_fire_03.wav |
| combat_body_fall | combat_body_fall_03.wav |
| threat_grenade_prepare | threat_grenade_prepare_01.wav |
| threat_grenade_throw | threat_grenade_throw_01.wav |
| threat_grenade_fuse_loop | threat_grenade_fuse_loop_01.wav |
| threat_grenade_explode | threat_grenade_explode_02.wav |
| threat_chain_debris | threat_chain_debris_01.wav |
| build_pickup | build_pickup_01.wav |
| build_putdown | build_putdown_03.wav |
| build_drop | build_drop_01.wav |
| footstep_cobblestone | footstep_cobblestone_01.wav |
| character_pickup_ammunition | character_pickup_ammunition_02.wav |
| structure_bullet_hit_wood | structure_bullet_hit_wood_01.wav |
| structure_bullet_hit_stone | structure_bullet_hit_stone_01.wav |
| structure_body_collision | structure_body_collision_01.wav |
| structure_crack_light | structure_crack_light_01.wav |
| structure_collapse | structure_collapse_01.wav |
| structure_crack_heavy | structure_crack_heavy_02.wav |
| artillery_fire | artillery_fire_01.wav |
| artillery_wall_impact | artillery_wall_impact_01.wav |
| artillery_chain_detail | artillery_chain_detail_01.wav |
| artillery_flyby | artillery_flyby_02.wav |
| ambience_wind_loop | ambience_wind_loop_01.wav |
| ambience_fire_detail_loop | ambience_fire_detail_loop_01.wav |
| ambience_carriage_pass_candidate | ambience_carriage_pass_candidate_01.wav |
| ambience_bell_distant_candidate | ambience_bell_distant_candidate_01.wav |
| ui_confirm | ui_confirm_02.wav |
| ui_cancel | ui_cancel_02.wav |
| ui_unavailable | ui_unavailable_02.wav |

原生报告：outputs/音效接入_20261024/音效原生验证.json。修改前源码备份：work/sfx-integration/backup。
