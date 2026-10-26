using UnityEngine;
using UnityEngine.UI;

public class Tap : MonoBehaviour
{
    public event System.Action<bool> InputAccepted;
    public event System.Action<KeyCode> GraceInputReceived;

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

    static KeyCode[] QteKeys => new[]{GameBindings.Key(GameAction.BuildA),GameBindings.Key(GameAction.BuildD),GameBindings.Key(GameAction.BuildW),GameBindings.Key(GameAction.BuildS)};
    static Tap activeTap;
    public static bool IsResting => (StoryDirector.Active && StoryDirector.Current.IsConstructionResting) || activeTap != null && activeTap.isActiveAndEnabled && activeTap.restTimer > 0f;

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
            previousPromptGrace = GameOptions.PromptGraceEnabled ? (float)ConstructionJudgement.PromptGraceSeconds : 0;
        }
        lastKeyIndex = next;
        KeyCode chosen = QteKeys[next];
        if (WordUI != null) WordUI.text = chosen.ToString();
        return chosen;
    }

    void Start()
    {
        ParisSceneUI.Construction(this);
        charge = GetComponent<WallCharge>();
        // The campaign owns its prompt. Do not briefly expose an unrelated random letter at scene entry.
        if(StoryDirector.Active)
        {
            if(WordUI!=null)WordUI.text="";
            StoryDirector.Current.ConstructionInput(this);
        }
        else Key = Randoml();
        ShowCount();
    }

    // 只把游戏操作键作为QTE尝试，点击Game窗口或其他非操作键不触发休息。
    static bool HasQteInput() => !DebugConsole.CapturingInput && (GameBindings.Down(GameAction.BuildA) || GameBindings.Down(GameAction.BuildD)
        || GameBindings.Down(GameAction.BuildW) || GameBindings.Down(GameAction.BuildS) || Input.GetKeyDown(KeyCode.Space));

    public void NotifyCampaignInput(bool perfect,float pitch=1f)
    {
        if (perfect) cnt++;
        ShowCount(); InputAccepted?.Invoke(perfect);
        SfxPlayer.Play(perfect ? "SFX-QTE-01" : "SFX-QTE-02", .5f,pitch);
    }

    public void NotifyGraceInput(KeyCode key)
    {
        lastInputDebug="prompt grace: "+key+" acknowledged; no score or penalty";
        InputAccepted?.Invoke(false);GraceInputReceived?.Invoke(key);
        if(WordUI!=null)WordUI.GetComponent<KeyPulse>()?.Pulse();
        SfxPlayer.Play("SFX-QTE-02",.3f);
    }

    void Update()
    {
        if (StoryDirector.Active) { StoryDirector.Current.ConstructionInput(this); return; }
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
        GameManager.AddWallHP(GameDifficultyRules.ConstructionReward(GameOptions.Difficulty,false,5));
        SfxPlayer.Play("SFX-QTE-02", 0.45f);
        Key = Randoml();
        InputAccepted?.Invoke(false);
    }

    void HandleWrongInput(bool followsMusic)
    {
        // 换题保护内的旧字母给予动作和提示反馈，不奖励，也不触发休息。
        // 正确的新字母始终立即响应；其他错字母照常惩罚。
        if (GameOptions.PromptGraceEnabled && previousPromptGrace > 0f && Input.GetKeyDown(previousPromptKey))
        {
            NotifyGraceInput(previousPromptKey);
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
        if (cntui != null) cntui.text = "合拍 "+cnt;
    }
}
