using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// 场景1独立计时：建造结束后停止输入，再为动员过场保留播放时间。
[DefaultExecutionOrder(-150)]
public class ConstructionStageFlow : MonoBehaviour
{
    [Header("建造与过场")]
    [Min(0f)] public float gameplaySeconds = 100f;
    [Tooltip("尚未配置Timeline时，预留多少秒后进入场景2")]
    [Min(0f)] public float placeholderCutsceneSeconds = 10f;
    public Tap qte;
    public StageMusicTransition transition;
    public PlayableDirector cutsceneDirector;

    enum StageState { Building, Cutscene, Loading }
    static ConstructionStageFlow current;
    StageState state;
    bool hasStarted, usingTimeline, loadFailureReported;
    float buildEndsAt, cutsceneEndsAt;

    public event System.Action CutsceneStarting;

    public float RemainingSeconds => hasStarted ? Mathf.Max(0f, buildEndsAt - Time.timeSinceLevelLoad) : gameplaySeconds;
    public bool IsBuilding => hasStarted && state == StageState.Building && Time.timeSinceLevelLoad < buildEndsAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRun() => current = null;

    void Awake()
    {
        if (gameObject.scene.name != "1")
        {
            Debug.LogWarning("[ConstructionStageFlow] 仅用于场景1，已禁用。");
            enabled = false;
            return;
        }
        if (current != null && current != this && current.gameObject.scene.handle == gameObject.scene.handle)
        {
            Debug.LogWarning("[ConstructionStageFlow] 场景1存在重复流程组件，已禁用重复项。");
            enabled = false;
            return;
        }
        current = this;
        if (cutsceneDirector == null) cutsceneDirector = GetComponent<PlayableDirector>();
        if (cutsceneDirector != null) cutsceneDirector.playOnAwake = false;
    }

    void Start()
    {
        if (!enabled) return;
        if (transition == null) transition = GetComponent<StageMusicTransition>();
        if (qte == null)
        {
            foreach (Tap candidate in FindObjectsByType<Tap>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene.handle == gameObject.scene.handle) { qte = candidate; break; }
        }
        if (qte == null) Debug.LogWarning("[ConstructionStageFlow] 场景1没有找到Tap，请检查QTE引用。");
        buildEndsAt = Time.timeSinceLevelLoad + ValidSeconds(gameplaySeconds, 100f);
        state = StageState.Building;
        hasStarted = true;
    }

    static float ValidSeconds(float seconds, float fallback) =>
        float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f ? fallback : seconds;

    // 输入端也检查截止时间，避免恰好第100秒时受Update执行顺序影响多算一次建造。
    public static bool CanAcceptInput(Tap sender) => current == null
        || current.gameObject.scene.handle != sender.gameObject.scene.handle || current.IsBuilding;

    void Update()
    {
        if (!hasStarted) return;
        if (state == StageState.Building && Time.timeSinceLevelLoad >= buildEndsAt) BeginCutscene();
        if (state == StageState.Cutscene && !usingTimeline && Time.timeSinceLevelLoad >= cutsceneEndsAt)
            FinishCutscene();
    }

    void BeginCutscene()
    {
        state = StageState.Cutscene;
        SfxPlayer.Play("SFX-SYS-03", 0.6f);
        CutsceneStarting?.Invoke();
        if (qte != null && qte.WordUI != null) qte.WordUI.gameObject.SetActive(false);
        if (transition != null) transition.BeginMusic();
        else RhythmManager.PrepareMusicForScene("2");

        double duration = cutsceneDirector != null && cutsceneDirector.playableAsset != null
            ? cutsceneDirector.playableAsset.duration : 0d;
        usingTimeline = duration > 0d && !double.IsNaN(duration) && !double.IsInfinity(duration);
        if (usingTimeline)
        {
            cutsceneDirector.extrapolationMode = DirectorWrapMode.None;
            cutsceneDirector.timeUpdateMode = DirectorUpdateMode.GameTime;
            cutsceneDirector.time = 0d;
            cutsceneDirector.stopped += OnDirectorStopped;
            cutsceneDirector.Play();
            Debug.Log("[ConstructionStageFlow] 建造已结束，播放Timeline，结束后进入场景2。");
        }
        else
        {
            cutsceneEndsAt = Time.timeSinceLevelLoad + ValidSeconds(placeholderCutsceneSeconds, 10f);
            Debug.Log("[ConstructionStageFlow] 建造已结束，当前未配置过场动画，预留 "
                + ValidSeconds(placeholderCutsceneSeconds, 10f) + " 秒后进入场景2。");
        }
    }

    void OnDirectorStopped(PlayableDirector director)
    {
        if (director == cutsceneDirector && isActiveAndEnabled
            && SceneManager.GetActiveScene().handle == gameObject.scene.handle) FinishCutscene();
    }

    /// 可由Timeline末尾的Signal提前完成；重复调用不会重复加载或重播音乐。
    public void FinishCutscene()
    {
        if (!hasStarted || state != StageState.Cutscene) return;
        string destination = transition != null ? transition.nextSceneName : "2";
        if (string.IsNullOrEmpty(destination) || !Application.CanStreamedLevelBeLoaded(destination))
        {
            if (!loadFailureReported) Debug.LogWarning("[ConstructionStageFlow] 目标场景未加入Build Settings：" + destination);
            loadFailureReported = true;
            return;
        }
        state = StageState.Loading;
        if (cutsceneDirector != null) cutsceneDirector.stopped -= OnDirectorStopped;
        if (transition != null) transition.GoToNextScene();
        else RhythmManager.LoadSceneWithMusic("2");
    }

    void OnDisable()
    {
        if (cutsceneDirector != null) cutsceneDirector.stopped -= OnDirectorStopped;
    }

    void OnDestroy()
    {
        OnDisable();
        if (current == this) current = null;
    }
}
