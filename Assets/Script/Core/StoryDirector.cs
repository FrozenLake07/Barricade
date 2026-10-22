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
    [Header("音乐与心跳的平衡")]
    [Range(0,1),Tooltip("过场背景音乐音量；不改变总音量或心跳音量。")] public float cinematicMusicVolume=.65f;
    [Range(0,1),Tooltip("建造、防守、选择及尾声中降低音乐，突出心跳。")] public float gameplayMusicVolume=.16f;
    [Min(0),Tooltip("音乐变小时的渐变秒数。")] public float gameplayMusicFadeSeconds=.45f;
    [Min(0),Tooltip("回到过场或配音结束时的恢复秒数。")] public float musicRecoveryFadeSeconds=.6f;
    public GameObject endingPrefab;
    public GameObject screenPrefab;
    public UnityEvent transitionStarted = new UnityEvent(), transitionFinished = new UnityEvent();
    public static event Action<int,string> TransitionRequested;
    public static StoryDirector Current {get;private set;}
    public static bool Active => Current!=null;
    public static bool CombatRunning => Active&&!CampaignSession.InputBlocked&&Current.Phase==CampaignPhase.Defense;
    public static bool GameplayRunning => Active&&!CampaignSession.InputBlocked&&(Current.Phase==CampaignPhase.Preparation||Current.Phase==CampaignPhase.Defense);
    public static bool HeartbeatRunning => !CampaignSession.InputBlocked&&(GameplayRunning||(Active&&(Current.Phase==CampaignPhase.Choice||Current.Phase==CampaignPhase.Epilogue||Current.Phase==CampaignPhase.LastChance||Current.Phase==CampaignPhase.EndCard||Current.Phase==CampaignPhase.Cinematic)));
    CampaignPhase phase;
    public CampaignPhase Phase {get=>phase;private set{if(phase==value)return;phase=value;telemetry?.PhaseChanged(value);}}
    public ChapterDefinition Chapter => table.chapters[RunState.Chapter-1];
    public CampaignBeatClock Clock {get;private set;} = new CampaignBeatClock();
    public int ChapterBeat => phaseOffset+Clock.Opened;
    public int FrozenUntilBeat {get;private set;}
    public bool EnemiesFrozen => !CombatRunning||Clock.Opened<FrozenUntilBeat||CampaignDebug.Value.freeze;
    public float BaseWait => Chapter.number==8&&RunState.Decision==JavertDecision.Executed?Chapter.startingWait/1.1f:Chapter.startingWait;
    CampaignTelemetry telemetry;
    CampaignUI ui;
    CampaignSession session;
    AudioSource audioSource, transitionAudio;
    bool musicPlaybackActive;
    CampaignMusicFader musicFader;
    public void SetCinematicVoiceActive(bool active){musicFader?.SetVoiceActive(active);}
    ChapterSequenceView sequence;
    CampaignCinematicView cinematicView;
    PlayableDirector cutscene;
    CampaignWaves waves;
    CampaignBeatClock savedClock;
    float titleSince, restRemaining, captionUntil;
    int phaseOffset, lastChancesUsed, constructionCycle=-1,judgementOffset;
    float builtHealth;
    readonly ConstructionJudgement construction=new ConstructionJudgement();
    int originalPromptSize;
    static readonly KeyCode[] ConstructionKeys={KeyCode.A,KeyCode.D,KeyCode.W,KeyCode.S};
    bool flowBusy, signalComplete, failed, windowJudged;
    public bool IsConstructionResting => Phase==CampaignPhase.Preparation&&restRemaining>0f;

    int inputBlockedThroughFrame;
    string failureReason;
    public void FailWaveRequirement(string reason){failureReason=reason;FailChapter();}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Current=null;TransitionRequested=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if(SceneManager.GetActiveScene().name!="Title"||Current!=null)return;
        var prefab=Resources.Load<GameObject>("Campaign/CampaignRoot");
        if(prefab!=null)Instantiate(prefab);
        else Debug.LogError("[StoryDirector] 缺少Campaign/CampaignRoot。");
    }
    void Awake()
    {
        if(Current!=null&&Current!=this){Destroy(gameObject);return;}Current=this;DontDestroyOnLoad(gameObject);
        if(table==null){table=ScriptableObject.CreateInstance<ChapterTable>();table.chapters=ChapterTable.CreateDefaults();
            table.firstChapterFrames=new ChapterFrame[4];int[] durations={8,6,10,8};
            for(int i=0;i<4;i++)table.firstChapterFrames[i]=new ChapterFrame{image=Resources.Load<Sprite>("Campaign/ChapterOne/Shot0"+(i+1)),beats=durations[i]};}
        table.Validate();
        if(cinematics==null)cinematics=Resources.Load<CampaignCinematics>("Campaign/Cinematics");
        if(cinematics!=null)cinematics.Validate();
        font=GameFont.Silver!=null?GameFont.Silver:font;
        if(font==null)Debug.LogError("[StoryDirector] 缺少 Silver 字体配置 Campaign/GameFont。");
        ui=new CampaignUI(transform,font,screenPrefab);audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.volume=cinematicMusicVolume;audioSource.spatialBlend=0;
        musicFader=gameObject.AddComponent<CampaignMusicFader>();musicFader.Initialize(this,audioSource);
        transitionAudio=gameObject.AddComponent<AudioSource>();transitionAudio.playOnAwake=false;transitionAudio.spatialBlend=0;
        sequence=gameObject.AddComponent<ChapterSequenceView>();
        cinematicView=gameObject.AddComponent<CampaignCinematicView>();
        cutscene=gameObject.AddComponent<PlayableDirector>();cutscene.playOnAwake=false;
        telemetry=gameObject.AddComponent<CampaignTelemetry>();telemetry.Initialize(this);
        session=gameObject.AddComponent<CampaignSession>();session.Initialize(this,ui.View);
        DebugConsole.Ensure(this);
    }
    void Start(){ShowTitle();}
    void OnDestroy(){if(Current==this)Current=null;}
    void ShowTitle()
    {
        telemetry?.FinishRun("returned_to_title");
        if(CampaignDebug.Value.nonDesign)Time.timeScale=1;
        CampaignDebug.Reset();
        Phase=CampaignPhase.Title;flowBusy=false;titleSince=Time.unscaledTime;RunState.NewCampaign();WaveDirector.NewRun();
        RhythmManager.JudgementFromMusic=false;RhythmManager.StopMusic();
        ui.AdoptTitleScene(transform);ui.ShowTitle();session.Bind(ui.View);session.ShowTitle();sequence.Initialize(ui.View);
        cinematicView.Initialize(ui.View,this);musicFader.SetVoiceActive(false);musicFader.SnapToPhase();
        StopCampaignMusic();audioSource.clip=null;transitionAudio.Stop();GameManager.WallMaxHP=table.constructionLimit;GameManager.WallHP=0;
    }
    void PlayMusic(AudioClip clip)
    {
        audioSource.loop=false;
        // Retain the clip after completion or chapter-nine silence, so chapter changes cannot restart it.
        if(audioSource.clip==clip)return;
        StopCampaignMusic();audioSource.clip=clip;
        if(clip!=null){audioSource.Play();musicPlaybackActive=true;}
    }
    void StopCampaignMusic(){audioSource.Stop();musicPlaybackActive=false;}
    void Update()
    {
        if(Current==this&&musicPlaybackActive&&!MusicSuspended&&!audioSource.isPlaying)musicPlaybackActive=false;
        if(Current!=this||CampaignSession.InputBlocked||DebugConsole.SimulationPaused)return;
        if(Phase==CampaignPhase.Title)
        {
            ui.AnimateTitle(Time.unscaledTime-titleSince);
            if(!DebugConsole.CapturingInput&&!flowBusy&&Input.GetKeyDown(KeyCode.F2))StartCoroutine(Calibrate(false));
            else if(!DebugConsole.CapturingInput&&session.FirstPrompt&&Input.anyKeyDown&&Time.unscaledTime>titleSince+.2f&&!flowBusy)session.NewGame();return;
        }
        if(Phase==CampaignPhase.Results)
        {
            if(Time.unscaledTime<titleSince+.25f)return;
            if(!DebugConsole.CapturingInput&&failed&&Input.GetKeyDown(KeyCode.R))StartCoroutine(BeginChapter(RunState.Chapter,true));
            else if(!DebugConsole.CapturingInput&&!failed&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)))StartCoroutine(BeginChapter(RunState.Chapter+1,false));
            return;
        }
        if(Phase==CampaignPhase.Finished)
        {if(!DebugConsole.CapturingInput&&Input.anyKeyDown&&Time.unscaledTime>titleSince+.5f)StartCoroutine(ReturnToTitle());return;}
        // 零血结算先于同帧齐射、波次和窗口处理。
        if(CombatRunning&&GameManager.WallHP<=0&&(Chapter.number==9||Clock.Opened>=Chapter.defenseBeats-4)){FailChapter();return;}
        if(!HeartbeatRunning)return;
        if(restRemaining>0)restRemaining=Mathf.Max(0,restRemaining-Time.deltaTime);
        Clock.Advance(DebugConsole.SimulationPaused?0:Time.deltaTime);
        SyncTimers();
        if(Phase==CampaignPhase.LastChance)
        {
            ui.Main.text="听心跳，抓住静止的瞬间";
            if(!DebugConsole.CapturingInput&&Input.GetKeyDown(KeyCode.Space)&&Clock.ClaimPerfect())
            {int beforeRescue=GameManager.WallHP;GameManager.WallHP=Mathf.Max(1,Mathf.CeilToInt(GameManager.WallMaxHP*.1f));telemetry.HealthChanged(beforeRescue,GameManager.WallHP,GameManager.WallHP-beforeRescue);Clock=savedClock;Phase=CampaignPhase.Defense;inputBlockedThroughFrame=Time.frameCount;ui.Gameplay(Chapter.number==6?.62f:0);return;}
            if(Clock.Completed>=1)FailChapter();return;
        }
        if(Phase==CampaignPhase.Choice){UpdateChoice();return;}

        if(CombatRunning&&GameManager.WallHP<=0)
        {
            if(Chapter.number==9||Clock.Opened>=Chapter.defenseBeats-4)FailChapter();
            else if(lastChancesUsed<table.lastChancesPerChapter)BeginLastChance();else FailChapter();return;
        }
        UpdateHUD();
        if(captionUntil>0&&Time.time>captionUntil){ui.Caption.text="";captionUntil=0;}
        if(flowBusy)return;
        if(Phase==CampaignPhase.Preparation&&Clock.Completed>=Chapter.preparationBeats)StartCoroutine(AfterPreparation());
        else if(Phase==CampaignPhase.Defense&&Clock.Opened>=Chapter.defenseBeats&&(waves==null||waves.Finished))
        {StartCoroutine(AfterDefense());}
        else if(Phase==CampaignPhase.Epilogue&&Clock.Completed>=Chapter.epilogueBeats)StartCoroutine(FinishCampaign());
    }
    IEnumerator BeginCampaign()
    {flowBusy=true;table.Validate();telemetry.StartRun("new_game");if(table.calibrateEveryNewGame||!PlayerPrefs.HasKey("Barricade.CalibrationReady"))yield return Calibrate(true);PlayMusic(chapterMusic);yield return BeginChapter(1,false);}
    IEnumerator Calibrate(bool starting)
    {
        flowBusy=true;Phase=CampaignPhase.Calibration;StopCampaignMusic();
        yield return OpeningCalibration.Run(ui,transform);
        if(!starting)ShowTitle();
    }
    void BoundaryBeat(){RunState.TotalHeartbeats++;if(!CampaignDebug.Value.muteHeartbeat)SfxPlayer.Play("HeartbeatPulse/Pulse-V1",.55f);}
    IEnumerator BeginChapter(int number,bool retry)
    {
        if(number>10){yield return FinishCampaign();yield break;}
        flowBusy=true;ClearWorld();RunState.Chapter=number;
        if(number==10&&RunState.Ending==CampaignEnding.None)SelectEnding(CampaignEnding.Hope,9);
        if(number==5)RunState.SpySpared=true;
        if(number==7)RunState.Decision=JavertDecision.Unchosen;
        RunState.EnterChapter(retry);RunState.ResetChapter();failureReason=null;lastChancesUsed=0;restRemaining=0;FrozenUntilBeat=0;phaseOffset=0;constructionCycle=-1;builtHealth=0;
        GameManager.WallMaxHP=table.constructionLimit;
        if(!retry)RunState.ChapterStartHP=CampaignRevisionRules.ClampHealth(GameManager.WallHP,GameManager.WallMaxHP);
        GameManager.WallHP=CampaignRevisionRules.EnterChapterHealth(GameManager.WallHP,GameManager.WallMaxHP,retry,RunState.ChapterStartHP);
        telemetry.BeginChapter(retry?"retry":"chapter_entry");
        if(number==1&&Chapter.cinematicBeats>0)
        {
            yield return OpeningCard("opening-subtitle",cinematics!=null?cinematics.openingSubtitle:"",cinematics!=null?cinematics.openingSubtitleSeconds:5,44,cinematics==null||cinematics.openingAllowSkip);
            yield return OpeningCard("chapter-announcement",CampaignHudPresentation.Announcement(number,Chapter.title),cinematics!=null?cinematics.firstChapterAnnouncementSeconds:3,64,cinematics==null||cinematics.openingAllowSkip);
            BoundaryBeat();yield return PlayFirstChapter();yield break;
        }
        if(number==10)BeginEndingMusic();else if(number==9)StopCampaignMusic();else PlayMusic(chapterMusic);
        if(!retry)
            yield return OpeningCard("chapter-announcement",CampaignHudPresentation.Announcement(number,Chapter.title),cinematics!=null?cinematics.chapterAnnouncementSeconds:3,64,cinematics==null||cinematics.chapterAnnouncementAllowSkip);
        if(Chapter.cinematicBeats>0){yield return PlayFirstChapter();yield break;}
        if(!(number==2&&retry&&RunState.ProtagonistIntroduced))
            yield return Transition(Chapter.entry,"chapter-entry",Chapter.introduction,number!=9);
        if(Chapter.epilogueBeats>0)
        {yield return LoadScene("1");Phase=CampaignPhase.Epilogue;ResetClock();ui.Gameplay(0);}
        else if(Chapter.preparationBeats>0)
        {yield return LoadScene("1");Phase=CampaignPhase.Preparation;ResetClock();ui.Gameplay(0);}
        else
        {yield return LoadScene("2");Phase=CampaignPhase.Defense;ResetClock();waves=new CampaignWaves(this);ui.Gameplay(0);}
        RhythmManager.StopMusic();flowBusy=false;
    }
    IEnumerator OpeningCard(string key,string text,float seconds,int fontSize,bool allowSkip)
    {
        if(seconds<=0)yield break;
        Phase=CampaignPhase.Transition;ui.Card("","");ui.Night.color=Color.clear;
        TransitionRequested?.Invoke(RunState.Chapter,key);transitionStarted.Invoke();telemetry.Record(key=="chapter-announcement"?"chapter_announcement":"opening_card",key);
        yield return cinematicView.PlayBlackCard(text,seconds,allowSkip,fontSize);
        CampaignSession.BlockCurrentInput();transitionFinished.Invoke();
    }
    IEnumerator PlayFirstChapter()
    {
        Phase=CampaignPhase.Cinematic;ResetClock();ui.Card("","");
        int[] durations=new int[table.firstChapterFrames.Length];
        for(int i=0;i<durations.Length;i++)durations[i]=table.firstChapterFrames[i].beats;
        sequence.ShowFrame(0,table.firstChapterFrames[0],table.firstChapterCrossfadeSeconds);
        while(Clock.Completed<Chapter.cinematicBeats)
        {
            if(!CampaignSession.InputBlocked)
            {
                int index=CampaignRevisionRules.FrameAt(Clock.Completed,durations);
                if(index!=sequence.FrameIndex)sequence.ShowFrame(index,table.firstChapterFrames[index],table.firstChapterCrossfadeSeconds);
                sequence.Advance(Time.deltaTime);
            }
            yield return null;
        }
        sequence.Hide();telemetry.EndChapter("completed");yield return BeginChapter(2,false);
    }
    IEnumerator LoadScene(string name)
    {
        Phase=CampaignPhase.Transition;
        if(!Application.CanStreamedLevelBeLoaded(name))throw new InvalidOperationException("场景未加入Build Settings："+name);
        yield return SceneManager.LoadSceneAsync(name);yield return null;
        // 场景Start完成后再开放输入，避免旧计时/HP初始化覆盖章节状态。
    }
    void ResetClock()
    {
        Clock=new CampaignBeatClock();Clock.Reset(CampaignDebug.Value.tempo>0?CampaignDebug.Value.tempo:BaseWait,CampaignDebug.Value.window>0?CampaignDebug.Value.window:table.perfectWindow);Clock.WindowOpened+=OnWindow;Clock.CycleCompleted+=OnCycleCompleted;
        windowJudged=false;constructionCycle=-1;judgementOffset=RunState.Judgements.Count;SyncTimers();
    }
    void OnWindow(int beat)
    {
        RunState.TotalHeartbeats++;
        if(GameplayRunning&&(Phase!=CampaignPhase.Defense||beat<Chapter.defenseBeats-4))
            EnsureJudgementSlot();

        if(CombatRunning)waves?.Beat(beat);
        if(Phase==CampaignPhase.Epilogue)
        {
            Clock.NextWait=CampaignDebug.Value.tempo>0?CampaignDebug.Value.tempo:Mathf.Lerp(.5f,1.2f,beat/(float)Mathf.Max(1,Chapter.epilogueBeats));
        }
    }
    void SyncTimers()
    {
        Tap.Setbeattime=TapSec.Setbeattime=Clock.Wait;Tap.oribeattime=TapSec.oribeattime=BaseWait;
        Tap.beattime=TapSec.beattime=Clock.BeatRemaining;Tap.perfecttime=TapSec.perfecttime=Clock.PerfectRemaining;
    }
    IEnumerator AfterPreparation()
    {
        flowBusy=true;
        if(Chapter.defenseBeats==0){flowBusy=false;CompleteChapter();yield break;}
        phaseOffset=Chapter.preparationBeats;
        GameManager.WallMaxHP=table.constructionLimit;
        string caption=Chapter.introduction;
        yield return Transition(Chapter.prepareToDefense,"prepare-to-defense",caption,Chapter.number!=9);
        yield return LoadScene("2");Phase=CampaignPhase.Defense;ResetClock();waves=new CampaignWaves(this);
        ui.Gameplay(Chapter.number==6?.62f:0);RhythmManager.StopMusic();flowBusy=false;
    }
    void StartChoice()
    {
        waves?.Complete();ClearWorld();Phase=CampaignPhase.Choice;phaseOffset=Chapter.preparationBeats+Chapter.defenseBeats;ResetClock();
        ui.Card("选择：放走或处决","Q：放走 —— 下一章追兵减少\nE：处决 —— 重振防线，下一章心跳加快");
        UpdateChoiceDisplay();
    }
    void UpdateChoice()
    {
        UpdateChoiceDisplay();
        if(Clock.Completed>=Chapter.choiceBeats){ResolveChoice(JavertDecision.Released,true);return;}
        HandleChoice();
    }
    void UpdateChoiceDisplay()
    {
        float remaining=Mathf.Max(0,(Chapter.choiceBeats-Clock.Completed)*Clock.Period-Clock.Elapsed);
        ui.Main.text=$"选择：放走或处决\n剩余 {remaining:F1} 秒（超时放走）";
    }
    void HandleChoice()
    {
        if(DebugConsole.CapturingInput)return;
        if(Input.GetKeyDown(KeyCode.Q))ResolveChoice(JavertDecision.Released,false);
        else if(Input.GetKeyDown(KeyCode.E))ResolveChoice(JavertDecision.Executed,false);
    }
    void ResolveChoice(JavertDecision decision,bool timedOut)
    {
        if(Phase!=CampaignPhase.Choice||flowBusy||RunState.Decision!=JavertDecision.Unchosen)return;
        RunState.Decision=decision;
        if(decision==JavertDecision.Executed)GameManager.AddWallHP(Mathf.CeilToInt(GameManager.WallMaxHP*.3f));
        telemetry.Record("choice",timedOut?"timeout_released":decision.ToString());
        CompleteChapter();
    }
    void BeginLastChance()
    {
        telemetry.Record("rescue");
        lastChancesUsed++;savedClock=Clock;Clock=new CampaignBeatClock();Clock.Reset(CampaignDebug.Value.tempo>0?CampaignDebug.Value.tempo:BaseWait,CampaignDebug.Value.window>0?CampaignDebug.Value.window:table.perfectWindow);
        Clock.WindowOpened+=beat=>RunState.TotalHeartbeats++;
        Phase=CampaignPhase.LastChance;ui.Card("最后一拍：听心跳","在下一次静止间歇按空格，尝试再次站稳。剩余机会："+(table.lastChancesPerChapter-lastChancesUsed)+"。");
    }
    void FailChapter()
    {
        if(flowBusy||Chapter.number>=10)return;
        flowBusy=true;
        SelectEnding(Chapter.number==9?CampaignEnding.Hope:CampaignEnding.Mourning,Chapter.number);
        telemetry.EndChapter("defeated");StartCoroutine(BeginChapter(10,false));
    }
    IEnumerator FailureTransition()
    {flowBusy=true;ClearWorld();yield return Transition(Chapter.exit,"chapter-failure",failureReason??"街垒没有守住。",false);failed=true;ShowResults();}
    void CompleteChapter()
    {
        if(flowBusy)return;
        waves?.Complete();
        if(Chapter.number==9){SelectEnding(CampaignEnding.Hope,9);telemetry.EndChapter("completed");StartCoroutine(BeginChapter(10,false));return;}
        StartCoroutine(CompleteWithExit());
    }
    IEnumerator CompleteWithExit()
    {
        flowBusy=true;ClearWorld();
        string text=Chapter.number==7&&RunState.Decision!=JavertDecision.Unchosen?(RunState.Decision==JavertDecision.Released?"已放走沙威":"已处决沙威"):Chapter.number==5?(RunState.SpySpared?"他没有开枪。这让一些人很不高兴。":"酒馆里的人，没有再出来。"):$"第{Chapter.number}章结束";
        yield return Transition(Chapter.exit,"chapter-exit",text,false);
        failed=false;ShowResults();
    }
    void ShowResults()
    {
        telemetry.EndChapter(failed?"failed":"completed");
        flowBusy=false;ClearWorld();Phase=CampaignPhase.Results;titleSince=Time.unscaledTime;
        ui.Card(failed?"本章未守住":"本章结束",CampaignHudPresentation.Results(RunState.Perfects,RunState.Judgements.Count,RunState.Mistakes,RunState.Score,failed));
    }
    IEnumerator FinishCampaign()
    {
        flowBusy=true;ClearWorld();
        ui.Gameplay(0);ui.Hud.text="";ui.Caption.text="";
        GameObject ending=null;
        if(RunState.Ending==CampaignEnding.Hope&&endingPrefab!=null){ending=Instantiate(endingPrefab,new Vector3(0,-2,0),Quaternion.identity);ending.GetComponent<HeroSequenceController>()?.Play();}
        yield return Transition(table.ending,"ending","",true,ending!=null);
        if(ending!=null)Destroy(ending);
        telemetry.EndChapter("completed");telemetry.FinishRun("completed");
        BoundaryBeat();Phase=CampaignPhase.Finished;flowBusy=false;titleSince=Time.unscaledTime;
        ui.Card(table.chapters[9].title,"按任意键回到开始界面");
    }
    IEnumerator ReturnToTitle()
    {flowBusy=true;yield return SceneManager.LoadSceneAsync("Title");ShowTitle();}

    // 动画可用Signal调用此方法完成；空接口使用定时文字卡，暂停Timeline不会误切场。
    public void CompleteTransition()=>signalComplete=true;
    public void PlayTransitionCue(AudioClip clip){if(clip!=null&&RunState.Chapter!=9)transitionAudio.PlayOneShot(clip);}
    IEnumerator Transition(ChapterTransition slot,string key,string text,bool useMusic,bool transparent=false)
    {
        Phase=CampaignPhase.Transition;signalComplete=false;
        if(!string.IsNullOrEmpty(slot.caption))text=slot.caption;
        TransitionRequested?.Invoke(RunState.Chapter,key);transitionStarted.Invoke();
        // Explicit prefab/Timeline overrides remain authoritative; otherwise use the configured image route.
        var route=cinematics!=null?cinematics.Find(RunState.Chapter,key,RunState.Decision,RunState.Ending):null;
        if(route!=null&&slot.cinematicPrefab==null&&slot.timeline==null)
        {
            ui.Card("","");ui.Black.color=Color.clear;ui.Night.color=Color.clear;
            if(RunState.Chapter==9)StopCampaignMusic();
            if(slot.audio!=null&&RunState.Chapter!=9)transitionAudio.PlayOneShot(slot.audio);
            yield return cinematicView.Play(cinematics,route,slot.allowSkip);
            CampaignSession.BlockCurrentInput();transitionAudio.Stop();transitionFinished.Invoke();yield break;
        }
        if(!transparent)ui.Card(text,slot.allowSkip?"空格：跳过":"");
        if(RunState.Chapter==9)StopCampaignMusic();
        else if(slot.audio!=null)transitionAudio.PlayOneShot(slot.audio);
        GameObject visual=slot.cinematicPrefab!=null?Instantiate(slot.cinematicPrefab):null;
        if(visual!=null||slot.timeline!=null){ui.Black.color=Color.clear;ui.Main.text="";}
        PlayableDirector supplied=visual!=null?visual.GetComponentInChildren<PlayableDirector>():null;
        PlayableDirector director=supplied!=null?supplied:cutscene;
        if(slot.timeline!=null)director.playableAsset=slot.timeline;
        bool timeline=director!=null&&director.playableAsset!=null;
        Action<PlayableDirector> stopped=d=>signalComplete=true;
        if(timeline){director.stopped+=stopped;director.extrapolationMode=DirectorWrapMode.None;director.time=0;director.Play();}
        float elapsed=0;
        while(!signalComplete||CampaignSession.Suspended)
        {
            if(CampaignSession.Suspended||DebugConsole.SimulationPaused){yield return null;continue;}
            elapsed+=Time.unscaledDeltaTime;
            if(!timeline&&!slot.waitForSignal&&elapsed>=Mathf.Max(.05f,slot.placeholderSeconds))break;
            if(!DebugConsole.CapturingInput&&slot.allowSkip&&elapsed>.25f&&Input.GetKeyDown(KeyCode.Space)){telemetry.Record("transition_skip",key);break;}
            if(slot.waitForSignal&&!timeline&&elapsed>=Mathf.Max(1,slot.signalTimeoutSeconds))
            {Debug.LogWarning("[StoryDirector] 过场完成信号超时："+key);break;}
            yield return null;
        }
        if(timeline){director.stopped-=stopped;director.Stop();}
        if(visual!=null)Destroy(visual);
        cutscene.playableAsset=null;transitionAudio.Stop();
        CompleteHopeRoute(route);transitionFinished.Invoke();
    }
    // Timeline narrative captions are allowed during transitions only. Chapter prose is removed.
    public void ShowCaption(string text){if(Phase==CampaignPhase.Transition)ShowGameplayHint(text);}
    public void ShowGameplayHint(string text){ui.Caption.text=text;captionUntil=Time.time+6f;}
    void UpdateHUD()
    {
        if(!GameplayRunning)return;
        ui.Hud.enabled=ui.Caption.enabled=!CampaignDebug.Value.hideHud;
        ui.Hud.color=ParisUI.Style.cream;
        string actions=Phase==CampaignPhase.Preparation?(Chapter.number==2?"建造：按画面字母；双键间隔不超过120ms":"修补：按画面字母；双键间隔不超过120ms"):"A/D 换轨   W 空中 / S 地面   空格开火";
        ui.Hud.text=CampaignHudPresentation.Heading(RunState.Chapter,Chapter.title)+"\n"+
            actions+(Chapter.number==8&&WaveDirector.Current!=null?$"\n炮弹拦截 {WaveDirector.Current.ShellInterceptions}/{WaveDirector.Current.Script.requiredShellInterceptions}":"");
    }
    void EnsureJudgementSlot()
    {while(RunState.Judgements.Count<=judgementOffset+Clock.Completed)RunState.Judgements.Add(0);}
    public void RecordInput(bool perfect,bool missed=false)
    {
        if(windowJudged)return; // 每周期一次，后续误按不能覆盖已记录的Perfect。
        windowJudged=true;EnsureJudgementSlot();if(!RunState.TryRecordAt(judgementOffset+Clock.Completed,perfect))return;
        telemetry.Judge(perfect,missed);
        Clock.NextWait=CampaignDebug.Value.tempo>0?CampaignDebug.Value.tempo:perfect&&Chapter.accelerate?Mathf.Max(.5f,Clock.NextWait-.05f):BaseWait;
    }
    void OnCycleCompleted(int beat)
    {
        if(Phase==CampaignPhase.Preparation&&!windowJudged&&constructionCycle==Clock.Completed)
        {
            var result=construction.Close();
            if(result==ConstructionResult.Wrong){telemetry.Record("rest");restRemaining=3;SfxPlayer.Play("SFX-QTE-03");}
        }
        bool scoring=GameplayRunning&&(Phase!=CampaignPhase.Defense||beat<Chapter.defenseBeats-4);
        if(scoring&&!windowJudged)RecordInput(false,true);
        telemetry.Beat(Phase,beat);
        windowJudged=false;
    }
    public void ConstructionInput(Tap tap)
    {
        if(DebugConsole.CapturingInput||CampaignSession.InputBlocked||Time.frameCount<=inputBlockedThroughFrame||Phase!=CampaignPhase.Preparation)return;
        if(constructionCycle!=Clock.Completed)
        {
            constructionCycle=Clock.Completed;
            ConstructionJudgement.Prompt(Chapter.number,constructionCycle+1,out int first,out int second);
            construction.Reset(first,second,table.chordMaxGap);
            if(Chapter.number==2&&constructionCycle+1==49)ShowGameplayHint("这一拍，同时按下两个字母。先后相差不超过120毫秒。");
        }
        if(tap.WordUI!=null)
        {
            if(originalPromptSize==0)originalPromptSize=tap.WordUI.fontSize;
            tap.WordUI.fontSize=construction.Chord?Mathf.Max(20,originalPromptSize-6):originalPromptSize;
            tap.WordUI.text=restRemaining>0?$"REST {Mathf.CeilToInt(restRemaining)}s":ConstructionKeys[construction.First]+(construction.Chord?" + "+ConstructionKeys[construction.Second]:"");
        }
        if(restRemaining>0||windowJudged)return;
        for(int key=0;key<ConstructionKeys.Length;key++)
        {
            if(!Input.GetKeyDown(ConstructionKeys[key]))continue;
            var result=construction.Press(key,Time.time,Clock.IsPerfect);
            if(result==ConstructionResult.None)continue;
            ApplyConstructionResult(tap,result);return;
        }
        if(Input.GetKeyDown(KeyCode.Space))ApplyConstructionResult(tap,ConstructionResult.Wrong);
    }
    void ApplyConstructionResult(Tap tap,ConstructionResult result)
    {
        bool perfect=result==ConstructionResult.Perfect;RecordInput(perfect);
        if(result==ConstructionResult.Wrong){telemetry.Record("rest");restRemaining=3;SfxPlayer.Play("SFX-QTE-03");return;}
        if(result==ConstructionResult.Miss)return;
        if(perfect)Clock.ClaimPerfect();
        tap.NotifyCampaignInput(perfect,construction.Chord&&perfect?1.12246f:1f);
        int raw=CampaignRevisionRules.ConstructionReward(construction.Chord,perfect,table.chordPerfectHP,table.chordOrdinaryHP);
        builtHealth+=raw;
        int total=Mathf.FloorToInt(builtHealth+.001f),gain=total-RunState.BuiltHP;
        RunState.BuiltHP=total;GameManager.AddWallHP(gain);
    }
    public void CombatInput(TapSec input)
    {
        if(DebugConsole.CapturingInput||!CombatRunning||Time.frameCount<=inputBlockedThroughFrame||(WaveDirector.Current!=null&&WaveDirector.Current.FinaleStarted))return;
        if(Input.GetKeyDown(KeyCode.W))input.CampaignSetAim("Up");else if(Input.GetKeyDown(KeyCode.S))input.CampaignSetAim("Down");
        var gun=input.sbullet;if(gun==null)return;
        if(Input.GetKeyDown(KeyCode.R))gun.BeginCampaignReload();
        if(!Input.GetKeyDown(KeyCode.Space)||!gun.CanFire)return;
        bool forced=CampaignDebug.Value.crit&&gun.CurrentTarget()!=null;
        bool perfect=forced||Clock.IsPerfect;
        if(gun.CurrentTarget()==null)perfect=false;
        if(perfect&&!forced&&!Clock.ClaimPerfect())return;
        if(forced)CampaignDebug.Value.crit=false;
        RecordInput(perfect);gun.Fire(TapSec.ShootStatu,perfect);
    }
    IEnumerator AfterDefense()
    {
        flowBusy=true;
        if(Chapter.number==9){SelectEnding(CampaignEnding.Hope,9);telemetry.EndChapter("completed");yield return BeginChapter(10,false);yield break;}
        waves?.Complete();
        if(Chapter.choiceBeats>0)
        {
            if(cinematics!=null&&cinematics.Find(Chapter.number,"before-choice",RunState.Decision)!=null)
                yield return Transition(Chapter.exit,"before-choice","",true);
            StartChoice();flowBusy=false;yield break;
        }
        Phase=CampaignPhase.EndCard;
        ui.Card($"第{Chapter.number}章结束", "");
        Clock=new CampaignBeatClock();Clock.Reset(CampaignDebug.Value.tempo>0?CampaignDebug.Value.tempo:BaseWait,CampaignDebug.Value.window>0?CampaignDebug.Value.window:table.perfectWindow);
        Clock.WindowOpened+=beat=>RunState.TotalHeartbeats++;
        while(Clock.Completed<2||CampaignSession.Suspended||DebugConsole.SimulationPaused)yield return null;
        flowBusy=false;CompleteChapter();
    }
    void ClearWorld()
    {
        sequence?.Hide();
        cinematicView?.Hide();
        waves?.Dispose();waves=null;
        foreach(var squad in FindObjectsByType<WaveSquad>(FindObjectsSortMode.None))Destroy(squad.gameObject);
        foreach(var c in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))Destroy(c.gameObject);
        foreach(var c in FindObjectsByType<ArrowDefinition>(FindObjectsSortMode.None))Destroy(c.gameObject);
        foreach(var c in FindObjectsByType<BombHP>(FindObjectsSortMode.None))Destroy(c.gameObject);
        foreach(var c in FindObjectsByType<CannonShell>(FindObjectsSortMode.None))Destroy(c.gameObject);
        foreach(var c in FindObjectsByType<Cannon>(FindObjectsSortMode.None))Destroy(c.gameObject);
        foreach(var c in FindObjectsByType<CampaignActor>(FindObjectsSortMode.None))Destroy(c.gameObject);
    }
    public bool CanPause => Phase!=CampaignPhase.Title&&Phase!=CampaignPhase.Calibration&&Phase!=CampaignPhase.Finished;
    public bool CanSave => !flowBusy&&CampaignSaveData.Stable(Phase);
    public void StartNewCampaign(){if(Phase==CampaignPhase.Title&&!flowBusy)StartCoroutine(BeginCampaign());}
    static readonly string[] SavedDirectorFields={"phaseOffset","lastChancesUsed","constructionCycle","judgementOffset","builtHealth","restRemaining",
        "windowJudged","failed","<FrozenUntilBeat>k__BackingField","originalPromptSize","captionUntil","titleSince","failureReason","construction"};
    public CampaignSaveData CaptureSavedGame()
    {
        if(!CanSave)throw new InvalidOperationException("当前过场尚未结束。");
        var world=new CampaignSaveWorld();var state=world.Capture();
        var data=new CampaignSaveData{debugSettings=CampaignDebug.Value.nonDesign?JsonUtility.ToJson(CampaignDebug.Value):null,configurationId=telemetry.ConfigurationId,chapter=RunState.Chapter,phase=(int)Phase,utc=DateTime.UtcNow.ToString("O"),scene=SceneManager.GetActiveScene().name,
            sceneTime=Time.timeSinceLevelLoad,gameTime=Time.time,unscaledTime=Time.unscaledTime,world=state,
            director=CampaignStateCodec.Capture(this,world.Reference,SavedDirectorFields),clock=CampaignStateCodec.Capture(Clock,world.Reference),
            hasSavedClock=savedClock!=null,savedClock=savedClock!=null?CampaignStateCodec.Capture(savedClock,world.Reference):null,
            run=CampaignStateCodec.Capture(typeof(RunState),world.Reference),wall=CampaignStateCodec.Capture(typeof(GameManager),world.Reference,"WallHP","WallMaxHP"),
            tap=CampaignStateCodec.Capture(typeof(Tap),world.Reference,"beattime","oribeattime","Setbeattime","perfecttime","Setperfecttime"),
            tapSec=CampaignStateCodec.Capture(typeof(TapSec),world.Reference),track=CampaignStateCodec.Capture(typeof(CapsuleTrack),world.Reference),
            spawn=CampaignStateCodec.Capture(typeof(Spawn),world.Reference,"nextGroupId"),
            main=ui.Main.text,detail=ui.Detail.text,hud=ui.Hud.text,caption=ui.Caption.text,mainFontSize=ui.Main.fontSize,
            black=ui.Black.color,night=ui.Night.color,mainColor=ui.Main.color,detailColor=ui.Detail.color,
            musicTrack=MusicTrackId(),musicPlaying=musicPlaybackActive&&(audioSource.isPlaying||MusicSuspended),musicTime=audioSource.clip!=null?audioSource.time:0};
        if(WaveDirector.Current!=null)
        {
            data.hasWave=true;data.waveProgress=WaveDirector.Current.CaptureProgress();
            data.wave=CampaignStateCodec.Capture(WaveDirector.Current,world.Reference,"cannon","eventsBeat","eventsThisBeat","reducedIndex","lastVolleyBeat","debugDensityCarry",
                "<FinaleStarted>k__BackingField","<ShellInterceptions>k__BackingField");
        }
        data.Validate();return data;
    }
    public IEnumerator RestoreSavedGame(CampaignSaveData data)
    {
        data.Validate();data.ValidateConfiguration(telemetry.ConfigurationId);CampaignDebug.Restore(data.debugSettings);telemetry.StartRun("load");StopAllCoroutines();Phase=CampaignPhase.Transition;flowBusy=true;
        foreach(var director in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))director.Stop();
        cutscene.playableAsset=null;StopCampaignMusic();transitionAudio.Stop();RhythmManager.StopMusic();ClearWorld();
        yield return SceneManager.LoadSceneAsync(data.scene);yield return null;
        var world=new CampaignSaveWorld();
        RunState.NewCampaign();CampaignStateCodec.Restore(typeof(RunState),data.run,world.Resolve);
        float sceneOffset=Time.timeSinceLevelLoad-data.sceneTime,gameOffset=Time.time-data.gameTime,unscaledOffset=Time.unscaledTime-data.unscaledTime;
        Clock=new CampaignBeatClock();Clock.Reset(CampaignDebug.Value.tempo>0?CampaignDebug.Value.tempo:BaseWait,CampaignDebug.Value.window>0?CampaignDebug.Value.window:table.perfectWindow);
        CampaignStateCodec.Restore(Clock,data.clock,world.Resolve);
        bool rescue=(CampaignPhase)data.phase==CampaignPhase.LastChance;
        if(rescue)Clock.WindowOpened+=beat=>RunState.TotalHeartbeats++;
        else{Clock.WindowOpened+=OnWindow;Clock.CycleCompleted+=OnCycleCompleted;}
        savedClock=null;
        if(data.hasSavedClock&&data.savedClock!=null)
        {
            savedClock=new CampaignBeatClock();savedClock.Reset(CampaignDebug.Value.tempo>0?CampaignDebug.Value.tempo:BaseWait,CampaignDebug.Value.window>0?CampaignDebug.Value.window:table.perfectWindow);
            CampaignStateCodec.Restore(savedClock,data.savedClock,world.Resolve);
            savedClock.WindowOpened+=OnWindow;savedClock.CycleCompleted+=OnCycleCompleted;
        }
        WaveDirector.NewRun();
        if(data.hasWave)waves=new CampaignWaves(this,data.waveProgress);
        world.Create(data.world);yield return null;
        world.Apply(sceneOffset,gameOffset,unscaledOffset);
        CampaignStateCodec.Restore(this,data.director,world.Resolve,sceneOffset,gameOffset,unscaledOffset);
        CampaignStateCodec.Restore(typeof(GameManager),data.wall,world.Resolve);
        CampaignStateCodec.Restore(typeof(Tap),data.tap,world.Resolve);
        CampaignStateCodec.Restore(typeof(TapSec),data.tapSec,world.Resolve);
        CampaignStateCodec.Restore(typeof(CapsuleTrack),data.track,world.Resolve,sceneOffset,gameOffset,unscaledOffset);
        CampaignStateCodec.Restore(typeof(Spawn),data.spawn,world.Resolve);
        if(WaveDirector.Current!=null)CampaignStateCodec.Restore(WaveDirector.Current,data.wave,world.Resolve);
        telemetry.BeginChapter("loaded_partial");
        Phase=(CampaignPhase)data.phase;flowBusy=false;inputBlockedThroughFrame=Time.frameCount;
        ui.Gameplay(0);ui.Main.text=data.main;ui.Detail.text=data.detail;ui.Hud.text="";ui.Caption.text=data.caption;
        ui.Main.fontSize=data.mainFontSize;ui.Main.color=data.mainColor;ui.Detail.color=data.detailColor;ui.Black.color=data.black;ui.Night.color=data.night;
        foreach(var visual in FindObjectsByType<BarricadeHealthVisual>(FindObjectsSortMode.None))visual.RefreshNow();
        foreach(var heart in FindObjectsByType<HeartFill>(FindObjectsSortMode.None))heart.RefreshNow();
        musicFader.SetVoiceActive(false);musicFader.SnapToPhase();
        audioSource.loop=false;audioSource.clip=ResolveMusicTrack(data.musicTrack);
        if(data.musicPlaying&&Chapter.number!=9&&audioSource.clip!=null)
        {
            audioSource.time=Mathf.Clamp(data.musicTime,0,Mathf.Max(0,audioSource.clip.length-.05f));
            audioSource.Play();musicPlaybackActive=true;
        }
        if(Phase==CampaignPhase.Results)ui.Detail.text=CampaignHudPresentation.Results(RunState.Perfects,RunState.Judgements.Count,RunState.Mistakes,RunState.Score,failed);
        if(Phase==CampaignPhase.Choice){ui.Detail.text="Q：放走沙威\nE：处决沙威";UpdateChoiceDisplay();}
        if(Phase==CampaignPhase.LastChance){ui.Main.text="听心跳，抓住静止的瞬间";ui.Detail.text="在下一次静止间歇按空格，尝试再次站稳。";}
        SyncTimers();
    }
    public IEnumerator RecoverLoadFailure()
    {Phase=CampaignPhase.Transition;ClearWorld();yield return SceneManager.LoadSceneAsync("Title");ShowTitle();}

}
