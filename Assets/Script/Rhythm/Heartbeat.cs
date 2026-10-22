using UnityEngine;

/// 战役直接读取CampaignBeatClock；单独运行场景时读取Tap/TapSec静态计时器。
///   beattime 递减期间 —— 越接近归零，跳得越重、越快
///   perfecttime 窗口内 —— 缩到 restScale 并完全静止
///
/// 节拍来源默认是 Auto：按场景里实际存在哪个节拍脚本自动选。
/// 场景 1 只有 Tap，场景 2 只有 TapSec，所以两边都不用配。
/// 选错来源的症状很隐蔽——那一侧没有实例在跑计时器，beattime 永远停在初值，
/// 于是进度 p 恒为 0，心跳就一直是"最小幅度 + 最慢周期"，看起来像坏了。
[DefaultExecutionOrder(100)] // 等玩家脚本完成模式交接后，再读取自主心跳计时。
public class Heartbeat : MonoBehaviour
{
    public enum BeatSource { Auto, Tap, TapSec }

    [Header("节拍来源")]
    [Tooltip("Auto = 按场景里实际存在的节拍脚本自动选（推荐，两个场景都不用配）")]
    public BeatSource source = BeatSource.Auto;

    [Header("幅度：beattime 越接近 0 越大")]
    [Tooltip("刚进入这一拍时的缩放幅度")]
    public float minAmplitude = 0.05f;
    [Tooltip("beattime 快归零时的缩放幅度（负值 = 收缩，更像心脏）")]
    public float maxAmplitude = 0.35f;

    [Header("频率：beattime 越接近 0 越快")]
    [Tooltip("刚进入这一拍时，一次心跳的周期（秒）")]
    public float slowPeriod = 0.7f;
    [Tooltip("beattime 快归零时的周期（秒）")]
    public float fastPeriod = 0.12f;

    [Header("静止期")]
    [Tooltip("完美窗口内保持的大小比例。小于 1 会缩下去不动，和跳动期对比更明显")]
    public float restScale = 0.85f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Perfect调试计时（F3显示／隐藏）")]
    [Tooltip("仅编辑器与Development Build显示；读取计时，不改变判定")]
    public bool showPerfectTimer = true;
    [Range(12, 28)] public int perfectTimerFontSize = 18;
    string debugTimerText;
    bool debugPerfectActive;
    GUIStyle debugTimerStyle;
#endif

    [Header("同步心跳声")]
    public bool enableHeartbeatSound = true;
    [Range(0f, 1f)] public float heartbeatVolume = .55f;
    HeartbeatAudioOutput audioOutput;
    long freePulseCycle, audioGeneration;
    int previousAudioMode = -1;
    float previousAudioBeat;
    float previousAudioPerfect;
    double previousAudioDsp, lastOutputDelay = -1d;
    Tap timingTap;
    TapSec timingTapSec;
    public string[] AudioTrace => audioOutput != null ? audioOutput.Trace : new string[0];
    public int AudioScheduleVersion => HeartbeatAudioTimeline.Version;

    Vector3 baseScale;
    float phase;           // 0~1 的心跳相位
    readonly HeartbeatClockWatch clockWatch=new HeartbeatClockWatch();
    bool wasMusic;
    float blendRemaining;
    Vector3 lastMusicScale;
    float lastMusicPeriod = 0.7f;
    float frequencyBlendElapsed;
    bool frequencyBlendActive;
    float visualPeriodForDebug;
    bool sampledMusicMode;
    MusicBeatFrame sampledMusic;


    // 只在真正跳动时推进交接；Perfect静止期间不会耗掉这段频率过渡。
    float BlendFreePeriod(float targetPeriod)
    {
        if (!frequencyBlendActive) return targetPeriod;
        float duration = Mathf.Max(0.02f, lastMusicPeriod);
        float result = Mathf.Lerp(lastMusicPeriod, targetPeriod,
            Mathf.Clamp01(frequencyBlendElapsed / duration));
        frequencyBlendElapsed += Time.deltaTime;
        if (frequencyBlendElapsed >= duration) frequencyBlendActive = false;
        return result;
    }

