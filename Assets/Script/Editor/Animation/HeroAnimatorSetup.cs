#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Unity 2022 native API generator. No YAML/meta hand editing and no Play-mode gate.
[InitializeOnLoad]
public static class HeroAnimatorSetup
{
    public const int Version = 4;
    public const string Folder = "Assets/Resources/HeroAnimations";
    public const string LibraryPath = Folder + "/HeroAnimationLibrary.asset";
    const string ControllerPath = Folder + "/Animator/Hero.controller";
    const string HeroPrefabPath = Folder + "/Prefabs/Hero.prefab";
    const string EndingPrefabPath = Folder + "/Prefabs/HeroEndingSequence.prefab";
    const string TransitionPath = Folder + "/Timelines/ConstructionToDefense.playable";
    const string EndingPath = Folder + "/Timelines/HeroEnding.playable";
    const string AttemptKey = "StreetBarricade.HeroAnimatorSetup.v4";
    static bool building;

    [Serializable] sealed class Manifest
    {
        public ActionData[] actions = null;
        public float walking_cycle_distance = .875f, carrying_cycle_distance = .75f;
    }
    [Serializable] sealed class ActionData
    {
        public string name = null;
        public int frame_count = 0;
        public float fps = 0f;
        public bool loop = false;
        public string[] files = null;
    }
    [Serializable] sealed class Report
    {
        public bool success;
        public int version = Version, actions, frames;
        public string utc = DateTime.UtcNow.ToString("O"), error, backup;
        public string[] scenes;
    }

