using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-300)]
public sealed class StoryDirector : MonoBehaviour
{
    public ChapterTable table;
    public Font font;
    public AudioClip titleMusic, chapterMusic;
    public GameObject endingPrefab;
    public GameObject screenPrefab;
    public UnityEvent transitionStarted = new UnityEvent(), transitionFinished = new UnityEvent();
    public static event Action<int,string> TransitionRequested;
    public static StoryDirector Current {get;private set;}
    public static bool Active => Current!=null;
    public static bool CombatRunning => Active&&!CampaignSession.InputBlocked&&Current.Phase==CampaignPhase.Defense;
    public static bool GameplayRunning => Active&&!CampaignSession.InputBlocked&&(Current.Phase==CampaignPhase.Preparation||Current.Phase==CampaignPhase.Defense);
    public static bool HeartbeatRunning => !CampaignSession.InputBlocked&&(GameplayRunning||(Active&&(Current.Phase==CampaignPhase.Choice||Current.Phase==CampaignPhase.Epilogue||Current.Phase==CampaignPhase.LastChance||Current.Phase==CampaignPhase.EndCard)));
    public CampaignPhase Phase {get;private set;}
    public ChapterDefinition Chapter => table.chapters[RunState.Chapter-1];
    public CampaignBeatClock Clock {get;private set;} = new CampaignBeatClock();
    public int ChapterBeat => phaseOffset+Clock.Opened;
    public int FrozenUntilBeat {get;private set;}
    public bool EnemiesFrozen => !CombatRunning||Clock.Opened<FrozenUntilBeat;
    public float BaseWait => Chapter.number==8&&RunState.Decision==JavertDecision.Executed?Chapter.startingWait/1.1f:Chapter.startingWait;
    CampaignUI ui;
    CampaignSession session;
    AudioSource audioSource;
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
        if(table==null){table=ScriptableObject.CreateInstance<ChapterTable>();table.chapters=ChapterTable.CreateDefaults();}
        table.Validate();
        font=GameFont.Silver!=null?GameFont.Silver:font;
        if(font==null)Debug.LogError("[StoryDirector] 缺少 Silver 字体配置 Campaign/GameFont。");
        ui=new CampaignUI(transform,font,screenPrefab);audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.volume=.65f;audioSource.spatialBlend=0;
        cutscene=gameObject.AddComponent<PlayableDirector>();cutscene.playOnAwake=false;
        session=gameObject.AddComponent<CampaignSession>();session.Initialize(this,ui.View);
    }
    void Start(){ShowTitle();}
    void OnDestroy(){if(Current==this)Current=null;}
    void ShowTitle()
    {
        Phase=CampaignPhase.Title;flowBusy=false;titleSince=Time.unscaledTime;RunState.NewCampaign();WaveDirector.NewRun();
        RhythmManager.JudgementFromMusic=false;RhythmManager.StopMusic();
        ui.AdoptTitleScene(transform);ui.ShowTitle();session.Bind(ui.View);session.ShowTitle();PlayMusic(titleMusic);
    }
    void PlayMusic(AudioClip clip)
    {audioSource.Stop();audioSource.loop=false;audioSource.clip=clip;if(clip!=null)audioSource.Play();}
    void Update()
    {
        if(Current!=this||CampaignSession.InputBlocked)return;
        if(Phase==CampaignPhase.Title)
        {
            ui.AnimateTitle(Time.unscaledTime-titleSince);
            if(!flowBusy&&Input.GetKeyDown(KeyCode.F2))StartCoroutine(Calibrate(false));
            else if(session.FirstPrompt&&Input.anyKeyDown&&Time.unscaledTime>titleSince+.2f&&!flowBusy)session.NewGame();return;
        }
        if(Phase==CampaignPhase.Results)
        {
            if(Time.unscaledTime<titleSince+.25f)return;
            if(failed&&Input.GetKeyDown(KeyCode.R))StartCoroutine(BeginChapter(RunState.Chapter,true));
            else if(!failed&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)))StartCoroutine(BeginChapter(RunState.Chapter+1,false));
            return;
        }
        if(Phase==CampaignPhase.Finished)
        {if(Input.anyKeyDown&&Time.unscaledTime>titleSince+.5f)StartCoroutine(ReturnToTitle());return;}
        // 零血结算先于同帧齐射、波次和窗口处理。
        if(CombatRunning&&GameManager.WallHP<=0&&(Chapter.number==9||Clock.Opened>=Chapter.defenseBeats-4)){FailChapter();return;}
        if(!HeartbeatRunning)return;
        if(restRemaining>0)restRemaining=Mathf.Max(0,restRemaining-Time.deltaTime);
        Clock.Advance(Time.deltaTime);
        SyncTimers();
        if(Phase==CampaignPhase.LastChance)
        {
            ui.Main.text=Clock.IsPerfect?"最后一拍：现在按空格":"最后一拍：听心跳";
            if(Input.GetKeyDown(KeyCode.Space)&&Clock.ClaimPerfect())
            {GameManager.WallHP=Mathf.Max(1,Mathf.CeilToInt(GameManager.WallMaxHP*.1f));Clock=savedClock;Phase=CampaignPhase.Defense;inputBlockedThroughFrame=Time.frameCount;ui.Gameplay(Chapter.number==6?.62f:0);return;}
            if(Clock.Completed>=1)FailChapter();return;
        }
        if(Phase==CampaignPhase.Choice&&Clock.Opened>=2)HandleChoice();
        if(Phase==CampaignPhase.Epilogue&&Input.anyKeyDown)
        {if(!RunState.Memories.Contains(ui.Main.text))RunState.Memories.Add(ui.Main.text);ui.Detail.text="记住了。";}
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
        else if(Phase==CampaignPhase.Choice&&Clock.Completed>=Chapter.choiceBeats)
        {if(RunState.Decision==JavertDecision.Unchosen){RunState.Decision=JavertDecision.Released;ShowCaption("他最终放下了枪。未作选择，默认放走。");}CompleteChapter();}
        else if(Phase==CampaignPhase.Epilogue&&Clock.Completed>=Chapter.epilogueBeats)StartCoroutine(FinishCampaign());
    }
    IEnumerator BeginCampaign()
    {flowBusy=true;if(!PlayerPrefs.HasKey("Barricade.CalibrationReady"))yield return Calibrate(true);yield return Transition(table.opening,"opening","六月五日。",false);BoundaryBeat();yield return BeginChapter(1,false);}
    IEnumerator Calibrate(bool starting)
    {
        flowBusy=true;Phase=CampaignPhase.Calibration;audioSource.Stop();
        yield return OpeningCalibration.Run(ui,transform);
        if(!starting)ShowTitle();
    }
    void BoundaryBeat(){RunState.TotalHeartbeats++;SfxPlayer.Play("HeartbeatPulse/Pulse-V1",.55f);}
    IEnumerator BeginChapter(int number,bool retry)
    {
        if(number>10){yield return FinishCampaign();yield break;}
        flowBusy=true;ClearWorld();RunState.Chapter=number;
        if(number==5)RunState.SpySpared=true;
        if(number==7)RunState.Decision=JavertDecision.Unchosen;
        RunState.ResetChapter();failureReason=null;lastChancesUsed=0;restRemaining=0;FrozenUntilBeat=0;phaseOffset=0;constructionCycle=-1;builtHealth=0;
        GameManager.WallMaxHP=table.constructionLimit;GameManager.WallHP=Chapter.preparationBeats>0?0:table.constructionLimit;
        if(number==8&&RunState.Decision==JavertDecision.Executed)GameManager.AddWallHP(Mathf.CeilToInt(table.constructionLimit*.3f));
        yield return Transition(Chapter.entry,"chapter-entry",$"第{number}章  {Chapter.title}\n\n{Chapter.introduction}",number!=9);
        if(Chapter.epilogueBeats>0)
        {yield return LoadScene("1");Phase=CampaignPhase.Epilogue;ResetClock();ui.Card("街垒之后",Chapter.introduction);}
        else if(Chapter.preparationBeats>0)
        {yield return LoadScene("1");Phase=CampaignPhase.Preparation;ResetClock();ui.Gameplay(0);ShowCaption(Chapter.introduction);}
        else
        {yield return LoadScene("2");Phase=CampaignPhase.Defense;ResetClock();waves=new CampaignWaves(this);ui.Gameplay(0);}
        if(Phase==CampaignPhase.Epilogue){PlayMusic(chapterMusic);audioSource.loop=true;}else audioSource.Stop();RhythmManager.StopMusic();flowBusy=false;
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
        Clock=new CampaignBeatClock();Clock.Reset(BaseWait,table.perfectWindow);Clock.WindowOpened+=OnWindow;Clock.CycleCompleted+=OnCycleCompleted;
        windowJudged=false;constructionCycle=-1;judgementOffset=RunState.Judgements.Count;SyncTimers();
    }
    void OnWindow(int beat)
    {
        RunState.TotalHeartbeats++;
        if(GameplayRunning&&(Phase!=CampaignPhase.Defense||beat<Chapter.defenseBeats-4))
            EnsureJudgementSlot();
        foreach(var line in Chapter.lines)if(line.beat==phaseOffset+beat)ShowCaption(line.text);
        if(CombatRunning)waves?.Beat(beat);
        if(Phase==CampaignPhase.Epilogue)
        {
            string[] names={"昂若拉","库费拉克","格朗泰尔","加弗罗什","街垒上的人们","没有留下名字的人"};
            ui.Main.text=beat>=170?"但第二天，还是有人唱起了这首歌。":beat>=150?"他们中没有一个人活到第二天。":names[(beat-1)%names.Length];
            ui.Detail.text="任意键：记住";Clock.NextWait=Mathf.Lerp(.5f,1.2f,beat/183f);
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
        string caption=Chapter.number==2?"街尾有人。穿蓝制服。":Chapter.introduction;
        yield return Transition(Chapter.prepareToDefense,"prepare-to-defense",caption,Chapter.number!=9);
        yield return LoadScene("2");Phase=CampaignPhase.Defense;ResetClock();waves=new CampaignWaves(this);
        ui.Gameplay(Chapter.number==6?.62f:0);if(Phase==CampaignPhase.Epilogue){PlayMusic(chapterMusic);audioSource.loop=true;}else audioSource.Stop();RhythmManager.StopMusic();flowBusy=false;
    }
    void StartChoice()
    {
        waves?.Complete();ClearWorld();Phase=CampaignPhase.Choice;phaseOffset=120;ResetClock();
        ui.Card("处决间谍的枪，递到了他手里。","Q：放走 —— 下一章敌人减少20%\nE：处决 —— 回复30%耐久，下一章心跳加快10%");
    }
    void HandleChoice()
    {
        if(RunState.Decision!=JavertDecision.Unchosen)return;
        if(Input.GetKeyDown(KeyCode.Q))RunState.Decision=JavertDecision.Released;
        else if(Input.GetKeyDown(KeyCode.E)){RunState.Decision=JavertDecision.Executed;GameManager.AddWallHP(Mathf.CeilToInt(GameManager.WallMaxHP*.3f));}
        else return;
        ui.Main.text=RunState.Decision==JavertDecision.Released?"他走出门，朝天上开了一枪。":"枪声之后，酒馆里安静了。";
        ui.Detail.text="选择已记下。";
    }
    void BeginLastChance()
    {
        lastChancesUsed++;savedClock=Clock;Clock=new CampaignBeatClock();Clock.Reset(BaseWait,table.perfectWindow);
        Clock.WindowOpened+=beat=>RunState.TotalHeartbeats++;
        Phase=CampaignPhase.LastChance;ui.Card("最后一拍：听心跳","在下一次静止间歇按空格，恢复10%耐久。本章剩余抢救次数："+(table.lastChancesPerChapter-lastChancesUsed)+"。");
    }
    void FailChapter()
    {
        if(Chapter.number==9){StartCoroutine(BeginChapter(10,false));return;}
        if(!flowBusy)StartCoroutine(FailureTransition());
    }
    IEnumerator FailureTransition()
    {flowBusy=true;ClearWorld();yield return Transition(Chapter.exit,"chapter-failure",failureReason??"街垒没有守住。",false);failed=true;ShowResults();}
    void CompleteChapter()
    {
        if(flowBusy)return;
        waves?.Complete();
        if(Chapter.number==9){StartCoroutine(BeginChapter(10,false));return;}
        StartCoroutine(CompleteWithExit());
    }
    IEnumerator CompleteWithExit()
    {
        flowBusy=true;ClearWorld();
        string text=Chapter.number==5?(RunState.SpySpared?"他没有开枪。这让一些人很不高兴。":"酒馆里的人，没有再出来。"):$"第{Chapter.number}章结束";
        yield return Transition(Chapter.exit,"chapter-exit",text,false);
        failed=false;ShowResults();
    }
    void ShowResults()
    {
        flowBusy=false;ClearWorld();Phase=CampaignPhase.Results;titleSince=Time.unscaledTime;
        int windows=Mathf.Max(1,RunState.Judgements.Count);
        string graph="每10拍的Perfect：\n";
        for(int i=0;i<RunState.Judgements.Count;i+=10)
        {int count=0;for(int j=i;j<Mathf.Min(i+10,RunState.Judgements.Count);j++)if(RunState.Judgements[j]>0)count++;graph+=(i+1)+"–"+Mathf.Min(i+10,RunState.Judgements.Count)+": "+new string('|',count)+"   ";if((i/10)%3==2)graph+="\n";}
        ui.Card(failed?"本章未守住":"本章结束",$"Perfect {RunState.Perfects*100f/windows:F0}%   失误 {RunState.Mistakes}   分数 {RunState.Score}\n\n{graph}\n\n"+(failed?"R：重开本章":"Enter：继续下一章"));
    }
    IEnumerator FinishCampaign()
    {
        flowBusy=true;ClearWorld();
        ui.Gameplay(0);ui.Hud.text="";ui.Caption.text="";
        GameObject ending=null;
        if(endingPrefab!=null){ending=Instantiate(endingPrefab,new Vector3(0,-2,0),Quaternion.identity);ending.GetComponent<HeroSequenceController>()?.Play();}
        yield return Transition(table.ending,"ending","但第二天，还是有人唱起了这首歌。",true,ending!=null);
        BoundaryBeat();Phase=CampaignPhase.Finished;flowBusy=false;titleSince=Time.unscaledTime;
        ui.Card("街垒之后",$"这一夜，共听见 {RunState.TotalHeartbeats} 拍。\n\n但第二天，还是有人唱起了这首歌。\n\n按任意键回到开始界面");
    }
    IEnumerator ReturnToTitle()
    {flowBusy=true;yield return SceneManager.LoadSceneAsync("Title");ShowTitle();}

    // 动画可用Signal调用此方法完成；空接口使用定时文字卡，暂停Timeline不会误切场。
    public void CompleteTransition()=>signalComplete=true;
    public void PlayTransitionCue(AudioClip clip){if(clip!=null&&RunState.Chapter!=9)audioSource.PlayOneShot(clip);}
    IEnumerator Transition(ChapterTransition slot,string key,string text,bool useMusic,bool transparent=false)
    {
        Phase=CampaignPhase.Transition;signalComplete=false;
        if(!string.IsNullOrEmpty(slot.caption))text=slot.caption;
        TransitionRequested?.Invoke(RunState.Chapter,key);transitionStarted.Invoke();
        if(!transparent)ui.Card(text,slot.allowSkip?"空格：跳过":"");
        PlayMusic(RunState.Chapter==9?null:slot.audio!=null?slot.audio:(useMusic?chapterMusic:null));
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
            if(CampaignSession.Suspended){yield return null;continue;}
            elapsed+=Time.unscaledDeltaTime;
            if(!timeline&&!slot.waitForSignal&&elapsed>=Mathf.Max(.05f,slot.placeholderSeconds))break;
            if(slot.allowSkip&&elapsed>.25f&&Input.GetKeyDown(KeyCode.Space))break;
            if(slot.waitForSignal&&!timeline&&elapsed>=Mathf.Max(1,slot.signalTimeoutSeconds))
            {Debug.LogWarning("[StoryDirector] 过场完成信号超时："+key);break;}
            yield return null;
        }
        if(timeline){director.stopped-=stopped;director.Stop();}
        if(visual!=null)Destroy(visual);
        cutscene.playableAsset=null;audioSource.Stop();transitionFinished.Invoke();
    }
    public void ShowCaption(string text){ui.Caption.text=text;captionUntil=Time.time+6f;}
    void UpdateHUD()
    {
        if(!GameplayRunning)return;
        var gun=FindFirstObjectByType<SBullet>();
        ui.Hud.color=gun!=null&&Time.unscaledTime<gun.ReloadFlashUntil?new Color(1,.8f,.35f):new Color(.89f,.88f,.83f);
        string ammo=gun!=null?(Chapter.number==9?"   弹药 ∞":$"   弹药 {gun.CampaignAmmo}/{gun.CampaignCapacity}  R换弹"):"";
        ui.Hud.text=$"第{RunState.Chapter}章  {Chapter.title}   {Mathf.Min(183,ChapterBeat)}/183拍   耐久 {GameManager.WallHP}/{GameManager.WallMaxHP}{ammo}\n"+
            "A/D 换轨   W 空中 / S 地面   空格开火"+(Chapter.number==8&&WaveDirector.Current!=null?$"   炮弹拦截 {WaveDirector.Current.ShellInterceptions}/{WaveDirector.Current.Script.requiredShellInterceptions}":"")+(Chapter.number==4?$"   换弹 ×{RunState.ReloadMultiplier:F1}":"");
    }
    void EnsureJudgementSlot()
    {while(RunState.Judgements.Count<=judgementOffset+Clock.Completed)RunState.Judgements.Add(0);}
    public void RecordInput(bool perfect)
    {
        if(windowJudged)return; // 每周期一次，后续误按不能覆盖已记录的Perfect。
        windowJudged=true;EnsureJudgementSlot();if(!RunState.TryRecordAt(judgementOffset+Clock.Completed,perfect))return;
        Clock.NextWait=perfect&&Chapter.accelerate?Mathf.Max(.5f,Clock.NextWait-.05f):BaseWait;
    }
    void OnCycleCompleted(int beat)
    {
        if(Phase==CampaignPhase.Preparation&&!windowJudged&&constructionCycle==Clock.Completed)
        {
            var result=construction.Close();
            if(result==ConstructionResult.Wrong){restRemaining=3;SfxPlayer.Play("SFX-QTE-03");}
        }
        bool scoring=GameplayRunning&&(Phase!=CampaignPhase.Defense||beat<Chapter.defenseBeats-4);
        if(scoring&&!windowJudged)RecordInput(false);
        windowJudged=false;
    }
    public void ConstructionInput(Tap tap)
    {
        if(CampaignSession.InputBlocked||Time.frameCount<=inputBlockedThroughFrame||Phase!=CampaignPhase.Preparation)return;
        if(constructionCycle!=Clock.Completed)
        {
            constructionCycle=Clock.Completed;
            ConstructionJudgement.Prompt(Chapter.number,constructionCycle+1,out int first,out int second);
            construction.Reset(first,second,table.chordMaxGap);
            if(Chapter.number==1&&constructionCycle+1==49)ShowCaption("这一拍，同时按下两个字母。先后相差不超过120毫秒。");
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
        if(result==ConstructionResult.Wrong){restRemaining=3;SfxPlayer.Play("SFX-QTE-03");return;}
        if(result==ConstructionResult.Miss)return;
        if(perfect)Clock.ClaimPerfect();
        tap.NotifyCampaignInput(perfect,construction.Chord&&perfect?1.12246f:1f);
        int raw=construction.Chord?(perfect?table.chordPerfectHP:table.chordOrdinaryHP):(perfect?10:0);
        builtHealth+=(float)(raw*ConstructionJudgement.RewardScale(Chapter.number,Chapter.preparationBeats,table.constructionLimit,table.chordPerfectHP));
        int total=Mathf.FloorToInt(builtHealth+.001f),gain=total-RunState.BuiltHP;
        RunState.BuiltHP=total;GameManager.AddWallHP(gain);
    }
    public void CombatInput(TapSec input)
    {
        if(!CombatRunning||Time.frameCount<=inputBlockedThroughFrame||(WaveDirector.Current!=null&&WaveDirector.Current.FinaleStarted))return;
        if(Input.GetKeyDown(KeyCode.W))input.CampaignSetAim("Up");else if(Input.GetKeyDown(KeyCode.S))input.CampaignSetAim("Down");
        var gun=input.sbullet;if(gun==null)return;
        if(Input.GetKeyDown(KeyCode.R))gun.BeginCampaignReload();
        if(!Input.GetKeyDown(KeyCode.Space)||!gun.CanFire)return;
        bool perfect=Clock.IsPerfect;
        if(gun.CurrentTarget()==null)perfect=false;
        if(perfect&&!Clock.ClaimPerfect())return;
        RecordInput(perfect);gun.Fire(TapSec.ShootStatu,perfect);
    }
    IEnumerator AfterDefense()
    {
        flowBusy=true;
        if(Chapter.number==9){yield return BeginChapter(10,false);yield break;}
        waves?.Complete();
        Phase=CampaignPhase.EndCard;
        ui.Card($"第{Chapter.number}章结束", "");
        Clock=new CampaignBeatClock();Clock.Reset(BaseWait,table.perfectWindow);
        Clock.WindowOpened+=beat=>RunState.TotalHeartbeats++;
        while(Clock.Completed<2||CampaignSession.Suspended)yield return null;
        if(Chapter.choiceBeats>0){StartChoice();flowBusy=false;}
        else {flowBusy=false;CompleteChapter();}
    }
    void ClearWorld()
    {
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
        var data=new CampaignSaveData{chapter=RunState.Chapter,phase=(int)Phase,utc=DateTime.UtcNow.ToString("O"),scene=SceneManager.GetActiveScene().name,
            sceneTime=Time.timeSinceLevelLoad,gameTime=Time.time,unscaledTime=Time.unscaledTime,world=state,
            director=CampaignStateCodec.Capture(this,world.Reference,SavedDirectorFields),clock=CampaignStateCodec.Capture(Clock,world.Reference),
            hasSavedClock=savedClock!=null,savedClock=savedClock!=null?CampaignStateCodec.Capture(savedClock,world.Reference):null,
            run=CampaignStateCodec.Capture(typeof(RunState),world.Reference),wall=CampaignStateCodec.Capture(typeof(GameManager),world.Reference,"WallHP","WallMaxHP"),
            tap=CampaignStateCodec.Capture(typeof(Tap),world.Reference,"beattime","oribeattime","Setbeattime","perfecttime","Setperfecttime"),
            tapSec=CampaignStateCodec.Capture(typeof(TapSec),world.Reference),track=CampaignStateCodec.Capture(typeof(CapsuleTrack),world.Reference),
            spawn=CampaignStateCodec.Capture(typeof(Spawn),world.Reference,"nextGroupId"),
            main=ui.Main.text,detail=ui.Detail.text,hud=ui.Hud.text,caption=ui.Caption.text,mainFontSize=ui.Main.fontSize,
            black=ui.Black.color,night=ui.Night.color,mainColor=ui.Main.color,detailColor=ui.Detail.color,
            musicPlaying=audioSource.isPlaying||(Phase==CampaignPhase.Epilogue&&audioSource.clip!=null),musicTime=audioSource.clip!=null?audioSource.time:0};
        if(WaveDirector.Current!=null)
        {
            data.hasWave=true;data.waveProgress=WaveDirector.Current.CaptureProgress();
            data.wave=CampaignStateCodec.Capture(WaveDirector.Current,world.Reference,"cannon","eventsBeat","eventsThisBeat","reducedIndex","lastVolleyBeat",
                "<FinaleStarted>k__BackingField","<ShellInterceptions>k__BackingField");
        }
        data.Validate();return data;
    }
    public IEnumerator RestoreSavedGame(CampaignSaveData data)
    {
        data.Validate();StopAllCoroutines();Phase=CampaignPhase.Transition;flowBusy=true;
        foreach(var director in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))director.Stop();
        cutscene.playableAsset=null;audioSource.Stop();RhythmManager.StopMusic();ClearWorld();
        yield return SceneManager.LoadSceneAsync(data.scene);yield return null;
        var world=new CampaignSaveWorld();
        RunState.NewCampaign();CampaignStateCodec.Restore(typeof(RunState),data.run,world.Resolve);
        float sceneOffset=Time.timeSinceLevelLoad-data.sceneTime,gameOffset=Time.time-data.gameTime,unscaledOffset=Time.unscaledTime-data.unscaledTime;
        Clock=new CampaignBeatClock();Clock.Reset(BaseWait,table.perfectWindow);
        CampaignStateCodec.Restore(Clock,data.clock,world.Resolve);
        bool rescue=(CampaignPhase)data.phase==CampaignPhase.LastChance;
        if(rescue)Clock.WindowOpened+=beat=>RunState.TotalHeartbeats++;
        else{Clock.WindowOpened+=OnWindow;Clock.CycleCompleted+=OnCycleCompleted;}
        savedClock=null;
        if(data.hasSavedClock&&data.savedClock!=null)
        {
            savedClock=new CampaignBeatClock();savedClock.Reset(BaseWait,table.perfectWindow);
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
        Phase=(CampaignPhase)data.phase;flowBusy=false;inputBlockedThroughFrame=Time.frameCount;
        ui.Gameplay(0);ui.Main.text=data.main;ui.Detail.text=data.detail;ui.Hud.text=data.hud;ui.Caption.text=data.caption;
        ui.Main.fontSize=data.mainFontSize;ui.Main.color=data.mainColor;ui.Detail.color=data.detailColor;ui.Black.color=data.black;ui.Night.color=data.night;
        foreach(var visual in FindObjectsByType<BarricadeHealthVisual>(FindObjectsSortMode.None))visual.RefreshNow();
        if(data.musicPlaying&&Chapter.number!=9){PlayMusic(chapterMusic);audioSource.loop=Phase==CampaignPhase.Epilogue;if(audioSource.clip!=null)audioSource.time=Mathf.Clamp(data.musicTime,0,Mathf.Max(0,audioSource.clip.length-.05f));}
        SyncTimers();
    }
    public IEnumerator RecoverLoadFailure()
    {Phase=CampaignPhase.Transition;ClearWorld();yield return SceneManager.LoadSceneAsync("Title");ShowTitle();}

}