    // 统一的读数入口 —— 两个来源的字段名是一样的
    float BeatTime    => StoryDirector.Active?StoryDirector.Current.Clock.BeatRemaining:source == BeatSource.TapSec ? TapSec.beattime : Tap.beattime;
    float SetBeatTime => StoryDirector.Active?StoryDirector.Current.Clock.Wait:source == BeatSource.TapSec ? TapSec.Setbeattime : Tap.Setbeattime;
    float PerfectTime => StoryDirector.Active?StoryDirector.Current.Clock.PerfectRemaining:source == BeatSource.TapSec ? TapSec.perfecttime : Tap.perfecttime;
    float NextBeatTime => StoryDirector.Active?StoryDirector.Current.Clock.NextWait:source==BeatSource.TapSec?TapSec.oribeattime:Tap.oribeattime;
    float PerfectWindow => StoryDirector.Active?StoryDirector.Current.Clock.Window:source==BeatSource.TapSec?TapSec.Setperfecttime:Tap.Setperfecttime;
    bool FreeRest => !StoryDirector.Active&&source==BeatSource.Tap&&Tap.IsResting;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void Start()
    {
        if (enableHeartbeatSound) audioOutput = new HeartbeatAudioOutput(gameObject);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (StoryDirector.Active) showPerfectTimer = false;
#endif
        timingTap = FindFirstObjectByType<Tap>();
        timingTapSec = FindFirstObjectByType<TapSec>();
        if(StoryDirector.Active)return; // CampaignBeatClock is authoritative, even while scene scripts are absent.
        if (source != BeatSource.Auto) return;

        // 哪个节拍脚本在这个场景里存在，就用哪个
        bool hasTap = FindObjectsByType<Tap>(FindObjectsSortMode.None).Length > 0;
        source = hasTap ? BeatSource.Tap : BeatSource.TapSec;

        Debug.Log($"[Heartbeat] 自动选定节拍来源：{source}");
    }

    void LateUpdate()
    {
        if (CampaignSession.Suspended || DebugConsole.SimulationPaused)
        {
            clockWatch.Reset();
            audioOutput?.Stop();
            transform.localScale=CampaignSession.Counting?baseScale*(1f+maxAmplitude*Pulse(CampaignSession.CountPhase)):baseScale*restScale;
            return;
        }
        if (StoryDirector.Active && !StoryDirector.HeartbeatRunning)
        { clockWatch.Reset();audioOutput?.Stop(); transform.localScale = baseScale * restScale; return; }
        UpdateHeartbeat();
        UpdateAudioPlan();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UpdatePerfectDebug();
#endif
    }

    void UpdateHeartbeat()
    {
        sampledMusicMode = !StoryDirector.Active&&RhythmManager.TryGetMusicFrame(out sampledMusic);
        if (sampledMusicMode)
        {
            MusicBeatFrame music = sampledMusic;
            bool gap = HeartbeatPulseTiming.IsPerfectGap(music.BeatTime, music.PerfectTime);
            bool moving = music.Started && !gap && HeartbeatPulseTiming.CanPulse(music.BeatTime)
                && music.HasPulseInterval;
            // 真正的Perfect窗口仍缩小静止；双搏动只占用窗口之间的可用时间。
            float pulse = moving ? HeartbeatPulseTiming.Pulse(music.PulsePhase, true) : 0f;
            transform.localScale = gap ? baseScale * restScale
                : baseScale * (1f + maxAmplitude * music.VisualStrength * pulse);
            lastMusicPeriod = Mathf.Max(0.02f, music.Interval);
            visualPeriodForDebug = lastMusicPeriod;
            frequencyBlendActive = false;
            wasMusic = true;
            lastMusicScale = transform.localScale;
            phase = 0f;
            clockWatch.Reset();
            return;
        }
        if (wasMusic)
        {
            wasMusic = false;
            blendRemaining = 0.15f;
            frequencyBlendElapsed = 0f;
            frequencyBlendActive = true;
        }
        // 休息只限制建造输入。自主模式用稳定的基础节拍继续搏动，
        // 不把冻结的beattime误当作不断逼近Perfect的高速脉冲；音乐模式仍优先跟DSP。
        if (FreeRest)
        {
            float restPeriod = BlendFreePeriod(Mathf.Max(0.5f, SetBeatTime));
            visualPeriodForDebug = restPeriod;
            AdvancePulsePhase(restPeriod);
            clockWatch.Reset();
            SetFreeScale(baseScale * (1f + maxAmplitude * Pulse(phase)));
            return;
        }
        float beat = BeatTime;
        float setBeat = SetBeatTime;

        if (clockWatch.Observe(beat,Time.deltaTime,Time.timeScale>0))
        {
            Debug.LogWarning(StoryDirector.Active?
                "[Heartbeat] CampaignBeatClock 在实际运行的5秒内未推进，请检查战役阶段和时钟。":
                $"[Heartbeat] 节拍来源 {source} 在实际运行的5秒内未变化，请检查该场景计时组件是否启用。");
        }

        // 完美窗口（beattime 已归零、perfecttime 还没超时）：完全静止
        if (!HeartbeatPulseTiming.CanPulse(beat))
        {
            phase = 0f;                     // 相位清零，下一拍从第一下重新开始
            // Perfect必须立即静止，不再套用音乐结束时的缩放插值。
            transform.localScale = HeartbeatPulseTiming.IsPerfectGap(beat, PerfectTime)
                ? baseScale * restScale : baseScale;
            return;
        }

        // p：0 → 1，代表 beattime 从 Setbeattime 递减到 0 的进度
        float p = 1f - Mathf.Clamp01(beat / Mathf.Max(0.01f, setBeat));

        // 越接近归零：幅度越大、周期越短 —— 心跳逐渐变重变快
        float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, p);
        float period = BlendFreePeriod(Mathf.Lerp(slowPeriod, fastPeriod, p));
        visualPeriodForDebug = period;

