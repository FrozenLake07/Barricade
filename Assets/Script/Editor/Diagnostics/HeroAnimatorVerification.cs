#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Checks generated assets in isolated preview objects; never enters Play mode or saves scenes.
[InitializeOnLoad]
public static class HeroAnimatorVerification
{
    [Serializable] sealed class SetupReport { public bool success = false; public int version = 0; }
    [Serializable] sealed class Report
    {
        public bool success;
        public int version = HeroAnimatorSetup.Version, spriteSamples, animatorStates, sceneBindings, timelineSamples;
        public string utc = DateTime.UtcNow.ToString("O"), error;
        public List<string> checks = new List<string>();
    }
    static double nextCheck, deadline, editModeStableSince;

    static bool IsPlaying => EditorApplication.isPlaying || Application.isPlaying
        || EditorApplication.isPlayingOrWillChangePlaymode;

    static HeroAnimatorVerification()
    {
        if (Application.isBatchMode) return;
        deadline = EditorApplication.timeSinceStartup + 120d;
        editModeStableSince = EditorApplication.timeSinceStartup;
        EditorApplication.playModeStateChanged += state =>
        {
            editModeStableSince = EditorApplication.timeSinceStartup;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                deadline = EditorApplication.timeSinceStartup + 120d;
                EditorApplication.update -= WaitForSetup;
                EditorApplication.update += WaitForSetup;
            }
        };
        EditorApplication.update += WaitForSetup;
    }

    static void WaitForSetup()
    {
        if (EditorApplication.timeSinceStartup > deadline) { EditorApplication.update -= WaitForSetup; return; }
        if (IsPlaying) { editModeStableSince = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling
            || EditorApplication.isUpdating || EditorApplication.timeSinceStartup - editModeStableSince < 1d) return;
        nextCheck = EditorApplication.timeSinceStartup + 2d;
        const string path = "Library/HeroAnimatorSetupResult.json";
        if (!File.Exists(path)) return;
        SetupReport setup;
        try { setup = JsonUtility.FromJson<SetupReport>(File.ReadAllText(path)); }
        catch { return; }
        if (setup == null || !setup.success || setup.version != HeroAnimatorSetup.Version) return;
        EditorApplication.update -= WaitForSetup;
        Check();
    }

    [MenuItem("街垒/动画/核验主角动画播放与绑定")]
    public static void Check()
    {
        if (IsPlaying) return;
        var report = new Report();
        try
        {
            HeroAnimationLibrary library = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(HeroAnimatorSetup.LibraryPath);
            Need(library != null && library.actions.Length == 17, "Missing action library");
            CheckPlayback(library, report);
            ReadScene("Assets/Scenes/1.unity", scene => CheckConstruction(scene, library, report));
            ReadScene("Assets/Scenes/2.unity", scene => CheckDefense(scene, library, report));
            CheckEnding(report);
            report.success = true;
            Debug.Log("[HeroAnimatorVerification] 原生播放核验完成：" + report.spriteSamples + "帧、" + report.animatorStates + "个Animator状态、场景1/2与结尾Timeline。");
        }
        catch (Exception error) { report.error = error.ToString(); Debug.LogError("[HeroAnimatorVerification] " + error); }
        finally { File.WriteAllText("Library/HeroAnimatorVerification.json", JsonUtility.ToJson(report, true)); }
    }

    static void CheckPlayback(HeroAnimationLibrary library, Report report)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        var graph = PlayableGraph.Create("HeroAnimationVerification");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        try
        {
            var hero = new GameObject("HeroValidationPreview");
            SceneManager.MoveGameObjectToScene(hero, preview);
            var visual = hero.AddComponent<SpriteRenderer>();
            var animator = hero.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var output = AnimationPlayableOutput.Create(graph, "HeroFrames", animator);
            foreach (HeroAnimationLibrary.Entry entry in library.actions)
            {
                var playable = AnimationClipPlayable.Create(graph, entry.clip);
                output.SetSourcePlayable(playable);
                graph.Play();
                var keys = AnimationUtility.GetObjectReferenceCurve(entry.clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                int expectedFrames = Mathf.RoundToInt(entry.duration * entry.fps);
                Need(expectedFrames > 0 && keys != null && keys.Length == expectedFrames, "Missing frames: " + entry.motion);
                foreach (ObjectReferenceKeyframe key in keys)
                {
                    playable.SetTime(key.time + .001d);
                    graph.Evaluate(0f);
                    Need(visual.sprite == key.value, "Sprite playback mismatch: " + entry.motion + " at " + key.time);
                    report.spriteSamples++;
                }
                graph.DestroyPlayable(playable);
            }
            var controller = AnimatorControllerPlayable.Create(graph, library.controller);
            output.SetSourcePlayable(controller);
            graph.Play();
            var nativeController = library.controller as AnimatorController;
            foreach (HeroAnimationLibrary.Entry entry in library.actions)
            {
                controller.SetInteger("Motion", (int)entry.motion);
                graph.Evaluate(.001f);
                graph.Evaluate(.001f);
                Need(controller.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash(entry.motion.ToString()),
                    "Animator transition mismatch: " + entry.motion);
                Need(nativeController.layers[0].stateMachine.states.Any(s => s.state.name == entry.motion.ToString()
                    && s.state.motion == entry.clip), "State references wrong clip: " + entry.motion);
                report.animatorStates++;
            }
            report.checks.Add(report.spriteSamples + " sprite samples and all " + report.animatorStates + " Animator parameter transitions");
        }
        finally { if (graph.IsValid()) graph.Destroy(); EditorSceneManager.ClosePreviewScene(preview); }
    }

    static void CheckConstruction(Scene scene, HeroAnimationLibrary library, Report report)
    {
        HeroConstructionDriver driver = Find<HeroConstructionDriver>(scene);
        Need(driver != null && driver.actor != null && driver.actor.library == library, "Scene1 missing construction actor");
        Need(driver.qte == Find<Tap>(scene) && driver.qte != null, "Scene1 QTE is not bound");
        Need(driver.flow == Find<ConstructionStageFlow>(scene) && driver.flow != null, "Scene1 flow is not bound");
        Need(driver.homePoint != null && driver.buildPoint != null && driver.homeCrate != null
            && driver.placedCrate != null && driver.droppedCrate != null, "Scene1 missing work path/props");
        Need(Mathf.Approximately(driver.flow.gameplaySeconds, 100f), "Scene1 build duration changed");
        PlayableDirector director = driver.flow.cutsceneDirector;
        Need(director != null && director.playableAsset != null, "Scene1 missing transition Timeline");
        var timeline = director.playableAsset as TimelineAsset;
        Need(timeline != null, "Scene1 transition is not a Timeline");
        foreach (AnimationTrack track in timeline.GetOutputTracks().OfType<AnimationTrack>())
            Need(director.GetGenericBinding(track) != null, "Scene1 Timeline has an unbound animation track");
        report.sceneBindings++;
        report.checks.Add("Scene1 QTE, 100-second flow, work points, props and transition binding");
    }

    static void CheckDefense(Scene scene, HeroAnimationLibrary library, Report report)
    {
        HeroCombatDriver driver = Find<HeroCombatDriver>(scene);
        TapSec input = Find<TapSec>(scene);
        Need(driver != null && input != null && driver.input == input && driver.shooter == input.sbullet,
            "Scene2 input/shooter is not bound");
        HeroActor actor = driver.actor;
        Need(actor != null && actor.library == library && actor.animator != null && actor.visual != null,
            "Scene2 actor references are incomplete");
        Need(actor.animator.runtimeAnimatorController == library.controller && actor.animator.enabled && actor.visual.enabled,
            "Scene2 visual/Animator is disabled or has wrong controller");
        Need(driver.shooter.groundMuzzle == actor.groundMuzzle && driver.shooter.upMuzzle == actor.upMuzzle
            && actor.groundMuzzle != null && actor.upMuzzle != null, "Scene2 muzzle references are not bound");
        Need(actor.transform == input.transform && input.GetComponent<CapsuleTrack>() != null,
            "Scene2 original player/track components were lost");
        report.sceneBindings++;
        report.checks.Add("Scene2 original player, input, shooter, enabled Animator and both muzzle references");
    }

    static void CheckEnding(Report report)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroAnimatorSetup.Folder + "/Prefabs/HeroEndingSequence.prefab");
            Need(prefab != null, "Ending prefab missing");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var sequence = root.GetComponent<HeroSequenceController>();
            Need(sequence != null && sequence.actor != null && sequence.director != null, "Ending references missing");
            var director = sequence.director;
            var timeline = director.playableAsset as TimelineAsset;
            Need(timeline != null && !director.playOnAwake && director.extrapolationMode == DirectorWrapMode.Hold,
                "Ending autoplay/hold configuration is wrong");
            var tracks = timeline.GetOutputTracks().OfType<AnimationTrack>().ToArray();
            Need(tracks.Length == 1 && director.GetGenericBinding(tracks[0]) == sequence.actor.animator, "Ending track unbound");
            Need(tracks[0].GetClips().Count() == 2 && Math.Abs(timeline.duration - 4d) < .002d, "Ending sequence duration wrong");
            sequence.actor.SetExternalControl(true);
            foreach (var sample in new[] { HeroMotion.EndingRemember, HeroMotion.EndingHope })
            {
                director.time = sample == HeroMotion.EndingRemember ? .01d : 2.01d;
                director.Evaluate();
                Need(sequence.actor.visual.sprite == sequence.actor.library.Find(sample).firstFrame, "Ending Timeline playback wrong: " + sample);
                report.timelineSamples++;
            }
            director.Stop();
            report.checks.Add("Ending prefab: bound 4-second Remember to Hope Timeline, no autoplay, hold final pose");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    static void ReadScene(string path, Action<Scene> read)
    {
        if (IsPlaying) throw new OperationCanceledException("Play mode changed during hero validation");
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { read(scene); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
    static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
