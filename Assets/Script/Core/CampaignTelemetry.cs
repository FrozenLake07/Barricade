using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;

// Local playtest files only. Recording failures never stop the campaign.
public sealed class CampaignTelemetry : MonoBehaviour
{
    [Serializable] sealed class RunReport {public int schema=3;public string ending;public int endingSourceChapter;public bool hopeMusicStarted;public bool nonDesign;public string debugLabel,debugSettings;public string runId,configurationId,label,startedUtc,status;public CampaignAttemptMetrics[] attempts;}
    [Serializable] sealed class EventRow {public string utc,kind,phase,detail;public int chapter,attempt,beat,hp;public float time;}
    [Serializable] sealed class Config {
        public int schema=5,limit,lastChances,targetBeats;public float perfectWindow,chordGap,crossfade;
        public int chordPerfect,chordOrdinary;public string label;public ConfigChapter[] chapters;public string[] frames;
    }
    [Serializable] sealed class ConfigChapter {
        public int number,preparation,defense,choice,epilogue,cinematic;public string title,introduction,waveA,waveB;
        public float startingWait,combatDamageMultiplier;public bool accelerate;public string[] transitions;
    }
    public static CampaignTelemetry Current {get;private set;}
    public static string RootPath=>Path.Combine(Application.persistentDataPath,"Playtests");
    public string ConfigurationId {get;private set;}
    public string DirectoryPath {get;private set;}
    public CampaignMetrics Metrics {get;private set;}=new CampaignMetrics();
    int escortStartSuccesses,escortStartFailures;
    StoryDirector owner;string runId,started,status="running";bool recording,failed,nonDesign;float sinceFlush;
    readonly StringBuilder pending=new StringBuilder();
    static string Name(UnityEngine.Object value)=>value!=null?value.name:"";
    static string Transition(ChapterTransition t)=>t==null?"":string.Join("|",Name(t.cinematicPrefab),Name(t.timeline),Name(t.audio),t.placeholderSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),t.allowSkip,t.waitForSignal,t.signalTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),t.caption);
    public static string ConfigurationJson(ChapterTable table)
    {
        var data=new Config{limit=table.constructionLimit,lastChances=table.lastChancesPerChapter,targetBeats=table.targetBeatBudget,perfectWindow=table.perfectWindow,chordGap=table.chordMaxGap,crossfade=table.firstChapterCrossfadeSeconds,chordPerfect=table.chordPerfectHP,chordOrdinary=table.chordOrdinaryHP,label=table.playtestLabel};
        data.chapters=table.chapters.Select(c=>new ConfigChapter{number=c.number,title=c.title,introduction=c.introduction,preparation=c.preparationBeats,defense=c.defenseBeats,choice=c.choiceBeats,epilogue=c.epilogueBeats,cinematic=c.cinematicBeats,combatDamageMultiplier=c.combatDamageMultiplier,startingWait=c.startingWait,accelerate=c.accelerate,waveA=c.waveA!=null?JsonUtility.ToJson(c.waveA):"",waveB=c.waveB!=null?JsonUtility.ToJson(c.waveB):"",transitions=new[]{Transition(c.entry),Transition(c.prepareToDefense),Transition(c.exit)}}).ToArray();
        data.frames=table.firstChapterFrames.Select(f=>string.Join("|",Name(f.image),f.beats,f.subtitle,Name(f.voiceOver),f.voiceDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        return JsonUtility.ToJson(data,true);
    }
    public static string Fingerprint(ChapterTable table)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(ConfigurationJson(table)))).Replace("-","").ToLowerInvariant();}
    public void Initialize(StoryDirector director){owner=director;Current=this;ConfigurationId=Fingerprint(owner.table);}
    public void StartRun(string origin)
    {
        FinishRun("restarted");nonDesign=CampaignDebug.Value.nonDesign;Metrics=new CampaignMetrics();failed=false;sinceFlush=0;pending.Clear();
        ConfigurationId=Fingerprint(owner.table);
        if(!owner.table.recordPlaytests)return;
        runId=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
        started=DateTime.UtcNow.ToString("O");status="running";DirectoryPath=Path.Combine(RootPath,runId);
        try{Directory.CreateDirectory(DirectoryPath);WriteAtomic("configuration.json",ConfigurationJson(owner.table));recording=true;Record("run_start",origin);Flush();}
        catch(Exception e){DisableRecording(e);}
    }
    public void MarkDebug(string command){nonDesign=true;Record("debug_override","非设计值;"+command);Flush();}
    public void BeginChapter(string origin)
    {
        if(!recording)return;
        EndChapter("interrupted");Metrics.Begin(RunState.Chapter,owner.Chapter.title,GameManager.WallHP,GameManager.WallMaxHP,origin);
        escortStartSuccesses=RunState.EscortSuccesses;escortStartFailures=RunState.EscortFailures;
        CaptureFinalState();
        Record("chapter_start",origin);Flush();
    }
    void CaptureFinalState()
    {
        var r=Metrics.Current;if(r==null)return;
        Metrics.ObserveHealth(GameManager.WallHP,GameManager.WallMaxHP,owner.ChapterBeat);
        r.escortSuccesses=Math.Max(0,RunState.EscortSuccesses-escortStartSuccesses);r.escortFailures=Math.Max(0,RunState.EscortFailures-escortStartFailures);r.carriedEscortSuccesses=RunState.EscortSuccesses;r.carriedEscortFailures=RunState.EscortFailures;r.reloadMultiplier=RunState.ReloadMultiplier;r.ammoCapacity=3+RunState.AmmoBonus;r.decision=RunState.Decision.ToString();
        if(WaveDirector.Current!=null&&WaveDirector.Current.Script.chapter==RunState.Chapter)r.waveVariant=WaveDirector.Current.Script.variant;
    }
    public void EndChapter(string outcome)
    {
        if(!recording||Metrics.Current==null)return;
        CaptureFinalState();Record("chapter_end",outcome);Metrics.End(outcome);Flush();
    }
    public void PhaseChanged(CampaignPhase phase){if(recording){Record("phase",phase.ToString());Flush();}}
    public void Judge(bool perfect,bool missed)
    {if(!recording)return;Metrics.Judge(perfect,missed,owner.Phase.ToString());Record("judgement",perfect?"perfect":missed?"missed":"ordinary");}
    public void Beat(CampaignPhase phase,int beat)
    {
        if(!recording||Metrics.Current==null)return;
        Metrics.Current.completedBeats++;Metrics.ObserveHealth(GameManager.WallHP,GameManager.WallMaxHP,owner.ChapterBeat);
        Record("beat",phase+":"+beat);
    }
    public void HealthChanged(int before,int after,int requested)
    {if(!recording)return;Metrics.HealthChanged(before,after,requested,GameManager.WallMaxHP,owner.ChapterBeat);Record("health",before+"→"+after+" requested="+requested);}
    public void Record(string kind,string detail="")
    {
        if(!recording)return;
        Metrics.Event(kind);
        var row=new EventRow{utc=DateTime.UtcNow.ToString("O"),kind=kind,detail=detail,phase=owner.Phase.ToString(),chapter=RunState.Chapter,attempt=Metrics.Current?.attempt??0,beat=owner.ChapterBeat,hp=GameManager.WallHP,time=Time.realtimeSinceStartup};
        pending.Append(JsonUtility.ToJson(row)).Append('\n');
    }
    void Update()
    {
        if(!recording)return;
        Metrics.Tick(Time.unscaledDeltaTime,CampaignSession.Suspended||DebugConsole.SimulationPaused,owner.Phase.ToString(),GameManager.WallHP,GameManager.WallMaxHP,owner.ChapterBeat);
        sinceFlush+=Time.unscaledDeltaTime;if(sinceFlush>=5){CaptureFinalState();Flush();}
    }
    void WriteAtomic(string name,string text)
    {
        CampaignPlaytestStorage.WriteAtomic(Path.Combine(DirectoryPath,name),text);
    }
    public void Flush()
    {
        if(!recording)return;
        try{
            if(pending.Length>0){File.AppendAllText(Path.Combine(DirectoryPath,"events.jsonl"),pending.ToString(),new UTF8Encoding(false));pending.Clear();}
            WriteAtomic("summary.csv",Metrics.Csv(nonDesign));
            WriteAtomic("run.json",JsonUtility.ToJson(new RunReport{ending=RunState.Ending.ToString(),endingSourceChapter=RunState.EndingSourceChapter,hopeMusicStarted=RunState.HopeMusicStarted,nonDesign=nonDesign,debugLabel=nonDesign?"非设计值":"设计值",debugSettings=nonDesign?JsonUtility.ToJson(CampaignDebug.Value):null,runId=runId,configurationId=ConfigurationId,label=owner.table.playtestLabel,startedUtc=started,status=status,attempts=Metrics.attempts.ToArray()},true));sinceFlush=0;
        }catch(Exception e){DisableRecording(e);}
    }
    void DisableRecording(Exception e){recording=false;if(!failed){failed=true;Debug.LogWarning("[Playtests] 本地记录失败，游戏继续："+e.Message);}}
    public void FinishRun(string outcome)
    {
        if(!recording)return;
        EndChapter(outcome=="completed"?"completed":"interrupted");status=outcome;Record("run_end",outcome);Flush();recording=false;
    }
    void OnApplicationPause(bool paused){if(paused)Flush();}
    void OnApplicationQuit(){FinishRun("quit");}
    void OnDestroy(){FinishRun("stopped");if(Current==this)Current=null;}
}
