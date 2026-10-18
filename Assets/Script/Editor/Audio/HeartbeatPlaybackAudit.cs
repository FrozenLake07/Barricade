#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 进入游戏后记录一次实际运行的排程，不改变游戏、不自动进入Play。
[InitializeOnLoad]
public static class HeartbeatPlaybackAudit
{
    [Serializable] sealed class Report
    {
        public int version = 3, runtimeVersion, bufferLength, buffers, sampleRate;
        public bool deviceLatencyMeasured = false;
        public string utc = DateTime.UtcNow.ToString("O"), scene, source;
        public double estimatedOutputMs;
        public float extraOutputMs, beattime, perfecttime;
        public string[] queuedPulses;
    }
    static double started;
    static bool pending;
    static HeartbeatPlaybackAudit()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            { started = EditorApplication.timeSinceStartup; pending = true; }
            if (state == PlayModeStateChange.ExitingPlayMode && pending) Save();
        };
        EditorApplication.update += () =>
        {
            if (pending && EditorApplication.isPlaying && EditorApplication.timeSinceStartup - started >= 8d) Save();
        };
    }
    static void Save()
    {
        var heart = UnityEngine.Object.FindFirstObjectByType<Heartbeat>();
        if (heart == null) { pending = false; return; }
        var report = new Report { scene = heart.gameObject.scene.name, source = heart.source.ToString(),
            runtimeVersion = heart.AudioScheduleVersion,
            estimatedOutputMs = RhythmManager.OutputDelaySeconds * 1000d,
            extraOutputMs = RhythmManager.ExtraOutputDelayMs, queuedPulses = heart.AudioTrace,
            beattime = heart.source == Heartbeat.BeatSource.TapSec ? TapSec.beattime : Tap.beattime,
            perfecttime = heart.source == Heartbeat.BeatSource.TapSec ? TapSec.perfecttime : Tap.perfecttime };
        AudioSettings.GetDSPBufferSize(out report.bufferLength, out report.buffers);
        report.sampleRate = AudioSettings.outputSampleRate;
        File.WriteAllText("Library/HeartbeatPlaybackAudit.json", JsonUtility.ToJson(report, true));
        pending = false;
        Debug.Log("[HeartbeatPlaybackAudit] 已记录v3实际排程；补偿为估计值，并非硬件实测延迟。");
    }
}
#endif
