using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// 音乐只接管玩家心跳与判定；敌人 Beat 的组节奏不受影响。
[DefaultExecutionOrder(-200)]
public class RhythmManager : MonoBehaviour
{
    public enum PerfectTimingMode { MarkedAccents, DynamicBpm }
    [Serializable]
    public class MusicBridge
    {
        public AudioClip clip;
        [Tooltip("可选：音频内每一拍的秒数，严格递增；有至少两拍时优先使用，音乐播放至文件末尾。为空时使用下面的固定 BPM。")]
        public float[] beatTimes = Array.Empty<float>();
        [Tooltip("Beat Times中允许Perfect的强拍序号，从0开始，严格递增。留空时每拍都可Perfect；弱拍保留轻心跳。")]
        public int[] perfectBeatIndices = Array.Empty<int>();
        [Tooltip("Marked Accents仅标记强拍；Dynamic Bpm按逐拍时间表随速度变化，每拍允许Perfect，不改变音频速度。")]
        public PerfectTimingMode perfectTiming = PerfectTimingMode.MarkedAccents;
        [Min(1f)] public float bpm = 80f;
        [Min(0f)] public float firstBeatOffset;
        [Min(1)] public int beatCount = 16;
        [Tooltip("拍点前后各多少秒算 Perfect；运行时限制为小于半拍")]
        [Min(0.001f)] public float perfectWindow = 0.1f;
        [Min(0f)] public float fadeOutSeconds = 1f;
    }

    [Header("两段衔接音乐；未填 Clip 就保持原有模式")]
    public MusicBridge openingToConstruction = new MusicBridge();
    public MusicBridge constructionToDefense = new MusicBridge();
    [Header("播放")]
    [Range(0f, 1f)] public float volume = 0.65f;
    [Min(0.05f)] public float scheduleLeadTime = 0.25f;
    public AudioMixerGroup musicMixerGroup;

    [Header("声音输出延迟补偿（蓝牙需要试听校准）")]
    public bool compensateOutputLatency = true;
    public bool estimateDspBufferDelay = true;
    [Tooltip("在Unity缓冲估计之外，再补偿耳机/驱动延迟。只是初始估计；F7增加10ms，F6减少10ms。")]
    [Range(-100f, 500f)] public float additionalOutputDelayMs = 150f;
    const string LatencyPreference = "Barricade.AudioExtraDelayMs.v3";
    int sampledFrame = -1;
    bool sampledAvailable;
    MusicBeatFrame sampledMusic;

    public static double OutputDelaySeconds
    {
        get
        {
            if (current == null || !current.compensateOutputLatency) return 0d;
            AudioSettings.GetDSPBufferSize(out int size, out int buffers);
            return HeartbeatAudioTimeline.EstimateDelay(current.estimateDspBufferDelay ? size : 0,
                buffers, AudioSettings.outputSampleRate, current.additionalOutputDelayMs);
        }
    }
    public static float ExtraOutputDelayMs => current != null ? current.additionalOutputDelayMs : 0f;
    public static void AdjustOutputDelay(float deltaMs)
    {
        if (current == null) return;
        current.additionalOutputDelayMs = Mathf.Clamp(current.additionalOutputDelayMs + deltaMs, -100f, 500f);
        current.sampledFrame = -1;
        PlayerPrefs.SetFloat(LatencyPreference, current.additionalOutputDelayMs);
        PlayerPrefs.Save();
    }

    static RhythmManager current;
    AudioSource musicSource;
    MusicBeatClock clock, finishedClock;
    MusicBridge playingSettings;
    string targetScene, preparedScene;
    bool loading;
    int musicVersion;
    long claimedPerfectBeat = -1;

    public static bool JudgementFromMusic = false;

