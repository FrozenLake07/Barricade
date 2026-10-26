using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-300)]
public sealed partial class StoryDirector : MonoBehaviour
{
    public ChapterTable table;
    public CampaignCinematics cinematics;
    public Font font;
    public AudioClip titleMusic, chapterMusic;
    [Header("终章两条结局音乐")]
    public AudioClip mourningMusic, finaleMusic;
    [Min(0), Tooltip("希望结局整首配乐中，转入People Sing段落的时间（秒）；门外镜头对齐此处。")]
    public float hopeFinaleCueSeconds = 64.9f;
    [Header("音乐与心跳的平衡")]
    [Range(0, 1), Tooltip("过场背景音乐音量；不改变总音量或心跳音量。")]
    public float cinematicMusicVolume = .65f;
    [Range(0, 1), Tooltip("建造、防守、选择及尾声中降低音乐，突出心跳。")]
    public float gameplayMusicVolume = .16f;
    [Min(0), Tooltip("音乐变小时的渐变秒数。")]
    public float gameplayMusicFadeSeconds = 2f;
    [Min(0), Tooltip("回到过场或配音结束时的恢复秒数。")]
    public float musicRecoveryFadeSeconds = .6f;
    public GameObject endingPrefab;
    public GameObject screenPrefab;
    public UnityEvent transitionStarted = new UnityEvent(), transitionFinished = new UnityEvent();
    public static event Action<int, string> TransitionRequested;
    public static StoryDirector Current { get; private set; }
    public static bool Active => Current != null;
    public static bool CombatRunning => Active && !CampaignSession.InputBlocked && Current.Phase == CampaignPhase.Defense;
    public static bool GameplayRunning => Active && !CampaignSession.InputBlocked && (Current.Phase == CampaignPhase.Preparation || Current.Phase == CampaignPhase.Defense);
    public static bool HeartbeatRunning => !CampaignSession.InputBlocked && ((Active && Current.PreparationHandoff) || GameplayRunning || (Active && (Current.Phase == CampaignPhase.Choice || Current.Phase == CampaignPhase.Epilogue || Current.Phase == CampaignPhase.LastChance || Current.Phase == CampaignPhase.EndCard || Current.Phase == CampaignPhase.Cinematic)));

    CampaignPhase phase;
    public CampaignPhase Phase
    {
        get => phase;
        private set
        {
            if (phase == value)
                return;
            phase = value;
            telemetry?.PhaseChanged(value);
        }
    }

    public ChapterDefinition Chapter => table.chapters[RunState.Chapter - 1];
    public CampaignBeatClock Clock { get; private set; } = new CampaignBeatClock();
    public int ChapterBeat => phaseOffset + Clock.Opened;
    public int FrozenUntilBeat { get; private set; }
    public bool EnemiesFrozen => !CombatRunning || Clock.Opened < FrozenUntilBeat || CampaignDebug.Value.freeze;
    public float BaseWait => Chapter.startingWait;

    CampaignTelemetry telemetry;
    CampaignUI ui;
    CampaignSession session;
    AudioSource audioSource, transitionAudio;
    bool musicPlaybackActive;
    CampaignMusicFader musicFader;
    public void SetCinematicVoiceActive(bool active)
    {
        musicFader?.SetVoiceActive(active);
    }

    ChapterSequenceView sequence;
    CampaignCinematicView cinematicView;
    PlayableDirector cutscene;
    CampaignWaves waves;
    CampaignBeatClock savedClock;
    float titleSince, restRemaining, captionUntil;
    int phaseOffset, lastChancesUsed, constructionCycle = -1, judgementOffset;
    float builtHealth;
    readonly ConstructionJudgement construction = new ConstructionJudgement();
    int originalPromptSize;
    static KeyCode[] ConstructionKeys => new[]
    {
        GameBindings.Key(GameAction.BuildA),
        GameBindings.Key(GameAction.BuildD),
        GameBindings.Key(GameAction.BuildW),
        GameBindings.Key(GameAction.BuildS)
    };

    bool flowBusy, signalComplete, failed, windowJudged;
    public bool IsConstructionResting => Phase == CampaignPhase.Preparation && restRemaining > 0f;