        AdvancePulsePhase(period);

        SetFreeScale(baseScale * (1f + amplitude * Pulse(phase)));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void UpdatePerfectDebug()
    {
        if (!DebugConsole.CapturingInput && Input.GetKeyDown(KeyCode.F3)) showPerfectTimer = !showPerfectTimer;
        if (!showPerfectTimer) return;
        if (!DebugConsole.CapturingInput && Input.GetKeyDown(KeyCode.F6)) RhythmManager.AdjustOutputDelay(-10f);
        if (!DebugConsole.CapturingInput && Input.GetKeyDown(KeyCode.F7)) RhythmManager.AdjustOutputDelay(10f);

        // 每帧在玩家逻辑之后读取一次；OnGUI只绘制快照，不推进或重置计时。
        // 和本帧声音、缩放共用一次采样，避免DSP跨窗口后面板显示下一状态。
        bool musicMode = sampledMusicMode;
        MusicBeatFrame music = sampledMusic;
        float beat = musicMode ? music.BeatTime : BeatTime;
        float perfect = musicMode ? music.PerfectTime : PerfectTime;
        bool resting = FreeRest;
        debugPerfectActive = musicMode ? music.Started && music.IsPerfect
            : !resting && beat <= 0f && perfect > 0f;
        bool hasNext = !musicMode || music.HasNextPerfect;
        string next = resting && !musicMode ? "PAUSED (REST)"
            : debugPerfectActive ? "0.000 s"
            : hasNext ? Mathf.Max(0f, beat).ToString("F3") + " s" : "-- (no more accents)";
        string state = resting && !musicMode ? "REST (FREE PULSE)"
            : musicMode && !music.Started ? "PREROLL"
            : debugPerfectActive ? "PERFECT: SILENT / STILL" : "BEATTIME: HEARTBEAT";
        debugTimerText = "PERFECT DEBUG v3  [F3: show / hide]\n"
            + "Output estimate: " + (RhythmManager.OutputDelaySeconds * 1000d).ToString("F0") + " ms"
            + "   extra " + RhythmManager.ExtraOutputDelayMs.ToString("F0") + " ms\n"
            + "F6: -10ms    F7: +10ms (sound late: F7)\n"
            + "Scheduled: " + (audioOutput != null ? audioOutput.ScheduledCount : 0)
            + "    late skipped: " + (audioOutput != null ? audioOutput.LateSkipped : 0) + "\n"
            + "Mode: " + (musicMode ? (music.UsesDynamicBpm ? "DYNAMIC BPM" : "MUSIC ACCENTS") : "FREE") + "    Source: " + (StoryDirector.Active?"CampaignBeatClock":source.ToString()) + "\n"
            + "Tempo: " + (musicMode ? music.CurrentBpm.ToString("F1") + " BPM"
                : visualPeriodForDebug > 0f ? (60f / visualPeriodForDebug).ToString("F1") + " visual BPM" : "--")
                + (musicMode ? "   +/- " + music.WindowHalfWidth.ToString("F3") + " s" : "") + "\n"
            + "Window: " + state + "\n"
            + "Perfect left: " + (debugPerfectActive ? Mathf.Max(0f, perfect).ToString("F3") + " s" : "--") + "\n"
            + "Next window: " + next + "\n"
            + "raw perfecttime: " + perfect.ToString("F3") + " s\n"
            + "raw beattime: " + beat.ToString("F3") + " s\n"
            + "Music beat index: " + (musicMode ? music.BeatIndex.ToString() + " (0-based)" : "--") + "\n"
            + (resting ? "REST: QTE input blocked" : "Timing only; gameplay input locks still apply.")
            + (source == BeatSource.Tap ? "\nQTE last: " + Tap.LastInputDebug : "");
    }

