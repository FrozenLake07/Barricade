#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 只生成原生动画资源、配置预制体；不在编辑器里执行掷弹兵的运行时网格代码。
[InitializeOnLoad]
public static class EnemyAnimatorSetup
{
    public const int Version = 2;
    public const string Folder = "Assets/Resources/EnemyAnimations/Animator";
    public const string LibraryPath = Folder + "/EnemyAnimationLibrary.asset";
    const string AttemptedKey = "StreetBarricade.EnemyAnimatorSetup.Attempted.v3";
    static readonly string[] PrefabPaths = {
        "Assets/Resources/enemy/Soldier.prefab", "Assets/Resources/enemy/Leader.prefab"
    };
    static bool building;
    static int checks;

    [Serializable] sealed class Report
    {
        public bool success;
        public int version, checks;
        public string utc, error, backup;
    }

    static EnemyAnimatorSetup()
    {
        if (!Application.isBatchMode) EditorApplication.delayCall += AutoSetup;
        // 动画配置仅在编辑状态处理，不拦截任何场景的播放。

    }

    static void AutoSetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoSetup;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || Installed()
            || SessionState.GetBool(AttemptedKey, false)) return;
        SessionState.SetBool(AttemptedKey, true);
        Build(false);
    }

    static bool Installed()
    {
        var library = AssetDatabase.LoadAssetAtPath<EnemyAnimationLibrary>(LibraryPath);
        if (library == null || library.formatVersion != Version || library.soldierController == null
            || library.grenadierController == null || library.alignedSoldierWalk == null
            || library.alignedSoldierWalk.Length != 8 || library.alignedSoldierWalk.Any(s => s == null)) return false;
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[i]);
            Animator animator = prefab != null ? prefab.GetComponent<Animator>() : null;
            if (animator == null || prefab.GetComponent<EnemyVisualAnimation>() == null
                || animator.runtimeAnimatorController != (i == 0 ? library.soldierController : library.grenadierController))
                return false;
        }
        return true;
    }

    [MenuItem("街垒/动画/生成或检查敌人Animator")]
    public static void GenerateOrValidate() => Build(false);

    [MenuItem("街垒/动画/重建生成的敌人Animator（覆盖生成内容）")]
    public static void Rebuild() => Build(true);

    public static bool Build(bool rebuild)
    {
        if (building || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[EnemyAnimatorSetup] 请停止运行后再生成动画。");
            return false;
        }
        building = true;
        checks = 0;
        var report = new Report { version = Version, utc = DateTime.UtcNow.ToString("O") };
        try
        {
            Sprite[] walk = LoadFrames("Walk"), fire = LoadFrames("Fire");
            var text = Resources.Load<TextAsset>("EnemyAnimations/GrenadierRig");
            Require(text != null, "掷弹兵动作数据缺失");
            var definition = JsonUtility.FromJson<GrenadierOriginalRig.Definition>(text.text);
            Require(definition != null && definition.ppu > 0f && definition.width > 0 && definition.height > 0
                && definition.parts != null && definition.parts.Length == 7, "掷弹兵部件数据无效");
            foreach (string name in new[] { "Stand", "Walk", "Fire", "Throw" })
            {
                var motion = definition.motions != null ? Array.Find(definition.motions, m => m.name == name) : null;
                Require(motion != null && motion.fps > 0 && motion.frames != null && motion.frames.Length > 0
                    && motion.frames.All(frame => frame != null), name + " 动作数据无效");
                float expectedFps = name == "Stand" ? 1f : name == "Walk" ? 8f : name == "Fire" ? 12f : 10f;
                Require(motion.frames.Length == (name == "Stand" ? 1 : 8) && Mathf.Abs(motion.fps - expectedFps) < .0001f,
                    name + " 帧数或帧率与当前战斗时机不匹配");
            }
            foreach (string path in PrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null && prefab.GetComponent<EnemyMove>() != null
                    && prefab.GetComponent<SpriteRenderer>() != null, path + " 缺少角色组件");
            }
            var library = AssetDatabase.LoadAssetAtPath<EnemyAnimationLibrary>(LibraryPath);
            bool needsGeneration = rebuild || library == null || library.formatVersion != Version
                || library.soldierController == null || library.grenadierController == null
                || library.alignedSoldierWalk == null || library.alignedSoldierWalk.Length != 8
                || library.alignedSoldierWalk.Any(sprite => sprite == null);
            if (!needsGeneration && Installed())
            {
                ValidateGenerated(library, definition);
                report.success = true;
                Debug.Log("[EnemyAnimatorSetup] Animator检查通过，可以直接运行场景2。");
                return true;
            }
            // 备份放在 Assets 之外；重建也能找回旧的 controller、动画和预制体。
            report.backup = BackupGeneratedAndPrefabs();
            EnsureFolder(Folder);
            EnsureFolder(Folder + "/Clips");
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<EnemyAnimationLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            if (needsGeneration)
            {
                Sprite[] oldSprites = library.alignedSoldierWalk;
                library.alignedSoldierWalk = CreateAlignedWalk(library, walk);
                AnimationClip idleClip = SpriteClip("Soldier_Idle", new[] { library.alignedSoldierWalk[0] }, 1f, true);
                AnimationClip walkClip = SpriteClip("Soldier_Walk", library.alignedSoldierWalk, 8f, true);
                AnimationClip fireClip = SpriteClip("Soldier_Fire", fire, 12f, false);
                library.soldierController = CreateController("Soldier", idleClip, walkClip, fireClip, null);
                AnimationClip idleRig = RigClip(definition, "Stand", "Grenadier_Idle", true);
                AnimationClip walkRig = RigClip(definition, "Walk", "Grenadier_Walk", true);
                AnimationClip fireRig = RigClip(definition, "Fire", "Grenadier_Fire", false);
                AnimationClip throwRig = RigClip(definition, "Throw", "Grenadier_Throw", false);
                library.grenadierController = CreateController("Grenadier", idleRig, walkRig, fireRig, throwRig);
                if (oldSprites != null)
                    foreach (Sprite sprite in oldSprites)
                        if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite, true);
                library.formatVersion = Version;
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }
            ConfigurePrefab(PrefabPaths[0], library.soldierController);
            ConfigurePrefab(PrefabPaths[1], library.grenadierController);
            AssetDatabase.SaveAssets();
            ValidateGenerated(library, definition);
            Require(Installed(), "预制体引用没有保存成功");
            report.success = true;
            SessionState.SetBool(AttemptedKey, true);
            Debug.Log("[EnemyAnimatorSetup] 已生成士兵和掷弹兵Animator，检查通过。资源：" + Folder + "；备份：" + report.backup);
            return true;
        }
        catch (Exception error)
        {
            report.error = error.ToString();
            Debug.LogError("[EnemyAnimatorSetup] 生成失败，请查看完整错误。旧资源备份：" + report.backup + "\n" + error);
            return false;
        }
        finally
        {
            report.checks = checks;
            try
            {
                string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "EnemyAnimatorSetupResult.json");
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            catch (Exception error) { Debug.LogWarning("[EnemyAnimatorSetup] 无法保存检查报告：" + error.Message); }
            building = false;
        }
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++;
    }

    static Sprite[] LoadFrames(string action)
    {
        string path = "Assets/Resources/EnemyAnimations/Soldier_" + action + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Require(importer != null && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
            && importer.textureCompression == TextureImporterCompression.Uncompressed, action + " 像素图导入设置无效");
        var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        Require(frames.Length == 8, action + " 必须包含8帧");
        return frames;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string BackupGeneratedAndPrefabs()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string target = Path.Combine(root, "AnimatorBackups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(target);
        foreach (string path in PrefabPaths.SelectMany(s => new[] { s, s + ".meta" }))
        {
            string output = Path.Combine(target, path);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.Copy(Path.Combine(root, path), output, true);
        }
        string generated = Path.Combine(root, Folder);
        if (Directory.Exists(generated))
            foreach (string source in Directory.GetFiles(generated, "*", SearchOption.AllDirectories))
            {
                string relative = source.Substring(root.Length + 1);
                string output = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.Copy(source, output, true);
            }
        return target;
    }

    static Sprite[] CreateAlignedWalk(EnemyAnimationLibrary library, Sprite[] source)
    {
        var result = new Sprite[source.Length];
        float anchor = source[0].pivot.x / source[0].pixelsPerUnit;
        for (int i = 0; i < source.Length; i++)
        {
            Sprite frame = source[i];
            float pivotX = anchor * frame.pixelsPerUnit / frame.rect.width;
            Require(pivotX >= 0f && pivotX <= 1f, "行走帧水平锚点超出图片");
            result[i] = Sprite.Create(frame.texture, frame.rect,
                new Vector2(pivotX, frame.pivot.y / frame.rect.height),
                frame.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            result[i].name = frame.name + "_WalkAligned";
            AssetDatabase.AddObjectToAsset(result[i], library);
        }
        return result;
    }

    static AnimationClip NewClip(string name, float fps, bool loop)
    {
        var clip = new AnimationClip { name = name, frameRate = fps, legacy = false };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    static AnimationClip SaveClip(AnimationClip clip)
    {
        string path = Folder + "/Clips/" + clip.name + ".anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        EditorUtility.CopySerialized(clip, existing);
        UnityEngine.Object.DestroyImmediate(clip);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static AnimationClip SpriteClip(string name, Sprite[] frames, float fps, bool loop)
    {
        var clip = NewClip(name, fps, loop);
        // 只写实际帧。以前末尾多补一个首帧键，Unity 会把片段算成 (帧数+1)/fps，
        // 循环时就多停一帧：士兵行走 1.125 秒里有 0.125 秒冻在第一帧。
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        FloatCurve(clip, "", typeof(SpriteRenderer), "m_Enabled", frames.Select(f => 1f).ToArray(), fps, loop);
        FinishClip(clip, frames.Length, fps, loop);
        return SaveClip(clip);
    }

    // 片段长度显式钉在“帧数 / fps”，不交给 Unity 按最后一个键推导。
    static void FinishClip(AnimationClip clip, int frames, float fps, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0f;
        settings.stopTime = frames / fps;
        settings.loopTime = loop;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    static void FloatCurve(AnimationClip clip, string path, Type type, string property, float[] values, float fps, bool loop)
    {
        var keys = new Keyframe[values.Length];
        for (int i = 0; i < values.Length; i++) keys[i] = new Keyframe(i / fps, values[i]);
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    static AnimationClip RigClip(GrenadierOriginalRig.Definition data, string motionName, string clipName, bool loop)
    {
        var motion = Array.Find(data.motions, m => m.name == motionName);
        var clip = NewClip(clipName, motion.fps, loop);
        foreach (var part in data.parts)
        {
            var poses = motion.frames.Select(frame =>
                frame.parts != null ? Array.Find(frame.parts, item => item.name == part.name) : null).ToArray();
            string path = "Rig_" + part.name;
            Vector3[] positions = poses.Select(p => Position(data, part.px + (p != null ? p.x : 0f),
                part.py + (p != null ? p.y : 0f))).ToArray();
            SetPositions(clip, path, positions, motion.fps, loop);
            Quaternion[] rotations = poses.Select(p => Quaternion.Euler(0f, 0f, p != null ? -p.angle : 0f)).ToArray();
            FloatCurve(clip, path, typeof(Transform), "m_LocalRotation.x", rotations.Select(q => q.x).ToArray(), motion.fps, loop);
            FloatCurve(clip, path, typeof(Transform), "m_LocalRotation.y", rotations.Select(q => q.y).ToArray(), motion.fps, loop);
            FloatCurve(clip, path, typeof(Transform), "m_LocalRotation.z", rotations.Select(q => q.z).ToArray(), motion.fps, loop);
            FloatCurve(clip, path, typeof(Transform), "m_LocalRotation.w", rotations.Select(q => q.w).ToArray(), motion.fps, loop);
        }
        SetPositions(clip, "Rig_HeldGrenade", motion.frames.Select(f => Position(data, f.grenadeX, f.grenadeY)).ToArray(), motion.fps, loop);
        FloatCurve(clip, "Rig_HeldGrenade", typeof(SpriteRenderer), "m_Enabled",
            motion.frames.Select(f => f.grenade ? 1f : 0f).ToArray(), motion.fps, loop);
        foreach (string name in new[] { "Rig_MuzzleFlash", "Rig_MuzzleFlashCore" })
        {
            SetPositions(clip, name, motion.frames.Select(f => Position(data, f.flashX, f.flashY)).ToArray(), motion.fps, loop);
            FloatCurve(clip, name, typeof(SpriteRenderer), "m_Enabled",
                motion.frames.Select(f => f.flash ? 1f : 0f).ToArray(), motion.fps, loop);
        }
        FloatCurve(clip, "", typeof(SpriteRenderer), "m_Enabled", motion.frames.Select(f => 0f).ToArray(), motion.fps, loop);
        FinishClip(clip, motion.frames.Length, motion.fps, loop);
        return SaveClip(clip);
    }

    static Vector3 Position(GrenadierOriginalRig.Definition data, float x, float y) =>
        EnemyAnimationLibrary.GrenadierOffset + new Vector3((x - data.width * .5f) / data.ppu,
            (data.height * .5f - y) / data.ppu, 0f);

    static void SetPositions(AnimationClip clip, string path, Vector3[] values, float fps, bool loop)
    {
        FloatCurve(clip, path, typeof(Transform), "m_LocalPosition.x", values.Select(v => v.x).ToArray(), fps, loop);
        FloatCurve(clip, path, typeof(Transform), "m_LocalPosition.y", values.Select(v => v.y).ToArray(), fps, loop);
        FloatCurve(clip, path, typeof(Transform), "m_LocalPosition.z", values.Select(v => v.z).ToArray(), fps, loop);
    }

    static AnimatorController CreateController(string name, AnimationClip idle, AnimationClip walk, AnimationClip fire, AnimationClip grenade)
    {
        string path = Folder + "/" + name + ".controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        Require(controller != null, "无法创建AnimatorController：" + path);
        var machine = controller.layers[0].stateMachine;
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var state in machine.states) machine.RemoveState(state.state);
        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        if (grenade != null) controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
        AnimatorState idleState = State(machine, "Idle", idle, new Vector3(260, 60));
        AnimatorState walkState = State(machine, "Walk", walk, new Vector3(520, 60));
        AnimatorState fireState = State(machine, "Fire", fire, new Vector3(260, 220));
        machine.defaultState = idleState;
        MoveTransition(idleState, walkState, true, false);
        MoveTransition(walkState, idleState, false, false);
        MoveTransition(fireState, walkState, true, true);
        MoveTransition(fireState, idleState, false, true);
        if (grenade != null)
        {
            AnimatorState throwState = State(machine, "Throw", grenade, new Vector3(520, 220));
            TriggerTransition(machine, throwState, "Throw");
            MoveTransition(throwState, walkState, true, true);
            MoveTransition(throwState, idleState, false, true);
        }
        TriggerTransition(machine, fireState, "Fire");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = false;
        state.speed = 1f;
        return state;
    }

    static void TriggerTransition(AnimatorStateMachine machine, AnimatorState state, string trigger)
    {
        var transition = machine.AddAnyStateTransition(state);
        TransitionDefaults(transition);
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    static void MoveTransition(AnimatorState from, AnimatorState to, bool moving, bool waitForEnd)
    {
        var transition = from.AddTransition(to);
        TransitionDefaults(transition);
        transition.hasExitTime = waitForEnd;
        transition.exitTime = 1f;
        transition.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Moving");
    }

    static void TransitionDefaults(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.None;
    }

    // Unity 的缺失组件可能是托管引用非空的空壳，必须用 Unity 的 == null 判断。
    static T EnsureComponent<T>(GameObject root) where T : Component
    {
        if (root == null) throw new InvalidOperationException("敌人物体不存在");
        T component = root.GetComponent<T>();
        if (component == null) component = root.AddComponent<T>();
        if (component == null) throw new InvalidOperationException("无法添加组件：" + typeof(T).Name);
        return component;
    }

    static void ConfigurePrefab(string path, RuntimeAnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Animator animator = EnsureComponent<Animator>(root);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            EnsureComponent<EnemyVisualAnimation>(root);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Require(saved != null, "无法保存敌人预制体：" + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ValidateGenerated(EnemyAnimationLibrary library, GrenadierOriginalRig.Definition data)
    {
        Require(library.alignedSoldierWalk != null && library.alignedSoldierWalk.Length == 8, "对齐后的行走帧缺失");
        float anchor = library.alignedSoldierWalk[0].pivot.x / library.alignedSoldierWalk[0].pixelsPerUnit;
        foreach (var sprite in library.alignedSoldierWalk)
            Require(sprite != null && AssetDatabase.Contains(sprite)
                && Mathf.Abs(sprite.pivot.x / sprite.pixelsPerUnit - anchor) < .0001f, "行走精灵没有持久保存或锚点不一致");
        foreach (RuntimeAnimatorController runtime in new[] { library.soldierController, library.grenadierController })
        {
            var controller = runtime as AnimatorController;
            bool grenadier = runtime == library.grenadierController;
            Require(controller != null && controller.layers.Length == 1, "AnimatorController缺失");
            var machine = controller.layers[0].stateMachine;
            Require(machine.defaultState != null && machine.defaultState.name == "Idle", "默认状态应为Idle");
            Require(machine.states.Length == (grenadier ? 4 : 3), "动画状态数量不正确");
            foreach (var child in machine.states)
            {
                var clip = child.state.motion as AnimationClip;
                Require(clip != null && !clip.legacy && !child.state.writeDefaultValues, "动画片段类型或Write Defaults配置无效");
                Require(clip.events.Length == 0, "动画不应直接生成子弹或手榴弹");
                Require(!AnimationUtility.GetCurveBindings(clip).Any(b => b.path == "" && b.type == typeof(Transform)), "动画不能改变敌人根位移");
                bool loop = child.state.name == "Idle" || child.state.name == "Walk";
                Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == loop, "动画循环设置不正确");
                float expected = child.state.name == "Fire" ? 8f / 12f : child.state.name == "Throw" ? 8f / 10f : 1f;
                Require(Mathf.Abs(clip.length - expected) < .0001f, "动画时长与攻击时机不匹配：" + clip.name);
                foreach (var transition in child.state.transitions)
                    Require(transition.duration == 0f
                        && (loop || transition.hasExitTime && Mathf.Abs(transition.exitTime - 1f) < .0001f), "动作切换配置无效");
            }
            foreach (var transition in machine.anyStateTransitions)
                Require(!transition.canTransitionToSelf && !transition.hasExitTime && transition.duration == 0f, "攻击触发配置无效");
        }
        var fireClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Clips/Grenadier_Fire.anim");
        var flash = AnimationUtility.GetEditorCurve(fireClip,
            EditorCurveBinding.FloatCurve("Rig_MuzzleFlash", typeof(SpriteRenderer), "m_Enabled"));
        Require(flash != null && flash.Evaluate(EnemyVisualAnimation.FireReleaseDelay + .001f) == 1f,
            "枪口闪光与0.25秒发射时机不匹配");
        var throwClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Clips/Grenadier_Throw.anim");
        var enabled = AnimationUtility.GetEditorCurve(throwClip,
            EditorCurveBinding.FloatCurve("Rig_HeldGrenade", typeof(SpriteRenderer), "m_Enabled"));
        Require(enabled != null && enabled.Evaluate(EnemyVisualAnimation.ThrowReleaseDelay - .001f) == 1f
            && enabled.Evaluate(EnemyVisualAnimation.ThrowReleaseDelay + .001f) == 0f, "投弹离手帧与0.4秒战斗时机不匹配");
    }
}
#endif
