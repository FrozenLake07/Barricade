using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(-1200)]
public sealed partial class DebugConsole : MonoBehaviour
{
    sealed class Command {public string help;public bool mutates;public Func<string[],IEnumerator> run;}
    readonly SortedDictionary<string,Command> commands=new SortedDictionary<string,Command>(StringComparer.OrdinalIgnoreCase);
    readonly LinkedList<string[]> queue=new LinkedList<string[]>();
    readonly Queue<string> urgent=new Queue<string>();
    readonly ConsoleHistory history=new ConsoleHistory();
    readonly List<string> output=new List<string>(),plainOutput=new List<string>();
    readonly Stack<IEnumerator> running=new Stack<IEnumerator>();
    StoryDirector owner;bool busy;string[] confirmation;string activeCommand,lastReport;
    public static DebugConsole Current {get;private set;}
    static int blockedUntil=-1;
    public static bool CapturingInput=>(Current!=null&&Current.IsOpen)||Time.frameCount<=blockedUntil;
    public bool IsOpen {get;private set;}
    public bool Busy=>busy||queue.Count>0||confirmation!=null;
    public string PlainOutput=>string.Join("\n",plainOutput);
    public string LastError {get;private set;}
    public static Action<bool> EditorVerify;
    public static string RootPath=>Path.Combine(Application.persistentDataPath,"DebugConsole");
    public static string ScriptPath=>Path.Combine(RootPath,"Scripts");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Current=null;blockedUntil=-1;}
    public static void Ensure(StoryDirector director)
    {if(CampaignDebug.Available&&Current==null){var c=director.gameObject.AddComponent<DebugConsole>();c.Initialize(director);}}
    void Initialize(StoryDirector director)
    {owner=director;Current=this;RegisterCommands();BuildView();Print("街垒 · 调试控制台  /  help 查看命令  /  ~ 关闭\n调试覆盖会标注“非设计值”。打开控制台自动暂停游戏与声音，关闭恢复；step 可逐帧。");}
    void Register(string name,string help,Func<string[],string> run,bool mutates=true)
    {commands.Add(name,new Command{help=help,mutates=mutates,run=a=>Sync(()=>run(a))});}
    void Flow(string name,string help,Func<string[],IEnumerator> run)
    {commands.Add(name,new Command{help=help,mutates=true,run=run});}
    IEnumerator Sync(Func<string> action){string value=action();if(!string.IsNullOrEmpty(value))Print(value);yield break;}
    static void Args(string[] a,int min,int max=-1){if(max<0)max=min;if(a.Length<min||a.Length>max)throw new ArgumentException("参数数量不符。");}
    static int Int(string s,int min=int.MinValue,int max=int.MaxValue)
    {if(!int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)||n<min||n>max)throw new ArgumentException($"整数范围 {min}—{max}。");return n;}
    static float Number(string s,float min,float max)
    {if(!float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)||float.IsNaN(n)||float.IsInfinity(n)||n<min||n>max)throw new ArgumentException($"数值范围 {min}—{max}。");return n;}
    static bool On(string s){if(s=="on")return true;if(s=="off")return false;throw new ArgumentException("请输入 on 或 off。");}
    SBullet Gun()=>FindFirstObjectByType<SBullet>()??throw new InvalidOperationException("当前场景没有枪，请进入防守段。");
    WaveDirector Wave()=>WaveDirector.Current??throw new InvalidOperationException("当前没有防守波次。");
    void RegisterCommands()
    {
        Register("help","help [命令] — 查看语法和示例。如 help ch。",a=>{Args(a,0,1);if(a.Length==0)return string.Join("  ",commands.Keys)+"\n示例：ch 9 hard; hp max 600; hp set 600\n危险命令：confirm 确认 / cancel 取消。";if(!commands.TryGetValue(a[0],out var c))throw new ArgumentException("未知命令。");return c.help;},false);
        Flow("ch","ch <1—10> [hard] — 正常开章；hard 直接防守。例：ch 9 hard（第二章没有战斗）。",a=>{Args(a,1,2);if(a.Length==2&&a[1]!="hard")throw new ArgumentException("第二参数仅支持 hard。");return owner.DebugChapter(Int(a[0],1,10),a.Length==2);});
        Flow("retry","retry — 按章节起点重开。例：retry。",a=>{Args(a,0);return owner.DebugChapter(RunState.Chapter,false,true);});
        Flow("next","next — 将本章记为调试完成并正常开下一章。例：next。",a=>{Args(a,0);return owner.DebugNext();});
        Register("fail","fail — 触发失败分支，第9章进入尾声。例：fail。",a=>{Args(a,0);owner.DebugFail();return "已触发失败流程。";});
        Flow("phase","phase <title/prep/defense/choice/epilogue/results> — 切到本章已配置阶段；重建对应场景。例：ch 7 hard; phase results。当前战役已取消choice阶段。",a=>{Args(a,1);return owner.DebugPhase(a[0].ToLowerInvariant());});
        Register("skip","skip — 跳过当前黑屏、整段图片过场、第一章或Timeline；命名段按匿名继续。例：skip。",a=>{Args(a,0);owner.DebugSkip();return "已发送跳过信号。";});
        Register("beat","beat — 拍号、阶段、Wait、Window、Period及Perfect剩余。例：beat。",a=>{Args(a,0);return BeatInfo();},false);
        Register("tempo","tempo [now] <秒> — 持续覆盖拍前等待；now立即生效。例：tempo now 0.2。窗口另用 window。",a=>{Args(a,1,2);bool now=a.Length==2;if(now&&a[0]!="now")throw new ArgumentException("语法 tempo [now] <秒>。");float n=Number(a[now?1:0],.05f,20);CampaignDebug.Value.tempo=n;owner.Clock.NextWait=n;if(now)owner.Clock.DebugTiming(n,owner.Clock.Window);return BeatInfo();});
        Register("window","window <秒> — 当前及后续节拍窗口。例：window 0.5。",a=>{Args(a,1);float n=Number(a[0],.01f,10);CampaignDebug.Value.window=n;owner.Clock.DebugTiming(owner.Clock.Wait,n);return BeatInfo();});
        Register("time","time <0—10> — 关闭控制台后的时间倍率；控制台内保持暂停。例：time 0。",a=>{Args(a,1);if(CampaignSession.Suspended)throw new InvalidOperationException("请先恢复暂停菜单。");if(stepping)throw new InvalidOperationException("正在逐帧，请等结束。");float scale=Number(a[0],0,10);SetGameTimeScale(scale);return IsOpen?"关闭后timeScale="+scale+"（当前仍暂停）":"timeScale="+scale;});
        Flow("step","step [1—240] — time 0 时运行指定模拟帧，然后恢复0。例：step 3。",a=>{Args(a,0,1);return Step(a.Length==0?1:Int(a[0],1,240));});
        Register("freeze","freeze — 冻结/解冻敌人，节拍继续。例：freeze。",a=>{Args(a,0);CampaignDebug.Value.freeze=!CampaignDebug.Value.freeze;return "敌人冻结="+CampaignDebug.Value.freeze;});
        Register("hp","hp [set|max|add 数值] — 查看/设值/上限/加减血；不会自动重开。例：hp max 600; hp set 600。",a=>{Args(a,0,2);if(a.Length>0){Args(a,2);int n=Int(a[1],a[0]=="add"?-100000:0,100000);if(a[0]=="max"){if(n<1)throw new ArgumentException("上限至少1。");GameManager.WallMaxHP=n;GameManager.WallHP=Math.Min(GameManager.WallHP,n);}else if(a[0]=="set")GameManager.WallHP=Math.Min(n,GameManager.WallMaxHP);else if(a[0]=="add")GameManager.WallHP=Math.Max(0,Math.Min(GameManager.WallMaxHP,GameManager.WallHP+n));else throw new ArgumentException("仅支持 set/max/add。");}return $"WallHP={GameManager.WallHP} / WallMaxHP={GameManager.WallMaxHP} / BuiltHP={RunState.BuiltHP}";},false);
        Register("god","god — 开关无敌。例：god。",a=>{Args(a,0);CampaignDebug.Value.god=!CampaignDebug.Value.god;return "无敌="+CampaignDebug.Value.god;});
        Register("lastchance","lastchance — 防守中触发最后一拍。例：lastchance。",a=>{Args(a,0);owner.DebugLastChance();return "最后一拍已触发。";});
        Register("mult","mult <0.1—10> — 换弹倍率，仅运行时。例：mult 0.8。",a=>{Args(a,1);RunState.ReloadMultiplier=Number(a[0],.1f,10);return "换弹系数="+RunState.ReloadMultiplier;});
        Register("ammo","ammo [set 数量] — 查看或设弹药，设置会取消当前换弹。例：ammo set 2。",a=>{Args(a,0,2);var gun=Gun();if(a.Length>0){Args(a,2);if(a[0]!="set")throw new ArgumentException("语法 ammo set <数量>。");gun.DebugAmmo(Int(a[1],0,gun.CampaignCapacity));}return $"弹药 {gun.CampaignAmmo}/{gun.CampaignCapacity} 无限={gun.DebugUnlimited} 换弹={gun.IsReloading}";},false);
        Register("infinite","infinite — 开关无限弹，覆盖第9章默认。例：infinite。",a=>{Args(a,0);CampaignDebug.Value.infinite=CampaignDebug.Infinite(RunState.Chapter==9)?0:1;return "无限弹="+CampaignDebug.Infinite(RunState.Chapter==9);});
        Register("reload","reload — 等价R，弹满/无限弹/齐射时不换弹。例：ammo set 0; reload。",a=>{Args(a,0);Gun().BeginCampaignReload();return "换弹状态="+Gun().IsReloading;});
        Register("aim","aim <up/down> — 空中/地面瞄准，保留调整延迟。例：aim up。",a=>{Args(a,1);if(a[0]!="up"&&a[0]!="down")throw new ArgumentException("请输入 up/down。");var tap=FindFirstObjectByType<TapSec>();if(tap==null)throw new InvalidOperationException("请进入防守。");tap.CampaignSetAim(a[0]=="up"?"Up":"Down");return "瞄准="+TapSec.ShootStatu;});
        Register("lane","lane <1—3> — 切到从下至上轨道，保留调整延迟。例：lane 2。",a=>{Args(a,1);var track=FindFirstObjectByType<CapsuleTrack>();if(track==null)throw new InvalidOperationException("当前没有攻击轨道。");track.DebugLane(Int(a[0],1,3));return "轨道="+(CapsuleTrack.AttackIndex+1);});
        Register("kill","kill — 清屏敌人、掷弹兵和弹丸；保留后续波次及护送对象。例：kill。",a=>{Args(a,0);Kill();return "已清理敌人与弹丸。";});
        Register("crit","crit — 下一次有目标且实际射出的弹强制Perfect。例：crit。",a=>{Args(a,0);CampaignDebug.Value.crit=true;return "下一发Perfect已就绪。";});
        Register("wave","wave — 章/变体/拍号/待生成事件。例：wave。",a=>{Args(a,0);return Wave().DebugInfo();},false);
        Register("spawn","spawn <soldier/squad/shell/decoy/escort> <1—3> [count | at 到达拍] — 从画外生成；at：士兵到墙/炮弹命中/小队首次齐投命中。例：spawn squad 2 at 40。",SpawnCommand);
        Register("density","density <0—4> — 缩放尚未执行的普通士兵事件数量，不改护送/诱饵/炮弹。例：density 1.5。",a=>{Args(a,1);CampaignDebug.Value.density=Number(a[0],0,4);return "后续士兵密度="+CampaignDebug.Value.density;});
        Register("cap","cap <1—64> — 临时屏上实体上限，齐次事件仍遵守上限。例：cap 16。",a=>{Args(a,1);CampaignDebug.Value.cap=Int(a[0],1,64);return "屏上上限="+CampaignDebug.Value.cap;});
        Register("finale","finale — 立即开启结尾齐射。例：finale。",a=>{Args(a,0);Wave().DebugFinale();return "齐射已触发。";});
        Register("clear","clear — 需confirm：清世界并重建本章波次；已过期事件可能重新生成。例：clear，然后confirm。",a=>{Args(a,0);owner.DebugClear();return "世界已清理，波次已重建。";});
        Register("state","state [字段] — 查看RunState公共字段；完整内容也写入报告。例：state Decision。",StateCommand,false);
        Register("set","set <字段> <值> — 仅允许公开标量白名单；Chapter须用ch。例：set Decision 1 / set PlayerName \"让 巴蒂斯特\"。",SetCommand);
        Register("save","save <slot> — 保存独立调试槽，含调试标记；不覆盖主存档。例：save siege9。",SaveCommand,false);
        Flow("load","load <slot> — 需confirm：读独立调试槽（含覆盖值）。例：load siege9，然后confirm。",LoadCommand);
        Flow("reset","reset — 需confirm：清运行覆盖，回标题并按正常校音流程开新局。例：reset，然后confirm。",ResetCommand);
        Register("confirm","confirm — 执行最近一个待确认危险命令，再继续后续批次。例：confirm。",a=>{Args(a,0);if(confirmation==null)throw new InvalidOperationException("没有待确认命令。");confirmed=confirmation;queue.AddFirst(confirmation);confirmation=null;return "已确认。";},false);
        Register("cancel","cancel — 取消待确认及尚未开始的批量命令，不中断正在加载的场景。例：cancel。",a=>{Args(a,0);confirmation=null;confirmed=null;queue.Clear();return "待执行队列已取消。";},false);
        Register("verify","verify [wave] — 编辑器调用原有CampaignVerification/WaveVerification；构建使用共享Validate规则。例：verify wave。",VerifyCommand,false);
        Register("report","report — 打开最近报告所在目录。例：report。",a=>{Args(a,0);string path=lastReport??CampaignTelemetry.Current?.DirectoryPath??RootPath;Directory.CreateDirectory(Path.HasExtension(path)?Path.GetDirectoryName(path):path);Application.OpenURL(new Uri(Path.HasExtension(path)?Path.GetDirectoryName(path):path).AbsoluteUri);return path;},false);
        Register("assert","assert <字段> <== != > >= < <= > <值> — 比较公开RunState或hp/maxhp/ammo/phase/time；文本加引号。例：assert hp == 600。",AssertCommand,false);
        Register("hud","hud <on/off> — 截图隐藏/显示心脏弹药与游戏提示。例：hud off。",a=>{Args(a,1);owner.DebugHud(On(a[0]));return "HUD="+a[0];});
        Register("debug","debug wave <on/off> | debug beat [on/off] — 复用F4；中央节拍信息。例：debug beat。",a=>{Args(a,1,2);if(a[0]=="wave"){Args(a,2);Wave().DebugOverlay(On(a[1]));}else if(a[0]=="beat")beatOverlay=a.Length==1?!beatOverlay:On(a[1]);else throw new ArgumentException("可用 wave/beat。");return "调试显示已更新。";},false);
        Register("fps","fps — 当前平滑帧率和托管堆占用。例：fps。",a=>{Args(a,0);return $"FPS={1/Mathf.Max(.0001f,frameSeconds):F1}  GC={GC.GetTotalMemory(false)/1048576d:F2} MiB";},false);
        Register("music","music <title/people/none/off> — 单次播放指定曲；title未配置时明确提示。例：music people。",a=>{Args(a,1);owner.DebugMusic(a[0]);return "音乐="+a[0];});
        Register("sfx","sfx <id> — 试听已有音效（含第9章调试试听），不循环。例：sfx SFX-QTE-01。",a=>{Args(a,1);var clip=SfxPlayer.ClipFor(a[0]);if(clip==null)throw new ArgumentException("音效不存在："+a[0]);if(previewAudio==null){previewAudio=gameObject.AddComponent<AudioSource>();previewAudio.playOnAwake=false;previewAudio.loop=false;}previewAudio.PlayOneShot(clip,.65f);return "试听="+a[0];});
        Register("heartbeat","heartbeat <on/off> — 仅切心跳声音，不改节拍或心脏动画。例：heartbeat off。",a=>{Args(a,1);CampaignDebug.Value.muteHeartbeat=!On(a[0]);return "心跳声音="+a[0];});
        Register("exec","exec <脚本文件> — 读取UTF8；每行/分号命令，#注释；仅Scripts目录内，最多256条，禁止嵌套。例：exec \"siege9.txt\"。",ExecCommand,false);
    }
    public string Execute(string line)
    {
        if(!CampaignDebug.Available)return "控制台未启用。";
        try
        {
            var parts=ConsoleSyntax.Parse(line);if(queue.Count+parts.Count>256)throw new ArgumentException("队列上限256条。");
            foreach(var p in parts)
            {
                string n=p[0].ToLowerInvariant();
                if(parts.Count==1&&(busy||confirmation!=null)&&(n=="skip"||n=="help"||n=="time"||n=="confirm"||n=="cancel"||n=="beat"))urgent.Enqueue(string.Join(" ",p.Select(Quote)));
                else queue.AddLast(p);
            }
            return "queued";
        }
        catch(Exception e){Error(e.Message);return e.Message;}
    }
    static string Quote(string s)=>"\""+s.Replace("\"","\\\"")+"\"";
    string[] confirmed;
    void LateUpdate()
    {
        if(owner==null)return;
        if(urgent.Count>0){var p=ConsoleSyntax.Parse(urgent.Dequeue())[0];StartCoroutine(Immediate(p));}
        if(!busy&&confirmation==null&&queue.Count>0)
        {
            var tokens=queue.First.Value;queue.RemoveFirst();string name=tokens[0].ToLowerInvariant();
            if((name=="reset"||name=="load"||name=="clear")&&!ReferenceEquals(tokens,confirmed))
            {confirmation=tokens;Print("待确认："+string.Join(" ",tokens)+"\n输入 confirm 执行，cancel 取消；后续批次等待。");}
            else{confirmed=null;busy=true;StartCoroutine(Run(tokens));}
        }
        RefreshView();
    }
    IEnumerator Immediate(string[] tokens)
    {
        var cmd=GetCommand(tokens);if(cmd==null)yield break;
        IEnumerator it=null;try{it=cmd.run(tokens.Skip(1).ToArray());if(cmd.mutates)CampaignDebug.Mark(string.Join(" ",tokens));}catch(Exception e){Error(e.Message+"；help "+tokens[0]);}
        if(it!=null){while(true){bool more=false;try{more=it.MoveNext();}catch(Exception e){Error(e.Message+"；help "+tokens[0]);}if(!more)break;yield return it.Current;}(it as IDisposable)?.Dispose();}
    }
    Command GetCommand(string[] t)
    {if(commands.TryGetValue(t[0],out var cmd))return cmd;Error("未知命令："+t[0]+"；help 查看命令。");queue.Clear();return null;}
    IEnumerator Run(string[] tokens)
    {
        activeCommand=tokens[0];var cmd=GetCommand(tokens);
        if(cmd==null){busy=false;yield break;}
        try{running.Push(cmd.run(tokens.Skip(1).ToArray()));if(cmd.mutates||(tokens.Length>1&&(tokens[0]=="hp"||tokens[0]=="ammo")))CampaignDebug.Mark(string.Join(" ",tokens));}
        catch(Exception e){Error(e.Message+"；help "+tokens[0]);queue.Clear();busy=false;yield break;}
        while(running.Count>0)
        {
            var top=running.Peek();bool more=false;Exception failure=null;
            try{more=top.MoveNext();}catch(Exception e){failure=e;}
            if(failure!=null){Error(failure.Message+"；help "+tokens[0]);DisposeRunning();queue.Clear();break;}
            if(!more){running.Pop();(top as IDisposable)?.Dispose();continue;}
            if(top.Current is IEnumerator child){running.Push(child);continue;}
            yield return top.Current;
        }
        busy=false;activeCommand=null;
    }
    void DisposeRunning(){while(running.Count>0){try{(running.Pop() as IDisposable)?.Dispose();}catch(Exception e){Debug.LogWarning(e.Message);}}}
    public void Print(string message,bool error=false)
    {
        foreach(var line in (message??"").Replace("\r","").Split('\n'))
        {
            plainOutput.Add(line);output.Add((error?"<color=#ED958B>":"<color=#E2D9C4>")+Escape(line)+"</color>");
            if(output.Count>20){output.RemoveAt(0);plainOutput.RemoveAt(0);}
        }
        viewDirty=true;
    }
    static string Escape(string s)=>s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
    void Error(string text){LastError=text;Print(text,true);}
    string BeatInfo()=>$"{owner.Phase}  开拍={owner.Clock.Opened} 完成={owner.Clock.Completed}  Wait={owner.Clock.Wait:F3} Window={owner.Clock.Window:F3} Period={owner.Clock.Period:F3} Perfect剩余={owner.Clock.PerfectRemaining:F3}s";
    string SpawnCommand(string[] a)
    {
        Args(a,2,4);if(a.Length==4&&a[2]!="at")throw new ArgumentException("请用 at <到达拍>。");
        WaveEventType type;switch(a[0]){case "soldier":type=WaveEventType.Soldier;break;case "squad":type=WaveEventType.Squad;break;case "shell":type=WaveEventType.Shell;break;case "decoy":type=WaveEventType.Decoy;break;case "escort":type=WaveEventType.Escort;break;default:throw new ArgumentException("类型 soldier/squad/shell/decoy/escort。");}
        Wave().DebugSpawn(type,Int(a[1],1,3),a.Length==3?Int(a[2],1,16):1,a.Length==4?Int(a[3],0,10000):-1);return "已生成调试对象。";
    }
    static readonly HashSet<string> Writable=new HashSet<string>(new[]{"PlayerName","ProtagonistIntroduced","BuiltHP","Score","Perfects","Attempts","TotalHeartbeats","AmmoBonus","ReloadMultiplier","SpySpared","Decision"},StringComparer.OrdinalIgnoreCase);
    static FieldInfo Field(string name)=>typeof(RunState).GetFields(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(f=>f.Name.Equals(name,StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("未知RunState字段："+name);
    static string Format(object value)=>value is IEnumerable sequence&&!(value is string)?string.Join(",",sequence.Cast<object>()):Convert.ToString(value,CultureInfo.InvariantCulture);
    string StateCommand(string[] a)
    {
        Args(a,0,1);if(a.Length==1){var f=Field(a[0]);return f.Name+"="+Format(f.GetValue(null));}
        string text=string.Join("\n",typeof(RunState).GetFields(BindingFlags.Public|BindingFlags.Static).Select(f=>f.Name+"="+Format(f.GetValue(null))));
        Directory.CreateDirectory(RootPath);lastReport=Path.Combine(RootPath,"RunState.txt");File.WriteAllText(lastReport,text);Print(lastReport);return text;
    }
    string SetCommand(string[] a)
    {
        Args(a,2);var f=Field(a[0]);if(!Writable.Contains(f.Name))throw new ArgumentException("字段不在可写白名单；Chapter请用ch，集合/检查点只读。");
        object value;
        if(f.FieldType==typeof(string)){if(a[1].Length>128)throw new ArgumentException("文本最多128字符。");value=a[1];}
        else if(f.FieldType==typeof(bool)){if(!bool.TryParse(a[1],out bool b))throw new ArgumentException("布尔值使用 true/false。");value=b;}
        else if(f.FieldType==typeof(JavertDecision)){if(!Enum.TryParse<JavertDecision>(a[1],true,out var d)||!Enum.IsDefined(typeof(JavertDecision),d))throw new ArgumentException("Decision使用0/1/2或Unchosen/Released/Executed。");value=d;}
        else if(f.FieldType==typeof(float))value=Number(a[1],.1f,10);
        else value=Int(a[1],0,f.Name=="AmmoBonus"?1:1000000);
        f.SetValue(null,value);return f.Name+"="+Format(value);
    }
    static string Slot(string name)
    {if(name.Length<1||name.Length>32||name.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))throw new ArgumentException("槽名1—32位，只能用字母数字下划线短横线。");return Path.Combine(RootPath,"Saves",name+".sav");}
    string SaveCommand(string[] a){Args(a,1);string path=Slot(a[0]);Directory.CreateDirectory(Path.GetDirectoryName(path));SingleSaveSlot.Write(path,JsonUtility.ToJson(owner.CaptureSavedGame()));return "已保存调试槽："+path;}
    IEnumerator LoadCommand(string[] a)
    {
        Args(a,1);if(CampaignSession.Suspended)throw new InvalidOperationException("请先恢复暂停菜单。");
        if(!CampaignSaveData.TryRead(Slot(a[0]),out var data,out string error))throw new InvalidOperationException(error);
        yield return owner.RestoreSavedGame(data);CampaignDebug.Mark("load "+a[0]);Print("调试槽已恢复。");
    }
    IEnumerator ResetCommand(string[] a){Args(a,0);CampaignDebug.Reset();SetGameTimeScale(1);yield return owner.DebugPhase("title");owner.StartNewCampaign();Print("新局已开始；旧调试覆盖已清除。");}
    string VerifyCommand(string[] a)
    {
        Args(a,0,1);bool wave=a.Length>0;if(wave&&a[0]!="wave")throw new ArgumentException("语法 verify [wave]。");
        Directory.CreateDirectory(RootPath);
        if(EditorVerify!=null){EditorVerify(wave);lastReport=Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/"+(wave?"WaveVerification.json":"CampaignVerification.json")));}
        else{owner.table.Validate();foreach(var ch in owner.table.chapters.Where(c=>c.defenseBeats>0)){ch.waveA.Validate();ch.waveB.Validate();}lastReport=Path.Combine(RootPath,wave?"WaveValidation.json":"CampaignVerification.json");File.WriteAllText(lastReport,"{\"success\":true,\"nativePlayVerified\":false,\"scope\":\"shared_runtime_validation\"}");}
        // Keep the document's WaveValidation name without replacing the original menu output.
        if(wave){string alias=Path.Combine(RootPath,"WaveValidation.json");if(!string.Equals(Path.GetFullPath(lastReport),Path.GetFullPath(alias),StringComparison.OrdinalIgnoreCase))File.Copy(lastReport,alias,true);lastReport=alias;}
        return "success（配置检查，不等同通关）\n"+lastReport;
    }
    object ReadValue(string name)
    {
        switch(name.ToLowerInvariant()){case "hp":return GameManager.WallHP;case "maxhp":return GameManager.WallMaxHP;case "ammo":return Gun().CampaignAmmo;case "phase":return owner.Phase.ToString();case "time":return Time.timeScale;default:return Field(name).GetValue(null);}
    }
    string AssertCommand(string[] a)
    {
        Args(a,3);object left=ReadValue(a[0]);bool success;
        if(double.TryParse(Format(left),NumberStyles.Float,CultureInfo.InvariantCulture,out double x)&&double.TryParse(a[2],NumberStyles.Float,CultureInfo.InvariantCulture,out double y)&&!double.IsNaN(y)&&!double.IsInfinity(y))
        {switch(a[1]){case "==":success=x==y;break;case "!=":success=x!=y;break;case ">":success=x>y;break;case ">=":success=x>=y;break;case "<":success=x<y;break;case "<=":success=x<=y;break;default:throw new ArgumentException("比较符 == != > >= < <=。");}}
        else{if(a[1]!="=="&&a[1]!="!=")throw new ArgumentException("文本仅支持 == 和 !=。");success=string.Equals(Format(left),a[2],StringComparison.OrdinalIgnoreCase);if(a[1]=="!=")success=!success;}
        if(!success)throw new InvalidOperationException("ASSERT FAIL："+string.Join(" ",a)+"；实际="+Format(left));return "ASSERT PASS："+string.Join(" ",a);
    }
    string ExecCommand(string[] a)
    {
        Args(a,1);Directory.CreateDirectory(ScriptPath);string root=Path.GetFullPath(ScriptPath)+Path.DirectorySeparatorChar,path=Path.GetFullPath(Path.Combine(root,a[0]));
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new ArgumentException("脚本须存在于 "+ScriptPath);
        if(new FileInfo(path).Length>65536)throw new ArgumentException("脚本不能超过64KiB。");
        var all=new List<string[]>();foreach(string line in File.ReadAllLines(path)){if(string.IsNullOrWhiteSpace(line)||line.TrimStart().StartsWith("#"))continue;all.AddRange(ConsoleSyntax.Parse(line));}
        if(all.Any(t=>t[0].Equals("exec",StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("脚本不可嵌套exec。");
        if(all.Count+queue.Count>256)throw new ArgumentException("脚本与队列合计不能超过256条。");
        for(int i=all.Count-1;i>=0;i--)queue.AddFirst(all[i]);return $"脚本已排队：{all.Count}条；危险命令仍需确认。";
    }
    bool stepping;AudioSource previewAudio;
    IEnumerator Step(int frames)
    {
        if(CampaignSession.Suspended||Time.timeScale!=0)throw new InvalidOperationException("请先恢复暂停菜单，再用time 0；不用编辑器Pause按钮。");
        bool consoleStep=IsOpen;int completed=0;
        stepping=true;try{Time.timeScale=1;for(int i=0;i<frames;i++){yield return null;completed++;if(consoleStep&&!IsOpen)break;}}
        finally{if(!consoleStep||IsOpen)Time.timeScale=0;stepping=false;}
        Print($"已运行{completed}帧。"+(IsOpen||!consoleStep?"恢复time 0。":"已恢复进入控制台前的时间倍率。"));
    }
    void Kill()
    {
        foreach(var x in FindObjectsByType<WaveSquad>(FindObjectsSortMode.None))Destroy(x.gameObject);
        foreach(var x in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))Destroy(x.gameObject);
        foreach(var x in FindObjectsByType<ArrowDefinition>(FindObjectsSortMode.None))Destroy(x.gameObject);
        foreach(var x in FindObjectsByType<BombHP>(FindObjectsSortMode.None))Destroy(x.gameObject);
        foreach(var x in FindObjectsByType<CannonShell>(FindObjectsSortMode.None))Destroy(x.gameObject);
        foreach(var x in FindObjectsByType<Cannon>(FindObjectsSortMode.None))Destroy(x.gameObject);
    }
    void OnDestroy(){DisposeRunning();if(stepping)Time.timeScale=0;ResumeSimulation();if(Current==this)Current=null;RestoreFocus();}
}

