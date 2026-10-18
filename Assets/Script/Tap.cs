using UnityEngine;
using UnityEngine.UI;

public class Tap : MonoBehaviour
{
    public event System.Action<bool> InputAccepted;

    public Text WordUI;
    public Text cntui;

    public static float beattime = 0.85f;
    public static float oribeattime = 0.85f;
    public static float Setbeattime = 0.85f;
    public static float perfecttime = 0.5f;
    public static float Setperfecttime = 0.5f;

    [Header("错按惩罚")]
    [Tooltip("只有按错当前提示字母才休息；正确但非Perfect不触发休息，心跳和音乐继续")]
    public float restTime = 3f;

    static readonly KeyCode[] QteKeys = { KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S };
    static Tap activeTap;
    public static bool IsResting => activeTap != null && activeTap.isActiveAndEnabled && activeTap.restTimer > 0f;

    float restTimer;
    KeyCode previousPromptKey;
    float previousPromptGrace;
    bool frameUsesMusic;
    string lastInputDebug = "no input yet";
    public static string LastInputDebug => activeTap != null ? activeTap.lastInputDebug : "--";
    int cnt, lastKeyIndex = -1;
    KeyCode Key;
    WallCharge charge;
    int seenMusicVersion = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRun() => activeTap = null;

    void Awake()
    {
        activeTap = this;
        Setbeattime = Mathf.Max(0.5f, oribeattime);
        beattime = Setbeattime;
        perfecttime = Setperfecttime;
    }

    void OnDestroy()
    {
        if (activeTap == this) activeTap = null;
    }

    KeyCode Randoml()
    {
        // 排除刚显示的字母，让成功和恢复后的提示确实有变化。
        int next = Random.Range(0, QteKeys.Length - (lastKeyIndex >= 0 ? 1 : 0));
        if (lastKeyIndex >= 0 && next >= lastKeyIndex) next++;
        if (lastKeyIndex >= 0)
        {
            previousPromptKey = Key;
            previousPromptGrace = 0.08f;
        }
        lastKeyIndex = next;
        KeyCode chosen = QteKeys[next];
        if (WordUI != null) WordUI.text = chosen.ToString();
        return chosen;
    }

    void Start()
    {
        charge = GetComponent<WallCharge>();
        Key = Randoml();
        ShowCount();
    }