    void OnGUI()
    {
        if (StoryDirector.Active && !StoryDirector.GameplayRunning) return;
        if (!showPerfectTimer || string.IsNullOrEmpty(debugTimerText)) return;
        if (debugTimerStyle == null) debugTimerStyle = new GUIStyle(GUI.skin.label) { font = GameFont.Silver };
        debugTimerStyle.fontSize = perfectTimerFontSize;
        debugTimerStyle.wordWrap = true;
        debugTimerStyle.normal.textColor = debugPerfectActive ? new Color(0.4f, 1f, 0.5f) : Color.white;
        float width = Mathf.Max(240f, Mathf.Min(480f, Screen.width - 24f));
        float height = perfectTimerFontSize * 1.65f * (source == BeatSource.Tap ? 16f : 13f) + 24f;
        GUI.Box(new Rect(12f, 12f, width, height), GUIContent.none);
        GUI.Label(new Rect(24f, 22f, width - 24f, height - 20f), debugTimerText, debugTimerStyle);
    }
#endif


    void SetFreeScale(Vector3 target)
    {
        if (blendRemaining <= 0f) { transform.localScale = target; return; }
        blendRemaining = Mathf.Max(0f, blendRemaining - Time.deltaTime);
        transform.localScale = Vector3.Lerp(lastMusicScale, target, 1f - blendRemaining / 0.15f);
    }

    // 一次"咚·咚"：u ≈ 0.12 重一下，u ≈ 0.30 补一下轻的。
    // 刻意让 u = 0 处接近 0，这样每拍重新开始时不会突然闪一下
    static float Pulse(float u) => HeartbeatPulseTiming.Pulse(u, false);

    void AdvancePulsePhase(float period)
    {
        float advanced = phase + Time.deltaTime / Mathf.Max(.02f, period);
        freePulseCycle += Mathf.FloorToInt(advanced);
        phase = Mathf.Repeat(advanced, 1f);
    }