    public static bool IsFollowingMusic => TryGetMusicFrame(out _);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRun() { current = null; JudgementFromMusic = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap() => EnsureInstance();

    static RhythmManager EnsureInstance()
    {
        if (current != null) return current;
        GameObject prefab = Resources.Load<GameObject>("Rhythm/RhythmRoot");
        if (prefab != null) Instantiate(prefab);
        else new GameObject("RhythmRoot").AddComponent<RhythmManager>();
        return current;
    }

    void Awake()
    {
        if (current != null && current != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }
        current = this;
        if (PlayerPrefs.HasKey(LatencyPreference))
            additionalOutputDelayMs = Mathf.Clamp(PlayerPrefs.GetFloat(LatencyPreference), -100f, 500f);
        DontDestroyOnLoad(gameObject);
        musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.pitch = 1f;
        musicSource.outputAudioMixerGroup = musicMixerGroup;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        if (current != this || !enabled) return;
        // 直接在已打开的场景中进入Play，或管理器创建较晚时，补齐首次播放。
        // 已经处理过场景事件、准备过音乐或正在切场，都不能重播／覆盖歌曲。
        if (musicVersion == 0 && !loading)
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (current == this) current = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (StoryDirector.Active) { StopMusic(); return; }
        loading = false;
        bool prepared = preparedScene == scene.name;
        preparedScene = null;
        // 通过统一入口先播放再加载时，场景加载不能把音乐从头再播一次。
        if (prepared) { RefreshMusic(); return; }
        StartBridge(scene.name);
    }

    void Update() => RefreshMusic();

    void RefreshMusic()
    {
        if (clock == null) return;
        double remaining = clock.EndDspTime - AudioSettings.dspTime;
        // 声音已送出之后还在设备中排队；判定保持到玩家预计听完整段音乐。
        if (remaining + OutputDelaySeconds <= 0d)
        {
            finishedClock = clock;
            clock = null;
            musicSource.Stop();
            return;
        }
        double fade = Math.Max(0d, playingSettings.fadeOutSeconds);
        musicSource.volume = volume * (fade > 0d ? (float)Math.Max(0d, Math.Min(1d, remaining / fade)) : 1f);
    }

    void StartBridge(string sceneName)
    {
        sampledFrame = -1;
        musicSource.Stop();
        clock = finishedClock = null;
        claimedPerfectBeat = -1;
        musicVersion++;
        targetScene = sceneName;
        MusicBridge settings = sceneName == "1" ? openingToConstruction
            : sceneName == "2" ? constructionToDefense : null;
        if (settings == null || settings.clip == null)
        {
            if (sceneName == "1" || sceneName == "2")
                Debug.Log("[RhythmManager] 场景 " + sceneName + " 未配置衔接音乐，采用自主心跳。");
            return;
        }
        if (!ValidNumber(settings.bpm) || settings.bpm <= 0f
            || !ValidNumber(settings.firstBeatOffset) || settings.firstBeatOffset < 0f
            || !ValidNumber(settings.perfectWindow) || settings.perfectWindow <= 0f
            || !ValidNumber(settings.fadeOutSeconds) || settings.fadeOutSeconds < 0f
            || !ValidNumber(scheduleLeadTime) || !ValidNumber(volume) || settings.beatCount < 1)
        {
            Debug.LogWarning("[RhythmManager] BPM／首拍偏移／拍数／判定窗口无效，采用自主心跳。");
            return;
        }
        double start = AudioSettings.dspTime + Math.Max(0.05d, scheduleLeadTime);
        int count = 0;
        bool usingBeatMap = false;
        if (settings.beatTimes != null && settings.beatTimes.Length >= 2)
        {
            try
            {
                try
                {
                    clock = new MusicBeatClock(start, settings.beatTimes, settings.clip.length,
                        settings.perfectWindow, settings.perfectTiming == PerfectTimingMode.DynamicBpm
                            ? Array.Empty<int>() : settings.perfectBeatIndices);
                }
                catch (InvalidOperationException)
                {
                    // 强拍配置错误不能退回到“每拍都Perfect”，应安全关闭本段Perfect。
                    Debug.LogWarning("[RhythmManager] 强拍序号无效：本段只播放音乐和心跳，不开放Perfect。请检查Perfect Beat Indices。");
                    clock = new MusicBeatClock(start, settings.beatTimes, settings.clip.length,
                        settings.perfectWindow, Array.Empty<int>(), false);
                }
                count = settings.beatTimes.Length;
                usingBeatMap = true;
            }
            catch (ArgumentOutOfRangeException)
            {
                Debug.LogWarning("[RhythmManager] 逐拍时间表无效，退回固定 BPM。拍点应在音频内并严格递增。");
            }
        }
        if (clock == null)
        {
            double interval = 60d / settings.bpm;
            int available = (int)Math.Min(int.MaxValue,
                Math.Floor((settings.clip.length - settings.firstBeatOffset) / interval + 0.00001d));
            if (available < 1)
            {
                Debug.LogWarning("[RhythmManager] 音乐在首拍后不足一整拍，采用自主心跳。");
                return;
            }
            count = Math.Min(settings.beatCount, available);
            if (count != settings.beatCount)
                Debug.LogWarning("[RhythmManager] 音乐长度不足，跟拍数缩短为 " + count + "，避免音频先结束。");
            clock = new MusicBeatClock(start, settings.firstBeatOffset, interval, count, settings.perfectWindow);
        }
        playingSettings = settings;
        musicSource.clip = settings.clip;
        musicSource.volume = volume;
        musicSource.pitch = 1f;
        musicSource.outputAudioMixerGroup = musicMixerGroup;
        musicSource.PlayScheduled(start);
        musicSource.SetScheduledEndTime(clock.EndDspTime);
        Debug.Log("[RhythmManager] 场景 " + sceneName + "：音乐跟拍 " + count + " 拍，"
            + (usingBeatMap ? "使用逐拍时间表" : "BPM " + settings.bpm)
            + (settings.perfectTiming == PerfectTimingMode.DynamicBpm ? "；动态BPM每拍Perfect" : "；标记强拍Perfect"));
    }

    static bool ValidNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool TryGetMusicFrame(out MusicBeatFrame frame)
    {
        frame = default;
        if (!JudgementFromMusic) return false;
        if (current == null) return false;
        current.RefreshMusic();
        if (current.sampledFrame == Time.frameCount)
        { frame = current.sampledMusic; return current.sampledAvailable; }
        current.sampledFrame = Time.frameCount;
        if (current.clock == null || !current.clock.TrySample(AudioSettings.dspTime - OutputDelaySeconds, out frame))
        { current.sampledAvailable = false; current.sampledMusic = default; return false; }
        frame.UsesDynamicBpm = current.playingSettings.perfectTiming == PerfectTimingMode.DynamicBpm;
        current.sampledMusic = frame; current.sampledAvailable = true;
        return true;
    }

    // 音频预排用未经设备延迟平移的DSP时间；与上面的可听判定时间分开。
    public static void VisitHeartbeatPulses(double fromDsp, double toDsp,
        Action<double, double, long, int, float> visitor)
    {
        if (current != null && current.clock != null)
            current.clock.VisitHeartbeatPulses(fromDsp, toDsp, visitor);
    }

    public static bool TryClaimPerfect(long beatIndex)
    {
        if (!TryGetMusicFrame(out MusicBeatFrame frame) || !frame.IsPerfect
            || beatIndex != frame.BeatIndex || beatIndex <= current.claimedPerfectBeat) return false;
        current.claimedPerfectBeat = beatIndex;
        return true;
    }

    public static bool ApplyHandoff(string sceneName, ref int seenVersion,
        ref float beatTime, ref float setBeatTime, ref float perfectTime, float window)
    {
        if (!JudgementFromMusic || current == null) return false;
        current.RefreshMusic();
        if (current.clock != null || current.finishedClock == null
            || current.targetScene != sceneName || seenVersion == current.musicVersion) return false;
        seenVersion = current.musicVersion;
        current.finishedClock.GetFreeTimers(AudioSettings.dspTime - OutputDelaySeconds, window,
            out beatTime, out setBeatTime, out perfectTime);
        return true;
    }

    public static void StopMusic()
    {
        if (current == null) return;
        current.musicSource.Stop(); current.clock = current.finishedClock = null;
        current.sampledFrame = -1;
    }

    /// 提前几拍调用，让音乐真正跨过场景边界；稍后切场景时不重播。
    public static void PrepareMusicForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[RhythmManager] 场景未加入 Build Settings：" + sceneName);
            return;
        }
        RhythmManager manager = EnsureInstance();
        if (manager.loading || manager.preparedScene == sceneName) return;
        manager.StartBridge(sceneName);
        manager.preparedScene = sceneName;
    }

    /// 未提前准备时先开始音乐再异步加载；已经准备则继续同一段音乐。
    public static void LoadSceneWithMusic(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[RhythmManager] 场景未加入 Build Settings：" + sceneName);
            return;
        }
        RhythmManager manager = EnsureInstance();
        if (manager.loading) return;
        manager.loading = true;
        if (manager.preparedScene != sceneName) manager.StartBridge(sceneName);
        manager.preparedScene = sceneName;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }

    // 可直接绑定到按钮／Timeline Signal；也可在运行时从组件菜单测试。
    [ContextMenu("测试衔接：进入场景1（建造）")]
    public void GoToConstruction() => LoadSceneWithMusic("1");
    [ContextMenu("测试衔接：进入场景2（守卫）")]
    public void GoToDefense() => LoadSceneWithMusic("2");
}

