#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 在停止运行后用原生API配置短心跳音频，只读核验两个场景；不保存场景。
[InitializeOnLoad]
public static class HeartbeatAudioSetup
{
    [Serializable] sealed class Report
    {
        public bool success;
        public int version = 3, clips, sceneHeartbeats, timingChecks;
        public string utc = DateTime.UtcNow.ToString("O"), error;
        public bool devicePlaybackTested = false;
    }

    static HeartbeatAudioSetup()
    {
        if (!Application.isBatchMode) EditorApplication.delayCall += Wait;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Wait;
        };
    }

    static void Wait()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Wait; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
        Verify();
    }

    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
        var report = new Report();
        try
        {
            for (int i = 1; i <= 3; i++)
            {
                string path = "Assets/Resources/SFX/HeartbeatPulse/Pulse-V" + i + ".wav";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Need(importer != null, "Missing pulse audio: " + path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true; importer.loadInBackground = false;
                importer.SaveAndReimport();
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Need(clip != null && clip.channels == 1 && Math.Abs(clip.length - .09f) < .002f, "Wrong short pulse format");
                clip.LoadAudioData();
                float[] samples = new float[clip.samples];
                Need(clip.GetData(samples, 0) && samples.Any(v => Math.Abs(v) > .05f), "Empty pulse samples");
                int peak = 0;
                for (int s = 1; s < samples.Length; s++)
                    if (Math.Abs(samples[s]) > Math.Abs(samples[peak])) peak = s;
                Need((float)peak / clip.frequency < .006f, "Pulse attack is too late");
                report.clips++;
            }
            foreach (string path in new[] { "Assets/Scenes/1.unity", "Assets/Scenes/2.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    Heartbeat[] hearts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Heartbeat>(true)).ToArray();
                    Need(hearts.Length == 1 && hearts[0].enabled && hearts[0].enableHeartbeatSound
                        && hearts[0].heartbeatVolume > 0f, "Heartbeat audio is disabled in " + path);
                    report.sceneHeartbeats++;
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            var clock = new MusicBeatClock(0d, new[] { .1f, .9f, 1.8f, 2.6f }, 3.5d, .25d);
            int planned = 0;
            clock.VisitHeartbeatPulses(0d, 3.5d, (peak, end, cycle, hit, strength) =>
            {
                const double delay = .214d;
                double audiblePeak = peak + delay;
                Need(HeartbeatAudioTimeline.TryCue(peak - .15d, .02d, audiblePeak,
                    end + delay, delay, .002d, .09d, out var cue), "Cannot schedule future heartbeat");
                Need(cue.AudibleEnd < end + delay && Math.Abs(cue.AudiblePeak - audiblePeak) < .000001d,
                    "Bluetooth compensation crosses Perfect");
                planned++;
            });
            Need(planned > 0, "No future heartbeat scheduled");
            Need(HeartbeatPulseTiming.CanPulse(.85f), "beattime must allow heartbeat");
            Need(!HeartbeatPulseTiming.IsPerfectGap(.85f, .5f), "Preset perfecttime is not an active window");
            Need(!HeartbeatPulseTiming.CanPulse(0f) && HeartbeatPulseTiming.IsPerfectGap(0f, .5f),
                "Perfect must be silent");
            Need(HeartbeatPulseTiming.ClipSeconds(.09f, .015f) <= .015f,
                "Pulse tail would cross Perfect boundary");
            Need(HeartbeatPulseTiming.CanPulse(0f, true), "Wrong-key rest must keep heartbeat");
            var events = new HeartbeatPulseEvents();
            for (double t = 0d; t < 3.5d; t += .002d)
            {
                MusicBeatFrame frame;
                Need(clock.TrySample(t, out frame), "Music ended early");
                bool active = frame.Started && frame.HasPulseInterval
                    && HeartbeatPulseTiming.CanPulse(frame.BeatTime)
                    && !HeartbeatPulseTiming.IsPerfectGap(frame.BeatTime, frame.PerfectTime);
                int pulse = events.Sample(frame.PulsePhase, frame.PulseCycle, true, active);
                Need(!frame.IsPerfect || (!frame.HasPulseInterval && pulse == 0), "Sound leaked into Perfect");
                if (pulse != 0) Need(frame.BeatTime > 0f && frame.PerfectTime == 0f,
                    "Heartbeat was reversed into Perfect");
                report.timingChecks++;
            }
            report.success = true;
            Debug.Log("[HeartbeatAudioSetup] 三段单跳音频、两个场景和Perfect静音时序已核验。");
        }
        catch (Exception error) { report.error = error.ToString(); Debug.LogError("[HeartbeatAudioSetup] " + error); }
        finally { File.WriteAllText("Library/HeartbeatAudioSetupResult.json", JsonUtility.ToJson(report, true)); }
    }

    static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