    int inputBlockedThroughFrame;
    string failureReason;
    public void FailWaveRequirement(string reason)
    {
        failureReason = reason;
        FailChapter();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = null;
        TransitionRequested = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != CampaignSceneNames.Title || Current != null)
            return;
        var prefab = Resources.Load<GameObject>("Campaign/CampaignRoot");
        if (prefab != null)
            Instantiate(prefab);
        else
            Debug.LogError("[StoryDirector] 缺少Campaign/CampaignRoot。");
    }

    void Awake()
    {
        if (Current != null && Current != this)
        {
            Destroy(gameObject);
            return;
        }

        Current = this;
        DontDestroyOnLoad(gameObject);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<ChapterTable>();
            table.chapters = ChapterTable.CreateDefaults();
            table.firstChapterFrames = new ChapterFrame[4];
            int[] durations =
            {
                8,
                6,
                10,
                8
            };
            for (int i = 0; i < 4; i++)
                table.firstChapterFrames[i] = new ChapterFrame
                {
                    image = Resources.Load<Sprite>("Campaign/ChapterOne/Shot0" + (i + 1)),
                    beats = durations[i]
                };
        }

        table.Validate();
        if (cinematics == null)
            cinematics = Resources.Load<CampaignCinematics>("Campaign/Cinematics");
        if (cinematics != null)
            cinematics.Validate();
        font = GameFont.Silver != null ? GameFont.Silver : font;
        if (font == null)
            Debug.LogError("[StoryDirector] 缺少 Silver 字体配置 Campaign/GameFont。");
        ui = new CampaignUI(transform, font, screenPrefab);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = cinematicMusicVolume;
        audioSource.spatialBlend = 0;
        musicFader = gameObject.AddComponent<CampaignMusicFader>();
        musicFader.Initialize(this, audioSource);
        transitionAudio = gameObject.AddComponent<AudioSource>();
        EffectsVolumeGain.Set(transitionAudio,1);
        transitionAudio.playOnAwake = false;
        transitionAudio.spatialBlend = 0;
        sequence = gameObject.AddComponent<ChapterSequenceView>();
        cinematicView = gameObject.AddComponent<CampaignCinematicView>();
        musicLyrics = gameObject.AddComponent<CampaignMusicLyricsView>();
        cutscene = gameObject.AddComponent<PlayableDirector>();
        cutscene.playOnAwake = false;
        telemetry = gameObject.AddComponent<CampaignTelemetry>();
        telemetry.Initialize(this);
        session = gameObject.AddComponent<CampaignSession>();
        session.Initialize(this, ui.View);
        gameObject.AddComponent<CampaignSoundscape>();
        DebugConsole.Ensure(this);
    }

    void Start()
    {
        ShowTitle();
    }

    void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    void ShowTitle()
    {
        mechanicIntroduction?.Hide();
        telemetry?.FinishRun("returned_to_title");
        if (CampaignDebug.Value.nonDesign)
            Time.timeScale = 1;
        CampaignDebug.Reset();
        SfxPlayer.StopAll();
        Phase = CampaignPhase.Title;
        flowBusy = false;
        titleSince = Time.unscaledTime;
        RunState.NewCampaign();
        WaveDirector.NewRun();
        RhythmManager.JudgementFromMusic = false;
        RhythmManager.StopMusic();
        ui.AdoptTitleScene(transform);
        ui.ShowTitle();
        session.Bind(ui.View);
        session.ShowTitle();
        sequence.Initialize(ui.View);
        cinematicView.Initialize(ui.View, this);
        musicLyrics.Initialize(this, audioSource, ui.View);
        musicFader.SetVoiceActive(false);
        musicFader.SetAnimationActive(false);
        musicFader.SnapToPhase();
        StopCampaignMusic();
        audioSource.clip = null;
        transitionAudio.Stop();
        GameManager.WallMaxHP = table.constructionLimit;
        GameManager.WallHP = 0;
    }

    void Update()
    {
        if (Current == this && musicPlaybackActive && !MusicSuspended && !audioSource.isPlaying)
            musicPlaybackActive = false;
        if (GameSettingsView.IsOpen || Current != this || CampaignSession.InputBlocked || DebugConsole.SimulationPaused)
            return;
        if (Phase == CampaignPhase.Title)
        {
            ui.AnimateTitle(Time.unscaledTime - titleSince);
            if (!DebugConsole.CapturingInput && !flowBusy && Input.GetKeyDown(KeyCode.F2))
                StartCoroutine(Calibrate(false));
            else if (!DebugConsole.CapturingInput && session.FirstPrompt && !GameSettingsView.TitleSettingsClick && Input.anyKeyDown && Time.unscaledTime > titleSince + .2f && !flowBusy)
                session.NewGame();
            return;
        }

        if (Phase == CampaignPhase.Results)
        {
            if (Time.unscaledTime < titleSince + .25f)
                return;
            if (!DebugConsole.CapturingInput && failed && Input.GetKeyDown(KeyCode.R))
                StartCoroutine(BeginChapter(RunState.Chapter, true));
            else if (!DebugConsole.CapturingInput && !failed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
                StartCoroutine(BeginChapter(RunState.Chapter + 1, false));
            return;
        }

        if (Phase == CampaignPhase.Finished)
        {
            return;
        }

        // 零血结算先于同帧齐射、波次和窗口处理。
        if (CombatRunning && GameManager.WallHP <= 0 && (Chapter.number == 9 || Clock.Opened >= Chapter.defenseBeats - 4))
        {
            FailChapter();
            return;
        }

        if (!HeartbeatRunning)
            return;
        if (restRemaining > 0)
            restRemaining = Mathf.Max(0, restRemaining - Time.deltaTime);
        Clock.Advance(DebugConsole.SimulationPaused ? 0 : Time.deltaTime);
        SyncTimers();
        if (Phase == CampaignPhase.LastChance)
        {
            ui.Main.text = "听心跳，抓住静止的瞬间";
            if (!DebugConsole.CapturingInput && Input.GetKeyDown(KeyCode.Space) && Clock.ClaimPerfect())
            {
                int beforeRescue = GameManager.WallHP;
                GameManager.WallHP = Mathf.Max(1, Mathf.CeilToInt(GameManager.WallMaxHP * .1f));
                telemetry.HealthChanged(beforeRescue, GameManager.WallHP, GameManager.WallHP - beforeRescue);
                Clock = savedClock;
                Phase = CampaignPhase.Defense;
                inputBlockedThroughFrame = Time.frameCount;
                ui.Gameplay(Chapter.number == 6 ? .62f : 0);
                return;
            }

            if (Clock.Completed >= 1)
                FailChapter();
            return;
        }

        if (Phase == CampaignPhase.Choice)
        {
            UpdateChoice();
            return;
        }

        if (CombatRunning && GameManager.WallHP <= 0)
        {
            if (Chapter.number == 9 || Clock.Opened >= Chapter.defenseBeats - 4)
                FailChapter();
            else if (lastChancesUsed < table.lastChancesPerChapter)
                BeginLastChance();
            else
                FailChapter();
            return;
        }

        UpdateHUD();
        if (captionUntil > 0 && Time.time > captionUntil)
        {
            ui.Caption.text = "";
            captionUntil = 0;
        }

        if (flowBusy)
            return;
        if (Phase == CampaignPhase.Preparation && (GameManager.WallHP >= GameManager.WallMaxHP || Clock.Completed >= Chapter.preparationBeats))
            StartCoroutine(AfterPreparation());
        else if (Phase == CampaignPhase.Defense && Clock.Opened >= Chapter.defenseBeats && (waves == null || waves.Finished))
        {
            StartCoroutine(AfterDefense());
        }
        else if (Phase == CampaignPhase.Epilogue && Clock.Completed >= Chapter.epilogueBeats)
            StartCoroutine(FinishCampaign());
    }

    IEnumerator BeginCampaign()
    {
        flowBusy = true;
        table.Validate();
        telemetry.StartRun("new_game");
        if (table.calibrateEveryNewGame || !PlayerPrefs.HasKey("Barricade.CalibrationReady"))
            yield return Calibrate(true);
        yield return BeginChapter(1, false);
    }

    IEnumerator Calibrate(bool starting)
    {
        flowBusy = true;
        Phase = CampaignPhase.Calibration;
        StopCampaignMusic();
        yield return OpeningCalibration.Run(ui, transform);
        if (!starting)
            ShowTitle();
    }

    IEnumerator BeginChapter(int number, bool retry)
    {
        if (number > 10)
        {
            yield return FinishCampaign();
            yield break;
        }

        mechanicIntroduction?.Hide();
        flowBusy = true;
        ClearWorld();
        RunState.Chapter = number;
        if (number == 10 && RunState.Ending == CampaignEnding.None)
            SelectEnding(CampaignEnding.Hope, 9);
        if (number == 5)
            RunState.SpySpared = true;
        if (number == 7)
            RunState.Decision = JavertDecision.Unchosen;
        if (number != 10)
            RunState.EnterChapter(retry);
        RunState.ResetChapter();
        failureReason = null;
        lastChancesUsed = 0;
        restRemaining = 0;
        FrozenUntilBeat = 0;
        phaseOffset = 0;
        constructionCycle = -1;
        builtHealth = 0;
        GameManager.WallMaxHP = table.constructionLimit;
        if (!retry && number != 10)
            RunState.ChapterStartHP = CampaignRevisionRules.ClampHealth(GameManager.WallHP, GameManager.WallMaxHP);
        GameManager.WallHP = CampaignRevisionRules.EnterChapterHealth(GameManager.WallHP, GameManager.WallMaxHP, retry, RunState.ChapterStartHP);
        GameManager.ResetBulletDamage();
        telemetry.BeginChapter(retry ? "retry" : "chapter_entry");
        if (number == 10)
        {
            BeginEndingMusic();
            yield return OpeningCard("chapter-announcement", Chapter.title, cinematics != null ? cinematics.chapterAnnouncementSeconds : 3, 64, false);
            yield return RunEnding();
            yield break;
        }

        // Later opening cues begin with the images, after the announcement and epigraph.
        bool deferOpeningMusic = !retry && number >= 3 && number <= 8;
        if (deferOpeningMusic)
            StopCampaignMusic();
        else
            PlayChapterMusic(number);
        if (number == 1 && Chapter.cinematicBeats > 0)
        {
            yield return OpeningCard("opening-subtitle", cinematics != null ? cinematics.openingSubtitle : "", cinematics != null ? cinematics.openingSubtitleSeconds : 5, 44, cinematics == null || cinematics.openingAllowSkip);
            yield return OpeningCard("chapter-announcement", CampaignHudPresentation.Announcement(number, Chapter.title), cinematics != null ? cinematics.firstChapterAnnouncementSeconds : 3, 64, cinematics == null || cinematics.openingAllowSkip);
            if(!retry)yield return PlayChapterOpeningWords(number);
            BoundaryBeat();
            yield return PlayFirstChapter();
            yield break;
        }

        if (!retry)
        {
            yield return OpeningCard("chapter-announcement", CampaignHudPresentation.Announcement(number, Chapter.title), cinematics != null ? cinematics.chapterAnnouncementSeconds : 3, 64, cinematics == null || cinematics.chapterAnnouncementAllowSkip);
            yield return PlayChapterOpeningWords(number);
        }
        if (deferOpeningMusic)
            PlayChapterMusic(number);
        if (Chapter.cinematicBeats > 0)
        {
            yield return PlayFirstChapter();
            yield break;
        }

        if (!retry && Chapter.openingSequence != null && (Chapter.openingSequence.cinematicPrefab != null || Chapter.openingSequence.timeline != null))
            yield return Transition(Chapter.openingSequence, "chapter-opening", Chapter.openingSequence.caption, number != 9);
        if (!(number == 2 && retry && RunState.ProtagonistIntroduced))
            yield return Transition(Chapter.entry, "chapter-entry", Chapter.introduction, number != 9);
        if (!retry)
            yield return ShowMechanicIntroduction(number);
        if (Chapter.epilogueBeats > 0)
        {
            yield return LoadScene(CampaignSceneNames.Construction);
            Phase = CampaignPhase.Epilogue;
            ResetClock();
            ui.Gameplay(0);
        }
        else if (Chapter.preparationBeats > 0)
        {
            yield return LoadScene(CampaignSceneNames.Construction);
            Phase = CampaignPhase.Preparation;
            ResetClock();
            ui.Gameplay(0);
        }
        else
        {
            yield return LoadScene(CampaignSceneNames.Defense);
            Phase = CampaignPhase.Defense;
            ResetClock();
            waves = new CampaignWaves(this);
            ui.Gameplay(0);
        }

        RhythmManager.StopMusic();
        flowBusy = false;
    }

    IEnumerator FinishCampaign()
    {
        yield return RunEnding();
    }

    IEnumerator ReturnToTitle()
    {
        flowBusy = true;
        yield return SceneManager.LoadSceneAsync(CampaignSceneNames.Title);
        ShowTitle();
    }

    void ClearWorld()
    {
        FinalBarrageActive=false;ChapterOpeningWordsActive=false;
        CancelPreparationHandoff();
        endingView?.Hide();
        sequence?.Hide();
        cinematicView?.Hide();
        waves?.Dispose();
        waves = null;
        foreach (var squad in FindObjectsByType<WaveSquad>(FindObjectsSortMode.None))
            Destroy(squad.gameObject);
        foreach (var c in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))
            Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<ArrowDefinition>(FindObjectsSortMode.None))
            Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<BombHP>(FindObjectsSortMode.None))
            Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<CannonShell>(FindObjectsSortMode.None))
            Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<Cannon>(FindObjectsSortMode.None))
            Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<CampaignActor>(FindObjectsSortMode.None))
            Destroy(c.gameObject);
    }

    public void StartNewCampaign()
    {
        if (Phase == CampaignPhase.Title && !flowBusy)
            StartCoroutine(BeginCampaign());
    }
}