public struct MusicBeatFrame
{
    public bool Started, IsPerfect, HasVisualBeat, HasNextPerfect;
    public float VisualStrength;
    public bool UsesDynamicBpm;
    public float WindowHalfWidth;
    public float CurrentBpm => Interval > 0f ? 60f / Interval : 0f;
    public long BeatIndex;
    public float Interval, BeatTime, PerfectTime, Phase;
    public bool HasPulseInterval;
    public float PulsePhase, PulseSeconds;
    public long PulseCycle;
}

/// 纯时间计算，与音源、场景和帧率解耦，便于测试跨帧和交接边界。
public sealed class MusicBeatClock
{
    readonly double start, firstBeat, period, window;
    readonly int count;
    readonly double[] beats;
    readonly bool[] perfectMask;
    readonly double mappedEnd;
    public double EndDspTime => beats != null ? mappedEnd : firstBeat + count * period;

    public MusicBeatClock(double startDsp, double offset, double interval, int beatCount, double perfectWindow)
    {
        if (double.IsNaN(startDsp) || double.IsInfinity(startDsp)
            || double.IsNaN(offset) || double.IsInfinity(offset) || offset < 0d
            || double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0d
            || double.IsNaN(perfectWindow) || double.IsInfinity(perfectWindow) || perfectWindow <= 0d
            || beatCount < 1) throw new ArgumentOutOfRangeException();
        start = startDsp;
        firstBeat = startDsp + offset;
        period = interval;
        count = beatCount;
        window = Math.Min(perfectWindow, period * 0.49999d);
    }

