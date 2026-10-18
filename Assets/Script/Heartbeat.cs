using UnityEngine;

/// 心跳效果。挂到想搏动的物体上，直接读节拍脚本的静态计时器，不需要拖引用。
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

    Vector3 baseScale;
    float phase;           // 0~1 的心跳相位
    float prevBeat = -1f;
    bool beatMoved;        // 见过 beattime 变化（用来判断来源选对没有）
    bool warned;
    bool wasMusic;
    float blendRemaining;
    Vector3 lastMusicScale;
    float lastMusicPeriod = 0.7f;
    float frequencyBlendElapsed;
    bool frequencyBlendActive;
    float visualPeriodForDebug;
    float lastMusicPhase = -1f;

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
    float BeatTime    => source == BeatSource.TapSec ? TapSec.beattime    : Tap.beattime;
    float SetBeatTime => source == BeatSource.TapSec ? TapSec.Setbeattime : Tap.Setbeattime;
    float PerfectTime => source == BeatSource.TapSec ? TapSec.perfecttime : Tap.perfecttime;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void Start()
    {
        if (source != BeatSource.Auto) return;

        // 哪个节拍脚本在这个场景里存在，就用哪个
        bool hasTap = FindObjectsByType<Tap>(FindObjectsSortMode.None).Length > 0;
        source = hasTap ? BeatSource.Tap : BeatSource.TapSec;

        Debug.Log($"[Heartbeat] 自动选定节拍来源：{source}");
    }

    void Update()
    {
        if (RhythmManager.TryGetMusicFrame(out MusicBeatFrame music))
        {
            // 用DSP拍点驱动画面；强拍Perfect窗口内采用与自主模式一致的缩小静止提示。
            float u = music.Phase;
            float pulse = music.HasVisualBeat
                ? Bump(u, 0f, 0.055f) + Bump(u, 1f, 0.055f) + 0.55f * Bump(u, 0.18f, 0.055f)
                : 0f;
            // 静止间歇与真正的Perfect资格共用同一判定，弱拍不会误停。
            transform.localScale = music.IsPerfect
                ? baseScale * restScale
                : baseScale * (1f + maxAmplitude * music.VisualStrength * pulse);
            // 相位回绕 = 新的一拍；强拍再叠一层重音。
            if (lastMusicPhase >= 0f && music.Phase < lastMusicPhase - 0.5f)
            {
                SfxPlayer.Play("SFX-HRT-01", 0.55f);
                if (music.VisualStrength >= 1f) SfxPlayer.Play("SFX-HRT-04", 0.45f);
            }
            lastMusicPhase = music.Phase;
            lastMusicPeriod = Mathf.Max(0.02f, music.Interval);
            visualPeriodForDebug = lastMusicPeriod;
            frequencyBlendActive = false;
            wasMusic = true;
            lastMusicScale = transform.localScale;
            phase = 0f;
            beatMoved = true;
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
        if (source == BeatSource.Tap && Tap.IsResting)
        {
            float restPeriod = BlendFreePeriod(Mathf.Max(0.5f, SetBeatTime));
            visualPeriodForDebug = restPeriod;
            phase = Mathf.Repeat(phase + Time.deltaTime / restPeriod, 1f);
            beatMoved = true;
            prevBeat = BeatTime;
            SetFreeScale(baseScale * (1f + maxAmplitude * Pulse(phase)));
            return;
        }
        float beat = BeatTime;
        float setBeat = SetBeatTime;

        // 来源自检：5 秒内 beattime 一次都没变过，说明这个来源没在跑
        if (prevBeat >= 0f && beat != prevBeat) beatMoved = true;
        // 自主模式：beattime 从 0 跳回 Setbeattime 就是新的一拍。
        if (prevBeat >= 0f && beat > prevBeat + 0.01f) SfxPlayer.Play("SFX-HRT-01", 0.5f);
        prevBeat = beat;

        if (!warned && !beatMoved && Time.time > 5f)
        {
            warned = true;
            Debug.LogWarning($"[Heartbeat] 节拍来源是 {source}，但它的 beattime 在 5 秒内一次都没变。" +
                             $"检查一下场景里到底挂的是 Tap 还是 TapSec。");
        }

        // 完美窗口（beattime 已归零、perfecttime 还没超时）：完全静止
        if (beat <= 0f && PerfectTime > 0f)
        {
            phase = 0f;                     // 相位清零，下一拍从第一下重新开始
            SetFreeScale(baseScale * restScale);
            return;
        }

        // p：0 → 1，代表 beattime 从 Setbeattime 递减到 0 的进度
        float p = 1f - Mathf.Clamp01(beat / Mathf.Max(0.01f, setBeat));

        // 越接近归零：幅度越大、周期越短 —— 心跳逐渐变重变快
        float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, p);
        float period = BlendFreePeriod(Mathf.Lerp(slowPeriod, fastPeriod, p));
        visualPeriodForDebug = period;

        phase = Mathf.Repeat(phase + Time.deltaTime / Mathf.Max(0.02f, period), 1f);

        SetFreeScale(baseScale * (1f + amplitude * Pulse(phase)));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F3)) showPerfectTimer = !showPerfectTimer;
        if (!showPerfectTimer) return;

        // 每帧在玩家逻辑之后读取一次；OnGUI只绘制快照，不推进或重置计时。
        bool musicMode = RhythmManager.TryGetMusicFrame(out MusicBeatFrame music);
        float beat = musicMode ? music.BeatTime : BeatTime;
        float perfect = musicMode ? music.PerfectTime : PerfectTime;
        debugPerfectActive = musicMode ? music.Started && music.IsPerfect : beat <= 0f && perfect > 0f;
        bool resting = source == BeatSource.Tap && Tap.IsResting;
        bool hasNext = !musicMode || music.HasNextPerfect;
        string next = resting && !musicMode ? "PAUSED (REST)"
            : debugPerfectActive ? "0.000 s"
            : hasNext ? Mathf.Max(0f, beat).ToString("F3") + " s" : "-- (no more accents)";
        string state = musicMode && !music.Started ? "PREROLL"
            : debugPerfectActive ? "PERFECT ACTIVE" : "WAIT";
        debugTimerText = "PERFECT DEBUG  [F3: show / hide]\n"
            + "Mode: " + (musicMode ? (music.UsesDynamicBpm ? "DYNAMIC BPM" : "MUSIC ACCENTS") : "FREE") + "    Source: " + source + "\n"
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
        if (!showPerfectTimer || string.IsNullOrEmpty(debugTimerText)) return;
        if (debugTimerStyle == null) debugTimerStyle = new GUIStyle(GUI.skin.label);
        debugTimerStyle.fontSize = perfectTimerFontSize;
        debugTimerStyle.wordWrap = true;
        debugTimerStyle.normal.textColor = debugPerfectActive ? new Color(0.4f, 1f, 0.5f) : Color.white;
        float width = Mathf.Max(240f, Mathf.Min(480f, Screen.width - 24f));
        float height = perfectTimerFontSize * 1.65f * (source == BeatSource.Tap ? 13f : 10f) + 24f;
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
    static float Pulse(float u)
    {
        return Bump(u, 0.12f, 0.07f) + 0.55f * Bump(u, 0.30f, 0.07f);
    }

    static float Bump(float u, float center, float width)
    {
        float x = (u - center) / width;
        return Mathf.Exp(-x * x);          // 一个平滑的鼓包
    }
}