    static HeroAnimatorSetup()
    {
        if (!Application.isBatchMode) EditorApplication.delayCall += AutoSetup;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += AutoSetup;
        };
    }

    static void AutoSetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoSetup;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || Installed()
            || SessionState.GetBool(AttemptKey, false)) return;
        SessionState.SetBool(AttemptKey, true);
        Build(false);
    }

    [MenuItem("街垒/动画/生成或检查主角Animator")]
    public static void GenerateOrCheck() => Build(false);

    [MenuItem("街垒/动画/重建主角生成资源（先备份）")]
    public static void Rebuild() => Build(true);

    public static void BuildBatch()
    {
        if (!Build(false)) throw new InvalidOperationException("Hero Animator setup failed; see Library/HeroAnimatorSetupResult.json");
    }

    public static void BuildAndVerifyBatch()
    {
        BuildBatch();
        HeroAnimatorVerification.Check();
        string path = "Library/HeroAnimatorVerification.json";
        Require(File.Exists(path) && JsonUtility.FromJson<Report>(File.ReadAllText(path)).success,
            "Hero playback verification failed; see " + path);
    }

    static bool Installed()
    {
        HeroAnimationLibrary library = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath);
        return library != null && library.formatVersion == Version && CompleteBindings(library);
    }

    static bool CompleteBindings(HeroAnimationLibrary library)
    {
        if (library == null || library.controller == null
            || library.actions == null || library.actions.Length != 17
            || library.actions.Any(e => e == null || e.clip == null || e.firstFrame == null)) return false;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(EndingPrefabPath) == null) return false;
        string constructionGuid = AssetDatabase.AssetPathToGUID("Assets/Script/Animation/Hero/HeroConstructionDriver.cs");
        string combatGuid = AssetDatabase.AssetPathToGUID("Assets/Script/Animation/Hero/HeroCombatDriver.cs");
        return !string.IsNullOrEmpty(constructionGuid) && !string.IsNullOrEmpty(combatGuid)
            && File.ReadAllText("Assets/Scenes/1.unity").Contains(constructionGuid)
            && File.ReadAllText("Assets/Scenes/2.unity").Contains(combatGuid);
    }

    public static bool Build(bool rebuild)
    {
        if (building || EditorApplication.isPlayingOrWillChangePlaymode) return false;
        building = true;
        var report = new Report();
        try
        {
            Manifest manifest = LoadManifest();
            report.actions = manifest.actions.Length;
            report.frames = manifest.actions.Sum(a => a.frame_count);
            if (!rebuild && Installed())
            {
                Validate(AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath), manifest);
                report.success = true;
                return true;
            }
            HeroAnimationLibrary existing = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath);
            if (!rebuild && existing != null && existing.formatVersion >= 3 && CompleteBindings(existing))
            {
                // 只更新本轮四组片段；保留现有状态机、Timeline、场景和自定义参数。
                report.backup = Backup();
                foreach (ActionData action in manifest.actions)
                {
                    if (action.name != "Idle" && action.name != "IdleLeft"
                        && action.name != "Walk" && action.name != "CarryWalk") continue;
                    Sprite[] frames = ImportFrames(action);
                    HeroAnimationLibrary.Entry entry = existing.Find((HeroMotion)Enum.Parse(typeof(HeroMotion), action.name));
                    Require(entry != null, "Missing existing action: " + action.name);
                    entry.clip = MakeClip(action, frames);
                    entry.firstFrame = frames[0]; entry.fps = action.fps;
                    entry.duration = action.frame_count / action.fps; entry.loop = action.loop;
                }
                existing.formatVersion = Version;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Validate(existing, manifest);
                report.scenes = new[] { "1", "2" };
                report.success = true;
                Debug.Log("[HeroAnimatorSetup] 行走16帧、搬运16帧、两组呼吸已更新，保留现有状态机与场景绑定。备份：" + report.backup);
                return true;
            }
            report.backup = Backup();
            EnsureFolder(Folder + "/Clips"); EnsureFolder(Folder + "/Animator");
            EnsureFolder(Folder + "/Prefabs"); EnsureFolder(Folder + "/Timelines");
            var entries = new List<HeroAnimationLibrary.Entry>();
            foreach (ActionData action in manifest.actions)
            {
                Sprite[] frames = ImportFrames(action);
                entries.Add(new HeroAnimationLibrary.Entry
                {
                    motion = (HeroMotion)Enum.Parse(typeof(HeroMotion), action.name),
                    clip = MakeClip(action, frames), firstFrame = frames[0], fps = action.fps,
                    duration = action.frame_count / action.fps, loop = action.loop
                });
            }
            AnimatorController controller = MakeController(entries);
            HeroAnimationLibrary library = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<HeroAnimationLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.controller = controller;
            library.actions = entries.ToArray();
            library.formatVersion = Version;
            library.walkingCycleDistance = manifest.walking_cycle_distance;
            library.carryingCycleDistance = manifest.carrying_cycle_distance;
            // Anchors are measured from the approved 64x64 aim frames, at PPU32.
            library.groundMuzzle = new Vector3(-.734375f, .296875f, 0f);
            library.upMuzzle = new Vector3(-.640625f, .703125f, 0f);
            EditorUtility.SetDirty(library);
            TimelineAsset transition = MakeTimeline(TransitionPath, library, false);
            TimelineAsset ending = MakeTimeline(EndingPath, library, true);
            MakePrefabs(library, ending);
            AssetDatabase.SaveAssets();
            ConfigureConstruction(library, transition);
            ConfigureDefense(library);
            AssetDatabase.SaveAssets();
            Validate(library, manifest);
            Require(Installed(), "Scene bindings were not saved");
            report.scenes = new[] { "1", "2" };
            report.success = true;
            Debug.Log("[HeroAnimatorSetup] 17组主角动作、状态机、场景1/2和两套Timeline已配置完成。备份：" + report.backup);
            return true;
        }
        catch (Exception error)
        {
            report.error = error.ToString();
            Debug.LogError("[HeroAnimatorSetup] 配置失败，请查看完整错误和备份：" + report.backup + "\n" + error);
            return false;
        }
        finally
        {
            building = false;
            try { File.WriteAllText("Library/HeroAnimatorSetupResult.json", JsonUtility.ToJson(report, true)); }
            catch (Exception error) { Debug.LogWarning("[HeroAnimatorSetup] 报告保存失败：" + error.Message); }
        }
    }

    static Manifest LoadManifest()
    {
        string file = Folder + "/Manifest.json";
        Require(File.Exists(file), "Hero manifest is missing");
        Manifest data = JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
        Require(data != null && data.actions != null && data.actions.Length == 17, "Expected all 17 actions");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (ActionData action in data.actions)
        {
            HeroMotion motion;
            Require(action != null && Enum.TryParse(action.name, out motion)
                && Enum.IsDefined(typeof(HeroMotion), motion) && names.Add(action.name), "Unknown/duplicate hero action");
            Require(action.frame_count > 0 && action.frame_count <= 256 && action.files != null && action.files.Length == action.frame_count
                && action.fps > 0f && !float.IsNaN(action.fps) && !float.IsInfinity(action.fps), "Bad frame data: " + action.name);
        }
        return data;
    }

    static Sprite[] ImportFrames(ActionData action)
    {
        var result = new Sprite[action.frame_count];
        for (int i = 0; i < result.Length; i++)
        {
            string source = action.files[i];
            Require(source.StartsWith("Actions/" + action.name + "/", StringComparison.Ordinal)
                && !source.Contains("..") && source.EndsWith(".png", StringComparison.OrdinalIgnoreCase), "Bad frame path");
            string path = Folder + "/Frames/" + source.Substring("Actions/".Length);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Missing hero PNG: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 32f;
            importer.isReadable = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(result[i] != null && result[i].rect.width == 64f && result[i].rect.height == 64f, "Frame must be 64x64");
        }
        return result;
    }

    static AnimationClip MakeClip(ActionData action, Sprite[] frames)
    {
        string path = Folder + "/Clips/Hero_" + action.name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.name = "Hero_" + action.name;
        clip.frameRate = action.fps;
        clip.legacy = false;
        // Sprite keys each occupy one frame; an extra end key adds another full frame.
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / action.fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0f; settings.stopTime = frames.Length / action.fps;
        settings.loopTime = action.loop; settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimatorController MakeController(List<HeroAnimationLibrary.Entry> entries)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.parameters = new[]
        {
            new AnimatorControllerParameter { name = "Motion", type = AnimatorControllerParameterType.Int },
            new AnimatorControllerParameter { name = "ReloadSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f },
            new AnimatorControllerParameter { name = "LocomotionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f }
        };
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        foreach (AnimatorStateTransition transition in root.anyStateTransitions) root.RemoveAnyStateTransition(transition);
        foreach (ChildAnimatorState state in root.states) root.RemoveState(state.state);
        foreach (ChildAnimatorStateMachine machine in root.stateMachines) root.RemoveStateMachine(machine.stateMachine);
        root.anyStatePosition = new Vector3(0f, 0f); root.entryPosition = new Vector3(0f, -90f);
        foreach (HeroAnimationLibrary.Entry entry in entries)
        {
            int index = (int)entry.motion;
            AnimatorState state = root.AddState(entry.motion.ToString(), new Vector3(300f + index / 6 * 320f, index % 6 * 95f));
            state.motion = entry.clip;
            state.writeDefaultValues = false;
            if (entry.motion == HeroMotion.Reload)
            {
                state.speedParameterActive = true; state.speedParameter = "ReloadSpeed";
            }
            if (entry.motion == HeroMotion.Walk || entry.motion == HeroMotion.CarryWalk)
            {
                state.speedParameterActive = true; state.speedParameter = "LocomotionSpeed";
            }
            AnimatorStateTransition transition = root.AddAnyStateTransition(state);
            transition.hasExitTime = false; transition.hasFixedDuration = true; transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, index, "Motion");
            if (entry.motion == HeroMotion.Idle) root.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static TimelineAsset MakeTimeline(string path, HeroAnimationLibrary library, bool ending)
    {
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, path); }
        foreach (TrackAsset old in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(old);
        AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, ending ? "主角：纪念到憧憬" : "主角：建造完成准备防守");
        track.trackOffset = TrackOffset.ApplySceneOffsets;
        if (ending)
        {
            AddTimelineClip(track, library, HeroMotion.EndingRemember, 0d, 2d);
            AddTimelineClip(track, library, HeroMotion.EndingHope, 2d, 2d);
        }
        else
        {
            AddTimelineClip(track, library, HeroMotion.IdleLeft, 0d, 3d);
            double raise = library.Duration(HeroMotion.RaiseGun);
            AddTimelineClip(track, library, HeroMotion.RaiseGun, 3d, raise);
            AddTimelineClip(track, library, HeroMotion.AimGround, 3d + raise, 10d - 3d - raise);
        }
        EditorUtility.SetDirty(timeline);
        return timeline;
    }

    static void AddTimelineClip(AnimationTrack track, HeroAnimationLibrary library, HeroMotion motion, double start, double duration)
    {
        TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
        AnimationPlayableAsset playable = (AnimationPlayableAsset)clip.asset;
        playable.clip = library.Find(motion).clip;
        playable.loop = library.Find(motion).loop ? AnimationPlayableAsset.LoopMode.On : AnimationPlayableAsset.LoopMode.Off;
        clip.displayName = motion.ToString(); clip.start = start; clip.duration = duration;
        clip.blendInDuration = clip.blendOutDuration = 0d;
    }

    static HeroActor ConfigureActor(GameObject root, HeroAnimationLibrary library)
    {
        Transform child = root.transform.Find("HeroVisual");
        if (child == null)
        {
            var visualObject = new GameObject("HeroVisual");
            visualObject.transform.SetParent(root.transform, false); child = visualObject.transform;
        }
        SpriteRenderer visual = Ensure<SpriteRenderer>(child.gameObject);
        visual.sprite = library.Find(HeroMotion.IdleLeft).firstFrame;
        visual.flipX = visual.flipY = false; visual.sortingOrder = 6;
        SpriteRenderer old = root.GetComponent<SpriteRenderer>();
        if (old != null) { visual.sortingLayerID = old.sortingLayerID; visual.sortingOrder = old.sortingOrder; visual.sharedMaterial = old.sharedMaterial; old.enabled = false; }
        Animator animator = Ensure<Animator>(child.gameObject);
        animator.runtimeAnimatorController = library.controller;
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        HeroActor actor = Ensure<HeroActor>(root);
        actor.library = library; actor.animator = animator; actor.visual = visual;
        actor.groundMuzzle = Child(child, "GroundMuzzle", library.groundMuzzle);
        actor.upMuzzle = Child(child, "UpMuzzle", library.upMuzzle);
        EditorUtility.SetDirty(actor);
        return actor;
    }

    static void MakePrefabs(HeroAnimationLibrary library, TimelineAsset ending)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(temporary);
            var hero = new GameObject("Hero"); ConfigureActor(hero, library);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(hero, HeroPrefabPath);
            Require(prefab != null, "Cannot save Hero prefab");
            UnityEngine.Object.DestroyImmediate(hero);
            var root = new GameObject("HeroEndingSequence");
            var endingHero = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temporary);
            endingHero.transform.SetParent(root.transform, false);
            HeroActor actor = endingHero.GetComponent<HeroActor>();
            PlayableDirector director = root.AddComponent<PlayableDirector>();
            director.playOnAwake = false; director.extrapolationMode = DirectorWrapMode.Hold;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.playableAsset = ending; Bind(director, ending, actor);
            HeroSequenceController sequence = root.AddComponent<HeroSequenceController>();
            sequence.actor = actor; sequence.director = director; sequence.resumeWhenStopped = false;
            Require(PrefabUtility.SaveAsPrefabAsset(root, EndingPrefabPath) != null, "Cannot save ending prefab");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(temporary, true);
        }
    }

    static void ConfigureConstruction(HeroAnimationLibrary library, TimelineAsset transition)
    {
        WithScene("Assets/Scenes/1.unity", scene =>
        {
            ConstructionStageFlow flow = Find<ConstructionStageFlow>(scene);
            Tap tap = Find<Tap>(scene);
            Require(flow != null && tap != null, "Construction scene needs its existing flow and QTE");
            HeroConstructionDriver driver = Find<HeroConstructionDriver>(scene);
            if (driver == null)
            {
                var stage = new GameObject("HeroConstructionStage"); SceneManager.MoveGameObjectToScene(stage, scene);
                driver = stage.AddComponent<HeroConstructionDriver>();
            }
            if (driver.actor == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                hero.name = "HeroConstruction"; hero.transform.SetParent(driver.transform, false);
                driver.actor = hero.GetComponent<HeroActor>();
            }
            driver.qte = tap; driver.flow = flow;
            if (Mathf.Approximately(driver.moveSpeed, 1.7f)) driver.moveSpeed = 1.05f;
            driver.homePoint = Child(driver.transform, "MaterialPickupPoint", new Vector3(1.8f, -2.7f, 0f));
            driver.buildPoint = Child(driver.transform, "MaterialPlacePoint", new Vector3(-1.8f, -2.7f, 0f));
            driver.actor.transform.position = driver.homePoint.position;
            driver.homeCrate = Crate(driver.transform, "HomeCrate", driver.homePoint.position + driver.crateGroundOffset, true);
            driver.placedCrate = Crate(driver.transform, "PlacedCrate", driver.buildPoint.position + driver.crateGroundOffset, false);
            driver.droppedCrate = Crate(driver.transform, "DroppedCrate", driver.homePoint.position + driver.crateGroundOffset, false);
            PlayableDirector director = flow.cutsceneDirector;
            if (director == null) director = Ensure<PlayableDirector>(flow.gameObject);
            // Respect a custom cinematic already authored by the user.
            if (director.playableAsset == null || director.playableAsset == transition)
            {
                director.playableAsset = transition; director.playOnAwake = false;
                director.extrapolationMode = DirectorWrapMode.None;
                Bind(director, transition, driver.actor);
            }
            flow.cutsceneDirector = director;
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(driver); EditorUtility.SetDirty(director);
        });
    }

    static void ConfigureDefense(HeroAnimationLibrary library)
    {
        WithScene("Assets/Scenes/2.unity", scene =>
        {
            TapSec input = Find<TapSec>(scene);
            Require(input != null && input.sbullet != null, "Defense scene needs its existing player/shooter");
            HeroActor actor = ConfigureActor(input.gameObject, library);
            HeroCombatDriver driver = Ensure<HeroCombatDriver>(input.gameObject);
            driver.actor = actor; driver.input = input; driver.shooter = input.sbullet;
            input.sbullet.groundMuzzle = actor.groundMuzzle; input.sbullet.upMuzzle = actor.upMuzzle;
            EditorUtility.SetDirty(input.sbullet); EditorUtility.SetDirty(driver);
        });
    }

    static SpriteRenderer Crate(Transform parent, string name, Vector3 position, bool enabled)
    {
        Transform child = Child(parent, name, Vector3.zero); child.position = position;
        SpriteRenderer renderer2D = Ensure<SpriteRenderer>(child.gameObject);
        string path = Folder + "/Props/Prop_Crate_64.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Require(importer != null, "Crate prop is missing");
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 32f; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        renderer2D.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        renderer2D.sortingOrder = 5; renderer2D.enabled = enabled;
        return renderer2D;
    }

    static void Bind(PlayableDirector director, TimelineAsset timeline, HeroActor actor)
    {
        foreach (TrackAsset track in timeline.GetOutputTracks())
            if (track is AnimationTrack) director.SetGenericBinding(track, actor.animator);
    }
    static void WithScene(string path, Action<Scene> action)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { action(scene); EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene), "Cannot save " + path); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    static T Find<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
    static T Ensure<T>(GameObject gameObject) where T : Component
    {
        T result = gameObject.GetComponent<T>();
        if (result == null) result = gameObject.AddComponent<T>();
        return result;
    }
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        Transform child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        child.localPosition = position; child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
        return child;
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static string Backup()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string output = Path.Combine(root, "AnimatorBackups", "Hero-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        var files = new List<string> { "Assets/Scenes/1.unity", "Assets/Scenes/1.unity.meta", "Assets/Scenes/2.unity", "Assets/Scenes/2.unity.meta" };
        if (Directory.Exists(Folder)) files.AddRange(Directory.GetFiles(Folder, "*", SearchOption.AllDirectories));
        foreach (string file in files)
        {
            if (!File.Exists(file)) continue;
            string target = Path.Combine(output, file); Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
        return output;
    }
    static void Validate(HeroAnimationLibrary library, Manifest manifest)
    {
        Require(library != null && library.actions.Length == 17, "Missing hero action entries");
        AnimatorController controller = library.controller as AnimatorController;
        Require(controller != null && controller.layers[0].stateMachine.states.Length == 17, "Missing Animator states");
        Require(controller.layers[0].stateMachine.anyStateTransitions.Length == 17, "Missing Animator transitions");
        Require(controller.parameters.Any(p => p.name == "LocomotionSpeed" && p.type == AnimatorControllerParameterType.Float), "Missing locomotion speed parameter");
        foreach (HeroMotion motion in new[] { HeroMotion.Walk, HeroMotion.CarryWalk })
        {
            AnimatorState state = controller.layers[0].stateMachine.states.Single(s => s.state.name == motion.ToString()).state;
            Require(state.speedParameterActive && state.speedParameter == "LocomotionSpeed", "Walking state does not follow displacement");
        }
        foreach (ActionData action in manifest.actions)
        {
            HeroMotion motion = (HeroMotion)Enum.Parse(typeof(HeroMotion), action.name);
            HeroAnimationLibrary.Entry entry = library.Find(motion);
            Require(entry != null && entry.clip != null && entry.firstFrame != null, "Missing clip: " + action.name);
            Require(Mathf.Abs(entry.clip.length - action.frame_count / action.fps) < .002f, "Wrong duration: " + action.name);
            Require(AnimationUtility.GetAnimationClipSettings(entry.clip).loopTime == action.loop, "Wrong loop setting");
            var keys = AnimationUtility.GetObjectReferenceCurve(entry.clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            Require(keys != null && keys.Length == action.frame_count && keys.All(k => k.value != null), "Missing sprite keyframes");
            for (int i = 0; i < keys.Length; i++)
                Require(Mathf.Abs(keys[i].time - i / action.fps) < .002f, "Wrong frame timing: " + action.name);
        }
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(EndingPrefabPath).GetComponent<HeroSequenceController>() != null, "Ending sequence not configured");
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