    void UpdateAudioPlan()
    {
        bool clockRunning = StoryDirector.Active?StoryDirector.HeartbeatRunning:sampledMusicMode || (source == BeatSource.Tap
            ? timingTap != null && timingTap.isActiveAndEnabled && ConstructionStageFlow.CanAcceptInput(timingTap)
            : timingTapSec != null && timingTapSec.isActiveAndEnabled);
        if (!enableHeartbeatSound || Time.timeScale <= 0f || AudioListener.pause || !clockRunning)
        {
            if (audioOutput != null) audioOutput.Stop();
            previousAudioMode = -1;
            return;
        }
        if (audioOutput == null) audioOutput = new HeartbeatAudioOutput(gameObject);
        double now = AudioSettings.dspTime, delay = RhythmManager.OutputDelaySeconds;
        audioOutput.BeginFrame(now, delay, heartbeatVolume * SfxPlayer.MasterVolume);
        bool rest = !sampledMusicMode && FreeRest;
        bool gap = !sampledMusicMode && HeartbeatPulseTiming.IsPerfectGap(BeatTime, PerfectTime);
        int mode = sampledMusicMode ? 1 : rest ? 2 : gap ? 3 : BeatTime > 0f ? 4 : 5;
        if (lastOutputDelay >= 0d && System.Math.Abs(lastOutputDelay - delay) > .0005d)
            audioOutput.Stop();
        lastOutputDelay = delay;
        if (previousAudioMode >= 0 && ((previousAudioMode == 1) != (mode == 1)
            || (previousAudioMode == 2) != (mode == 2)))
        { audioOutput.CancelFuture(); audioGeneration++; }
        else if (previousAudioMode == 3 && mode == 4)
        {
            // Perfect内按键会提前结束间歇，必须撤销按自然结束时间预测的下一拍。
            if (previousAudioPerfect > (now - previousAudioDsp) * Time.timeScale + .002d)
                audioOutput.CancelFuture();
            audioGeneration++; freePulseCycle = 0L;
        }
        else if (previousAudioMode == 4 && mode == 4 && BeatTime > previousAudioBeat + .025f)
        { audioOutput.CancelFuture(); audioGeneration++; }
        previousAudioMode = mode; previousAudioBeat = BeatTime;
        previousAudioPerfect = PerfectTime; previousAudioDsp = now;
        double horizon = now + delay + audioOutput.MinimumLead + .16d;
        if (sampledMusicMode)
        {
            // 背景音乐和心跳均经过同一输出延迟。声音排原始DSP拍点；
            // 画面/判定读“DSP减输出延迟”，不能再把心跳单独平移两遍。
            RhythmManager.VisitHeartbeatPulses(now + audioOutput.MinimumLead, now + audioOutput.MinimumLead + .16d,
                (peak, end, cycle, hit, strength) => audioOutput.Submit(audioGeneration, cycle, hit,
                    true, peak + delay, end + delay, strength));
            return;
        }
        float scale = Time.timeScale;
        if (rest)
        {
            var span = FreeSpan(now, horizon, freePulseCycle + phase, BeatTime, SetBeatTime, true);
            SubmitFreeSpan(span, audioGeneration, horizon);
            return;
        }
        if (gap)
        {
            // Perfect期间允许提前提交“下一段beattime”的声音，届时才会到耳朵。
            // 此时不能对声源粗暴Stop，否则蓝牙永远来不及追上画面。
            double nextStart = now + PerfectTime / scale;
            float nextBeat = Mathf.Max(.05f,NextBeatTime);
            SubmitFreeSpan(FreeSpan(nextStart, nextStart + nextBeat / scale, 0d, nextBeat, nextBeat, false),
                audioGeneration + 1L, horizon);
            return;
        }
        if (BeatTime <= 0f) { audioOutput.CancelFuture(); return; }
        double endNow = now + BeatTime / scale;
        SubmitFreeSpan(FreeSpan(now, endNow, freePulseCycle + phase, BeatTime, SetBeatTime, false),
            audioGeneration, horizon);
        double followingStart = endNow + PerfectWindow / scale;
        float followingBeat = Mathf.Max(.05f,NextBeatTime);
        SubmitFreeSpan(FreeSpan(followingStart, followingStart + followingBeat / scale,
            0d, followingBeat, followingBeat, false), audioGeneration + 1L, horizon);
    }

    HeartbeatAudioTimeline.FreeSpan FreeSpan(double start, double end, double startPhase,
        float remaining, float setBeat, bool rest)
    {
        return new HeartbeatAudioTimeline.FreeSpan
        {
            Start = start, End = end, Phase = startPhase, Beat = remaining, SetBeat = setBeat,
            Slow = slowPeriod, Fast = fastPeriod, TimeScale = Time.timeScale, Rest = rest,
            RestPeriod = Mathf.Max(.5f, setBeat), Blending = frequencyBlendActive,
            BlendOrigin = lastMusicPeriod, BlendElapsed = frequencyBlendElapsed,
            BlendDuration = Mathf.Max(.02f, lastMusicPeriod)
        };
    }

    void SubmitFreeSpan(HeartbeatAudioTimeline.FreeSpan span, long generation, double horizon)
    {
        span.Visit(horizon, (peak, cycle, hit) =>
        {
            float progress = 1f - Mathf.Clamp01((span.Beat - (float)(peak - span.Start) * span.TimeScale)
                / Mathf.Max(.01f, span.SetBeat));
            // 初段也保持可听，避免只有临近Perfect的最后一跳被听见。
            audioOutput.Submit(generation, cycle, hit, false, peak, span.End,
                span.Rest ? 1f : Mathf.Lerp(.45f, 1f, progress));
        });
    }

    void OnDisable()
    {
        clockWatch.Reset();
        if (audioOutput != null) audioOutput.Stop();
        previousAudioMode = -1;
    }

    void OnDestroy()
    {
        if (audioOutput != null) audioOutput.Dispose();
    }

    static float Bump(float u, float center, float width)
    {
        float x = (u - center) / width;
        return Mathf.Exp(-x * x);          // 一个平滑的鼓包
    }
}
