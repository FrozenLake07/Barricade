using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 波次节流、到达反推与诊断集中在这里；Spawn仅执行资源生成。
[DefaultExecutionOrder(-200)]
public sealed partial class WaveDirector : MonoBehaviour
{
    sealed class Pending
    {
        public WaveEvent source;
        public int beat, alignTo, lane;
        public bool done;
    }
    public static WaveDirector Current {get;private set;}
    static readonly Dictionary<int,int> variants=new Dictionary<int,int>();
    static readonly Dictionary<int,int> nextVariants=new Dictionary<int,int>();
    static readonly System.Random random=new System.Random();
    static Vector3? carriedCannon;
    readonly List<Pending> pending=new List<Pending>();
    readonly List<string> history=new List<string>();
    readonly Dictionary<int,int> laneLast=new Dictionary<int,int>();
    StoryDirector story;
    public WaveScript Script {get;private set;}
    public Collider2D Wall {get;private set;}
    Spawn[] spawns;
    Cannon cannon;
    int eventsBeat=-1,eventsThisBeat,reducedIndex;
    float debugDensityCarry;
    bool debug;
    public bool FinaleStarted {get;private set;}
    public int ShellInterceptions {get;private set;}
    public void RecordShellInterception(){if(!FinaleStarted&&StoryDirector.CombatRunning){ShellInterceptions++;CampaignTelemetry.Current?.Record("shell_intercept");}}
    int lastVolleyBeat=-1;
    UnityEngine.UI.Text debugText;
    GameObject debugCanvas;
    public double Position => WaveTiming.Position(story.Clock.Completed,story.Clock.Elapsed,story.Clock.Wait,story.Clock.Window);
    public float Period => story.Clock.Period;
    public bool Frozen => story.EnemiesFrozen||FinaleStarted;
    public bool Finished => FinaleStarted&&story.Clock.Opened>=Script.defenseBeats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRun(){Current=null;variants.Clear();nextVariants.Clear();carriedCannon=null;}
    public static void NewRun(){variants.Clear();carriedCannon=null;}
    public void Initialize(StoryDirector owner,bool tick=true,string forcedVariant=null)
    {
        story=owner;Current=this;
        var chapter=story.Chapter;
        if(forcedVariant!=null)variants[chapter.number]=forcedVariant=="A"?0:1;
        if(!variants.TryGetValue(chapter.number,out int version)){version=nextVariants.TryGetValue(chapter.number,out int next)?next:random.Next(2);variants[chapter.number]=version;}
        Script=version==0?chapter.waveA:chapter.waveB;
        if(Script==null)Script=chapter.waveA;
        if(Script==null)throw new InvalidOperationException("未配置本章波次脚本："+chapter.number);
        Script.Validate();
        if(CampaignTelemetry.Current?.Metrics.Current!=null)CampaignTelemetry.Current.Metrics.Current.waveVariant=Script.variant;
        spawns=FindObjectsByType<Spawn>(FindObjectsSortMode.None).Where(s=>s.isActiveAndEnabled).OrderBy(s=>s.transform.position.y).ToArray();
        Wall=FindObjectsByType<Collider2D>(FindObjectsSortMode.None).FirstOrDefault(c=>c.enabled&&GameManager.IsWall(c));
        if(spawns.Length!=3||Wall==null)throw new InvalidOperationException("波次需要三轨生成点和启用的街垒碰撞体。");
        foreach(var e in Script.events)
        {
            if(e.lanes.Length>0)
            {
                // 同一个多轨事件以同一原子批次生成，不被节流拆散。
                pending.Add(new Pending{source=e,beat=e.beat,alignTo=e.alignTo,lane=e.lane});
            }
            else for(int i=0;i<e.count;i++)pending.Add(new Pending{source=e,beat=e.beat+i*e.spacing,alignTo=e.alignTo<0?-1:e.alignTo+i*e.spacing,lane=e.lane});
        }
        // 开场第0拍立即出场；不必等到第一次Perfect窗口。
        if(tick)Tick();
    }
    [Serializable] public sealed class Progress
    {
        public string variant;public bool[] done;public int[] lanes,last;
        public bool hasCarriedCannon;public Vector3 carried;
    }
    public Progress CaptureProgress()=>new Progress{variant=Script.variant,done=pending.Select(p=>p.done).ToArray(),
        lanes=laneLast.Keys.ToArray(),last=laneLast.Values.ToArray(),hasCarriedCannon=carriedCannon.HasValue,carried=carriedCannon??Vector3.zero};
    public void RestoreProgress(Progress data)
    {
        if(data.done==null||data.done.Length!=pending.Count)throw new InvalidOperationException("存档波次与当前章节配置不一致。");
        for(int i=0;i<pending.Count;i++)pending[i].done=data.done[i];
        laneLast.Clear();for(int i=0;i<data.lanes.Length;i++)laneLast[data.lanes[i]]=data.last[i];
        carriedCannon=data.hasCarriedCannon?(Vector3?)data.carried:null;
    }
    public void MarkCleared(){nextVariants[story.Chapter.number]=Script.variant=="A"?1:0;}
    public float LaneY(int lane)=>spawns[Mathf.Clamp(lane-1,0,2)].transform.position.y;
    public float WallX(int lane)
    {
        float y=LaneY(lane);
        foreach(var hit in Physics2D.RaycastAll(new Vector2(Wall.bounds.min.x-30,y),Vector2.right,60))
            if(hit.collider==Wall)return hit.point.x;
        return Wall.bounds.min.x;
    }
    bool OpeningShortcut(Pending p)=>Script.chapter==3&&p.beat==0&&p.source.tag=="opening";
    float StartX(Pending p)=>spawns[p.lane-1].WaveEntryX(OpeningShortcut(p),p.source.advance);
    public float SquadThrowX(int lane)
    {
        var camera=Camera.main;
        float left=camera!=null&&camera.orthographic?camera.transform.position.x-camera.orthographicSize*camera.aspect:spawns[lane-1].transform.position.x;
        return Mathf.Min(WallX(lane)-4,left+3);
    }
    float Speed(Pending p)=>spawns[p.lane-1].WaveSpeed;
    public int ImpactBeat(int beat)=>beat;
    public void Beat(int beat){if(story!=null&&StoryDirector.CombatRunning)Tick();}
    void Update()
    {
        if(story==null)return;
        if(CampaignSession.Suspended){if(debugCanvas!=null)debugCanvas.SetActive(false);return;}
        if(debugCanvas!=null)debugCanvas.SetActive(debug);
        if(!DebugConsole.CapturingInput&&Input.GetKeyDown(KeyCode.F4)){debug=!debug;if(debugCanvas==null)CreateDebugUI();debugCanvas.SetActive(debug);}
        if(StoryDirector.CombatRunning&&!DebugConsole.SimulationPaused)Tick();
        if(debug&&debugText!=null)DrawDebug();
    }
    public void Tick()
    {
        if(Script==null)return;
        if(Position>=Script.defenseBeats-4)
        {
            if(GameManager.WallHP>0)
            {
                if(ShellInterceptions<Script.requiredShellInterceptions)story.FailWaveRequirement($"火炮仍在压制街垒。需要拦截{Script.requiredShellInterceptions}发炮弹，已拦截{ShellInterceptions}发。");
                else FinalVolley();
            }
            return;
        }
        if(Frozen)return;
        int now=(int)Math.Floor(Position+.0001);
        if(now!=eventsBeat){eventsBeat=now;eventsThisBeat=0;}
        foreach(var p in pending)
        {
            if(p.done||eventsThisBeat>=Script.maxEventsPerBeat)continue;
            float distance=WallX(p.lane)-.3f-StartX(p);
            if(!WaveTiming.Due(Position,p.beat,p.alignTo,distance,Speed(p),Period))continue;
            int[] lanes=p.source.lanes.Length>0?p.source.lanes:new[]{p.lane};
            bool entity=p.source.type==WaveEventType.Soldier||p.source.type==WaveEventType.Squad||p.source.type==WaveEventType.Decoy||p.source.type==WaveEventType.Escort;
            if(entity)
            {
                if(lanes.Any(l=>laneLast.TryGetValue(l,out int last)&&now-last<Script.laneCooldown))continue;
                if(p.source.type==WaveEventType.Escort&&CampaignActor.ActiveEscort!=null)continue;
                int repeats=p.source.type==WaveEventType.Soldier?Mathf.FloorToInt(debugDensityCarry+CampaignDebug.Value.density):1;
                if(ActiveCount()+Cost(p)*repeats>DebugCap)continue;
            }
            p.done=true;
            eventsThisBeat++;
            if(entity)foreach(int lane in lanes)laneLast[lane]=now;
            int copies=1;
            if(p.source.type==WaveEventType.Soldier){debugDensityCarry+=CampaignDebug.Value.density;copies=Mathf.FloorToInt(debugDensityCarry);debugDensityCarry-=copies;}
            for(int copy=0;copy<copies;copy++)Execute(p,lanes);
            Log($"{Position:F1} 生成 {p.source.type} {p.source.tag}");
        }
    }
    int Cost(Pending p)=>p.source.type==WaveEventType.Squad?3:p.source.lanes.Length>0?p.source.lanes.Length:1;
    bool ReducedSoldier(string tag)
    {
        if(tag=="opening")return false;
        float density=story.Chapter.number==6&&RunState.SpySpared?.85f:story.Chapter.number==8&&RunState.Decision==JavertDecision.Released?.8f:1f;
        int n=reducedIndex++;return density<1&&(int)((n+1)*density)==(int)(n*density);
    }
    public int ActiveCount()=>FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).Count(h=>h.HP>0)
        +FindObjectsByType<CampaignActor>(FindObjectsSortMode.None).Count(a=>a.escort);
    void Execute(Pending p,int[] lanes)
    {
        var e=p.source;
        if(e.type==WaveEventType.Cannon){EnsureCannon(e.tag=="cannon-persist");return;}
        if(e.type==WaveEventType.Shell){EnsureCannon();if(cannon!=null)cannon.FireWave(ImpactBeat(p.beat+Script.shellTravelBeats),p.lane,e.tag);return;}
        if(e.type==WaveEventType.Decoy||e.type==WaveEventType.Escort){Actor(p);return;}
        if(e.type==WaveEventType.Squad)
        {
            var members=spawns[p.lane-1].SpawnAt(e.type,e.advance,3,OpeningShortcut(p));
            var group=new GameObject("WaveSquad_"+e.tag);group.transform.SetParent(transform,false);
            int baseBeat=Math.Max(p.beat,(int)Math.Ceiling(Position+3-Script.squadImpactOffsets.Min()));
            group.AddComponent<WaveSquad>().Initialize(this,members,p.lane,baseBeat,e.tag);
            CampaignTelemetry.Current?.Record("enemy_spawn",$"Squad;count={members.Length};lane={p.lane};tag={e.tag}");
            return;
        }
        foreach(int lane in lanes)
        {
            if(ReducedSoldier(e.tag))continue;
            var members=spawns[lane-1].SpawnAt(WaveEventType.Soldier,e.advance,1,OpeningShortcut(p));
            if(members.Length==0)continue;
            var threat=members[0].AddComponent<WaveThreat>();
            int target=p.alignTo;
            if(target>=0&&target<Position+2)
            {int original=target;target=(int)Math.Ceiling(Position+2);Log($"节流后重排 {e.tag} 到达{original}→{target}，保留反应时间");}
            threat.Initialize(this,WaveEventType.Soldier,lane,target,e.tag);
            CampaignTelemetry.Current?.Record("enemy_spawn",$"Soldier;count={members.Length};lane={lane};tag={e.tag}");
        }
    }
    void EnsureCannon(bool persist=false)
    {
        if(cannon!=null)return;
        spawns[1].DeployCannonNow();cannon=FindFirstObjectByType<Cannon>();
        if(cannon!=null)
        {
            cannon.autoFire=false;cannon.ConfigureSpawn(Wall);
            if(persist)cannon.ResumeWaveDeployment(carriedCannon??new Vector3(spawns[1].transform.position.x+1.5f,spawns[1].transform.position.y-.59375f,0));
        }
    }
    void Actor(Pending p)
    {
        bool escort=p.source.type==WaveEventType.Escort;
        var source=Resources.Load<GameObject>("enemy/Leader");
        var go=new GameObject(escort?"GavrocheSilhouette":"SilentPrisoner");
        go.transform.position=new Vector3(escort?Wall.bounds.max.x-.3f:WallX(p.lane)-1.2f,LaneY(p.lane),0);
        var sr=go.AddComponent<SpriteRenderer>();var child=Resources.Load<Sprite>("HeroAnimations/Frames/Walk/Hero_Walk_01");
        sr.sprite=escort&&child!=null?child:source.GetComponent<SpriteRenderer>().sprite;
        sr.color=escort?new Color(.18f,.16f,.13f):new Color(.38f,.46f,.55f);sr.sortingOrder=7;
        go.transform.localScale=Vector3.one*(escort?.55f:1);
        var box=go.AddComponent<BoxCollider2D>();box.isTrigger=true;box.size=new Vector2(.65f,1.5f);
        var rb=go.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;
        var actor=go.AddComponent<CampaignActor>();actor.escort=escort;actor.lane=p.lane-1;
        actor.ConfigureWave(this,(float)Position,Mathf.Max(1,p.source.duration));
        if(!escort)go.AddComponent<EnemyHP>();
        else
        {
            story.ShowGameplayHint("护送：清理同轨士兵（S：地面瞄准）；炸弹不会伤到护送目标。");
        }
    }
    public void Arrival(string tag,int lane,double target)
    {Log($"{Position:F1} 到达 {tag} 轨{lane}"+(target>=0?$" 目标{target:F1} 偏差{Position-target:+0.0;-0.0;0}":""));}
    public void Log(string value){history.Add(value);if(history.Count>12)history.RemoveAt(0);}
    void FinalVolley()
    {
        int beat=(int)Math.Floor(Position);
        if(beat>=Script.defenseBeats||beat==lastVolleyBeat)return;
        lastVolleyBeat=beat;FinaleStarted=true;
        if(cannon!=null&&Script.chapter==8)carriedCannon=cannon.transform.position;

        SfxPlayer.PlayVariants("SFX-PLY-01",3,.7f);
        foreach(var hp in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))
        {
            if(hp.HP<=0)continue;hp.HP=Mathf.Max(0,hp.HP-100);
            if(hp.HP==0&&hp.GetComponent<WaveExitVisual>()==null)hp.gameObject.AddComponent<WaveExitVisual>().Begin(Period*1.6f);
        }
        foreach(var bomb in FindObjectsByType<BombHP>(FindObjectsSortMode.None))if(bomb.IsAlive)bomb.TakeDamage(10000);
        foreach(var shell in FindObjectsByType<CannonShell>(FindObjectsSortMode.None))if(shell.IsAlive)shell.ResolveFinaleIntercept();
        foreach(var arrow in FindObjectsByType<ArrowDefinition>(FindObjectsSortMode.None))Destroy(arrow.gameObject);
        if(cannon!=null&&cannon.GetComponent<WaveExitVisual>()==null)cannon.gameObject.AddComponent<WaveExitVisual>().Begin(Period*1.6f);
    }
    void CreateDebugUI()
    {
        debugCanvas=new GameObject("WaveDebug",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
        debugCanvas.transform.SetParent(transform,false);
        var canvas=debugCanvas.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=900;
        var scaler=debugCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var panel=new GameObject("Panel",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(debugCanvas.transform,false);
        var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.02f,.15f);rect.anchorMax=new Vector2(.58f,.84f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        panel.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.92f);
        var label=new GameObject("Text",typeof(RectTransform),typeof(UnityEngine.UI.Text));label.transform.SetParent(panel.transform,false);
        var r=label.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(18,14);r.offsetMax=new Vector2(-18,-14);
        debugText=label.GetComponent<UnityEngine.UI.Text>();debugText.font=story.font;debugText.fontSize=21;debugText.color=Color.white;debugText.alignment=TextAnchor.UpperLeft;
        debugText.raycastTarget=false;
    }
    void DrawDebug()
    {
        var next=pending.Where(p=>!p.done).Select(p=>new{p,at=p.alignTo>=0?WaveTiming.SpawnAt(p.alignTo,WallX(p.lane)-.3f-StartX(p),Speed(p),Period):p.beat}).OrderBy(x=>x.at).ToArray();
        string upcoming=string.Join("\n",next.Where(x=>x.at<=Position+8).Take(8).Select(x=>$"{Math.Max(Position,x.at):F1} {x.p.source.type} 轨{x.p.lane} {x.p.source.tag}"));
        string arrivals=string.Join("\n",FindObjectsByType<WaveThreat>(FindObjectsSortMode.None).Where(t=>t.IsPending).Take(8).Select(t=>t.Description));
        debugText.text=$"F4 波次调试  第{Script.chapter}章 {Script.variant}  {Position:F1}/{Script.defenseBeats}拍\n屏上 {ActiveCount()}/{Script.maxOnScreen}  周期 {Period:F2}s（含0.5s窗口）\n未来8拍（满员/轨道冷却会顺延）：\n{upcoming}\n预计到达（标!表示到期未结算）：\n{arrivals}\n最近记录：\n"+string.Join("\n",history.Skip(Math.Max(0,history.Count-5)));
    }
    void OnDestroy(){if(Current==this)Current=null;if(debugCanvas!=null)Destroy(debugCanvas);}
}