    // 只把游戏操作键作为QTE尝试，点击Game窗口或其他非操作键不触发休息。
    static bool HasQteInput() => Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D)
        || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Space);

    void Update()
    {
        if (!ConstructionStageFlow.CanAcceptInput(this)) return;
        previousPromptGrace = Mathf.Max(0f, previousPromptGrace - Time.deltaTime);
        ShowCount();
        if (RhythmManager.TryGetMusicFrame(out MusicBeatFrame music))
        {
            frameUsesMusic = true;
            Setbeattime = music.Interval;
            beattime = music.BeatTime;
            perfecttime = music.PerfectTime;
            if (AdvanceRest(true) || !music.Started || !HasQteInput()) return;
            if (!Input.GetKeyDown(Key)) HandleWrongInput(true);
            else if (music.IsPerfect)
            {
                if (RhythmManager.TryClaimPerfect(music.BeatIndex)) AcceptPerfect(true);
                else RecordInput("beat already used; ignored");
            }
            else AcceptOrdinary(true);
            return;
        }

        frameUsesMusic = false;
        bool handedOff = RhythmManager.ApplyHandoff(gameObject.scene.name, ref seenMusicVersion,
            ref beattime, ref Setbeattime, ref perfecttime, Setperfecttime);
        if (AdvanceRest(false)) return;
        // 先推进到当前帧，再判定按键；跨过零点这一帧已经属于Perfect窗口。
        bool hasInput = HasQteInput();
        if (!handedOff) AdvanceFreeBeat();
        if (!hasInput) return;
        if (!Input.GetKeyDown(Key)) HandleWrongInput(false);
        else if (beattime <= 0f && perfecttime > 0f) AcceptPerfect(false);
        else AcceptOrdinary(false);
    }

    void AdvanceFreeBeat()
    {
        if (beattime > 0f)
        {
            beattime = Mathf.Max(0f, beattime - Time.deltaTime);
            return;
        }
        perfecttime = Mathf.Max(0f, perfecttime - Time.deltaTime);
        if (perfecttime > 0f) return;
        Setbeattime = Mathf.Max(0.5f, oribeattime);
        beattime = Setbeattime;
        perfecttime = Setperfecttime;
        // 漏拍只重置计时，保留屏幕字母，避免玩家按下刚看到的提示时被突然换题。
        // 仅正确输入或休息恢复才换题。
    }

    void AcceptPerfect(bool followsMusic)
    {
        RecordInput("PERFECT");
        if (!followsMusic)
        {
            Setbeattime = Mathf.Max(0.5f, Setbeattime - 0.05f);
            beattime = Setbeattime;
            perfecttime = Setperfecttime;
        }
        SfxPlayer.Play("SFX-QTE-01", 0.6f);
        cnt++;
        if (charge == null) charge = GetComponent<WallCharge>();
        if (charge != null) charge.OnPerfect();
        Key = Randoml();
        ShowCount();
        InputAccepted?.Invoke(true);
    }

    void AcceptOrdinary(bool followsMusic)
    {
        RecordInput("correct; ordinary");
        // 字母正确但时机不完美：换题，不给Perfect收益，也不进入休息。
        if (!followsMusic)
        {
            Setbeattime = Mathf.Max(0.5f, oribeattime);
            beattime = Setbeattime;
            perfecttime = Setperfecttime;
        }
        SfxPlayer.Play("SFX-QTE-02", 0.45f);
        Key = Randoml();
        InputAccepted?.Invoke(false);
    }

    void HandleWrongInput(bool followsMusic)
    {
        // 换题后短时间内的旧字母重复按下只忽略，不奖励，也不让它误触发休息。
        // 正确的新字母始终立即响应；其他错字母照常惩罚。
        if (previousPromptGrace > 0f && Input.GetKeyDown(previousPromptKey))
        {
            RecordInput("previous prompt repeat; ignored");
            return;
        }
        BeginRest(followsMusic);
    }

    void RecordInput(string result)
    {
        string pressed = "";
        foreach (KeyCode candidate in QteKeys)
            if (Input.GetKeyDown(candidate)) pressed += (pressed.Length > 0 ? "+" : "") + candidate;
        if (Input.GetKeyDown(KeyCode.Space)) pressed += (pressed.Length > 0 ? "+" : "") + "Space";
        lastInputDebug = Time.timeSinceLevelLoad.ToString("F3") + "s "
            + (frameUsesMusic ? "MUSIC" : "FREE") + " expected=" + Key + " pressed=" + pressed + " : " + result;
    }

    void BeginRest(bool followsMusic)
    {
        RecordInput("WRONG -> REST");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[Tap] " + lastInputDebug + "; shown=" + (WordUI != null ? WordUI.text : "<no label>"));
#endif
        SfxPlayer.Play("SFX-QTE-03", 0.6f);
        restTimer = Mathf.Max(0f, restTime);
        if (!followsMusic)
        {
            Setbeattime = Mathf.Max(0.5f, oribeattime);
            beattime = Setbeattime;
            perfecttime = Setperfecttime;
        }
        if (restTimer > 0f) { ShowRest(); SfxPlayer.Play("SFX-QTE-04", 0.35f); }
        else Key = Randoml();
    }

    bool AdvanceRest(bool followsMusic)
    {
        if (restTimer <= 0f) return false;
        restTimer = Mathf.Max(0f, restTimer - Time.deltaTime);
        if (restTimer > 0f) ShowRest();
        else
        {
            if (!followsMusic)
            {
                beattime = Setbeattime;
                perfecttime = Setperfecttime;
            }
            Key = Randoml();
        }
        return true;
    }

    void ShowRest()
    {
        // 现有占位字体没有中文字符；用REST与剩余秒数明确表示正在休息。
        if (WordUI != null) WordUI.text = "REST " + Mathf.CeilToInt(restTimer) + "s";
    }

    void ShowCount()
    {
        if (cntui != null) cntui.text = cnt.ToString();
    }
}