    /// 电影录音与剪辑版可使用逐拍时间表；末尾按音频实际长度交接。
    public MusicBeatClock(double startDsp, float[] beatTimes, double clipDuration, double perfectWindow,
        int[] perfectBeatIndices = null, bool allowAllWhenEmpty = true)
        : this(startDsp, 0d, 1d, 2, perfectWindow)
    {
        if (beatTimes == null || beatTimes.Length < 2 || double.IsNaN(clipDuration)
            || double.IsInfinity(clipDuration) || clipDuration <= 0d)
            throw new ArgumentOutOfRangeException();
        beats = new double[beatTimes.Length];
        for (int i = 0; i < beats.Length; i++)
        {
            double value = beatTimes[i];
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d
                || value >= clipDuration || (i > 0 && value <= beats[i - 1]))
                throw new ArgumentOutOfRangeException();
            beats[i] = value;
        }
        if (!allowAllWhenEmpty || (perfectBeatIndices != null && perfectBeatIndices.Length > 0))
        {
            perfectMask = new bool[beats.Length];
            int last = -1;
            if (perfectBeatIndices != null)
                foreach (int index in perfectBeatIndices)
                {
                    if (index <= last || index >= beats.Length || index < 0)
                        throw new InvalidOperationException("Invalid Perfect beat index.");
                    perfectMask[index] = true;
                    last = index;
                }
        }
        count = beats.Length;
        firstBeat = startDsp + beats[0];
        period = beats[count - 1] - beats[count - 2];
        // 实际窗口在每一拍按相邻间距限制，避免较快的段落出现窗口重叠。
        window = perfectWindow;
        mappedEnd = startDsp + clipDuration;
    }

    double MappedWindow(int index)
    {
        double gap = double.MaxValue;
        if (index > 0) gap = Math.Min(gap, beats[index] - beats[index - 1]);
        if (index + 1 < count) gap = Math.Min(gap, beats[index + 1] - beats[index]);
        return Math.Min(window, gap * 0.49999d);
    }

    static void SetPulseSpan(ref MusicBeatFrame frame, double elapsed, double begin,
        double end, double afterWindow, double beforeWindow, long cycle)
    {
        double left = begin + afterWindow, right = end - beforeWindow;
        double duration = Math.Max(0d, right - left);
        frame.PulseCycle = cycle;
        frame.PulseSeconds = (float)duration;
        frame.PulsePhase = duration > .000001d
            ? (float)Math.Max(0d, Math.Min(1d, (elapsed - left) / duration)) : 0f;
        frame.HasPulseInterval = frame.HasVisualBeat && !frame.IsPerfect
            && duration > .000001d && elapsed >= left && elapsed < right;
    }

    bool TrySampleMapped(double now, out MusicBeatFrame frame)
    {
        frame = default;
        if (now >= EndDspTime) return false;
        double elapsed = now - start;
        int previous = Array.BinarySearch(beats, elapsed);
        if (previous < 0) previous = ~previous - 1;
        int nearest = Math.Max(0, previous);
        if (previous >= 0 && previous + 1 < count
            && beats[previous + 1] - elapsed < elapsed - beats[previous]) nearest++;
        double width = MappedWindow(nearest);
        bool perfect = now >= start && (perfectMask == null || perfectMask[nearest])
            && Math.Abs(elapsed - beats[nearest]) <= width + 0.0000001d;
        double interval = previous < 0 ? beats[1] - beats[0]
            : previous + 1 < count ? beats[previous + 1] - beats[previous] : period;
        int next = previous + 1;
        while (next < count && perfectMask != null && !perfectMask[next]) next++;
        double until = next < count ? beats[next] - MappedWindow(next) - elapsed : EndDspTime - now;
        double phase = previous < 0 ? 0d : Math.Min(0.999999d, Math.Max(0d, (elapsed - beats[previous]) / interval));
        frame = new MusicBeatFrame
        {
            Started = now >= start,
            IsPerfect = perfect,
            BeatIndex = nearest,
            Interval = (float)interval,
            BeatTime = perfect ? 0f : (float)Math.Max(0.0001d, until),
            PerfectTime = perfect ? (float)Math.Max(0.0001d, beats[nearest] + width - elapsed) : 0f,
            Phase = (float)phase,
            HasVisualBeat = now >= start && elapsed >= beats[0],
            VisualStrength = perfectMask == null || perfectMask[nearest] ? 1f : 0.45f,
            HasNextPerfect = next < count,
            WindowHalfWidth = (float)width
        };
        if (previous >= 0)
        {
            double begin = beats[previous];
            long extra = previous == count - 1 ? (long)Math.Max(0d, Math.Floor((elapsed - begin) / period)) : 0L;
            begin += extra * period;
            double end = previous + 1 < count ? beats[previous + 1] : begin + period;
            double after = extra == 0 && (perfectMask == null || perfectMask[previous]) ? MappedWindow(previous) : 0d;
            double before = previous + 1 < count && (perfectMask == null || perfectMask[previous + 1])
                ? MappedWindow(previous + 1) : 0d;
            SetPulseSpan(ref frame, elapsed, begin, Math.Min(end, mappedEnd - start), after, before, previous + extra);
        }
        return true;
    }

    public bool TrySample(double now, out MusicBeatFrame frame)
    {
        if (beats != null) return TrySampleMapped(now, out frame);
        frame = default;
        if (now >= EndDspTime) return false;
        double elapsed = now - firstBeat;
        long nearest = (long)Math.Floor(elapsed / period + 0.5d);
        bool perfect = now >= start && nearest >= 0 && nearest < count
            && Math.Abs(elapsed - nearest * period) <= window + 0.0000001d;
        long next = Math.Max(0L, (long)Math.Floor((elapsed + window) / period) + 1);
        double until = next < count ? firstBeat + next * period - window - now : EndDspTime - now;
        double phase = elapsed / period - Math.Floor(elapsed / period);
        frame = new MusicBeatFrame
        {
            Started = now >= start,
            IsPerfect = perfect,
            BeatIndex = nearest,
            Interval = (float)period,
            BeatTime = perfect ? 0f : (float)Math.Max(0.0001d, until),
            PerfectTime = perfect ? (float)Math.Max(0.0001d, firstBeat + nearest * period + window - now) : 0f,
            Phase = (float)phase,
            HasVisualBeat = now >= start && elapsed >= -window,
            VisualStrength = 1f,
            HasNextPerfect = next < count,
            WindowHalfWidth = (float)window
        };
        long previous = (long)Math.Floor(elapsed / period);
        if (previous >= 0 && previous < count)
            SetPulseSpan(ref frame, elapsed, previous * period, (previous + 1d) * period,
                window, previous + 1 < count ? window : 0d, previous);
        return true;
    }

    // 精确枚举未来窗口之间的两次心跳，窄间隙也不依靠逐帧“碰巧撞上”。
    public void VisitHeartbeatPulses(double fromDsp, double toDsp,
        Action<double, double, long, int, float> visitor)
    {
        for (int i = 0; i < count; i++)
        {
            double begin = beats != null ? start + beats[i] : firstBeat + i * period;
            double end = beats != null && i + 1 < count ? start + beats[i + 1] : begin + period;
            double after = beats != null ? (perfectMask == null || perfectMask[i] ? MappedWindow(i) : 0d) : window;
            double before = i + 1 < count ? (beats != null
                ? (perfectMask == null || perfectMask[i + 1] ? MappedWindow(i + 1) : 0d) : window) : 0d;
            if (end - before < fromDsp || begin + after > toDsp) continue;
            VisitSpan(begin + after, Math.Min(end - before, EndDspTime), i, fromDsp, toDsp, visitor);
        }
        if (beats != null)
        {
            double last = start + beats[count - 1];
            long extra = Math.Max(1L, (long)Math.Floor((fromDsp - last) / period));
            for (double begin = last + extra * period; begin < Math.Min(toDsp, EndDspTime); begin += period, extra++)
                VisitSpan(begin, Math.Min(begin + period, EndDspTime), count - 1L + extra, fromDsp, toDsp, visitor);
        }
    }

    void VisitSpan(double left, double right, long cycle, double from, double to,
        Action<double, double, long, int, float> visitor)
    {
        if (right <= left) return;
        for (int hit = 1; hit <= 2; hit++)
        {
            double peak = left + (right - left) * (hit == 1 ? .28d : .67d);
            if (peak < from || peak > to) continue;
            if (TrySample(peak, out MusicBeatFrame frame) && frame.HasPulseInterval && !frame.IsPerfect)
                visitor(peak, right, cycle, hit, frame.VisualStrength);
        }
    }

    public void GetFreeTimers(double now, float legacyWindow,
        out float beatTime, out float setBeatTime, out float perfectTime)
    {
        double elapsed = Math.Max(0d, now - EndDspTime);
        setBeatTime = (float)Math.Max(0.5d, period);
        if (elapsed < legacyWindow)
        {
            // 边界就是自主模式的第一拍，不是再等一整拍或重开上一拍。
            beatTime = 0f;
            perfectTime = (float)(legacyWindow - elapsed);
        }
        else
        {
            // 长时间加载后不补发旧拍点，只对齐到还没发生的下一拍。
            double next = EndDspTime + (Math.Floor(elapsed / setBeatTime) + 1d) * setBeatTime;
            beatTime = (float)Math.Max(0.0001d, next - now);
            perfectTime = legacyWindow;
        }
    }
}
