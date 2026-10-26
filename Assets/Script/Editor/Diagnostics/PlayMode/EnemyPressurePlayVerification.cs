#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyPressurePlayVerification
{
    const string Pending="Barricade.EnemyPressure.Pending";
    static int frame=-1;static double changed;static bool stepping;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/EnemyPressurePlay.request"));
    static EnemyPressurePlayVerification(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Pending+".active",false)){EditorApplication.isPaused=false;PlayerSettings.runInBackground=SessionState.GetBool(Pending+".background",false);SessionState.EraseBool(Pending+".active");}};}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string setup=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/EnemyPressureSetup.request"));
        if(File.Exists(setup)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            string output=File.ReadAllText(setup).Trim();File.Delete(setup);
            var table=Resources.Load<ChapterTable>("Campaign/ChapterTable");Undo.RecordObject(table,"敌人密度与射击修复");
            table.playtestLabel="combat-v4-density-fire-20261023";
            foreach(var chapter in table.chapters.Where(c=>c.defenseBeats>0))chapter.waves=WaveDefaults.Legacy(chapter.waveA);
            EditorUtility.SetDirty(table);table.Validate();AssetDatabase.SaveAssetIfDirty(table);
            File.WriteAllText(Path.Combine(output,"配置标签.txt"),table.playtestLabel);return;
        }
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            string output=File.ReadAllText(Request).Trim();File.Delete(Request);
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty){File.WriteAllText(Path.Combine(output,"原生Play验证.json"),"{\"success\":false,\"error\":\"当前场景存在未保存修改，未切换场景或启动回归。\"}");return;}
            if(scene.name!="Title")UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            SessionState.SetBool(Pending+".active",true);SessionState.SetBool(Pending+".background",PlayerSettings.runInBackground);PlayerSettings.runInBackground=true;
            frame=-1;changed=EditorApplication.timeSinceStartup;stepping=false;SessionState.SetString(Pending,output);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending+".active",false))return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(frame!=Time.frameCount){frame=Time.frameCount;changed=EditorApplication.timeSinceStartup;}
        if(stepping||EditorApplication.timeSinceStartup-changed>1){stepping=true;EditorApplication.isPaused=true;EditorApplication.Step();}
        if(StoryDirector.Current!=null&&!string.IsNullOrEmpty(SessionState.GetString(Pending,"")))
        {string output=SessionState.GetString(Pending,"");SessionState.EraseString(Pending);StoryDirector.Current.gameObject.AddComponent<EnemyPressurePlayProbe>().Begin(output);}
    }
}

public sealed class EnemyPressurePlayProbe : MonoBehaviour
{
    [Serializable] sealed class Sample {public int chapter,peakActive,peakVisible,spawned,shots;public float seconds;}
    [Serializable] sealed class Report {public bool success,nativePlayVerified;public int checks,startFrame,endFrame;public string error;public string[] tested;public Sample[] samples;public string coverage="原生Update/物理/动画与真实子弹验证；存档及换拍用API，密度采样以10倍时间加速，不代表玩家通关胜率。";}
    Report report;StoryDirector story;string output;readonly List<string> tested=new List<string>();readonly List<Sample> samples=new List<Sample>();
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    void Require(bool ok,string text){report.checks++;tested.Add(text);if(!ok)throw new Exception(text);}
    public void Begin(string path){output=path;story=StoryDirector.Current;report=new Report{startFrame=Time.frameCount};StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            object next=null;bool more=false;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)next=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();DebugConsole.Current?.SetOpen(false);Time.timeScale=1;AudioListener.pause=false;
        report.endFrame=Time.frameCount;report.nativePlayVerified=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.tested=tested.ToArray();report.samples=samples.ToArray();
        File.WriteAllText(Path.Combine(output,"原生Play验证.json"),JsonUtility.ToJson(report,true));EditorApplication.isPlaying=false;
    }
    IEnumerator Until(Func<bool> ready,int frames=400)
    {for(int i=0;i<frames;i++){if(ready())yield break;yield return null;}throw new TimeoutException("等待超时 "+story.Phase+"/"+RunState.Chapter+" beat="+story.Clock.Completed);}
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    void ClearEnemies()
    {
        foreach(var e in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))Destroy(e.gameObject);
        foreach(var b in FindObjectsByType<ArrowDefinition>(FindObjectsSortMode.None))Destroy(b.gameObject);
    }
    EnemyMove Soldier(string name,float x)
    {
        var w=WaveDirector.Current;var previous=new HashSet<int>(FindObjectsByType<EnemyMove>(FindObjectsSortMode.None).Select(m=>m.GetInstanceID()));
        w.DebugSpawn(WaveEventType.Soldier,1,1,-1);
        var e=FindObjectsByType<EnemyMove>(FindObjectsSortMode.None).Single(m=>!previous.Contains(m.GetInstanceID()));
        e.gameObject.name=name;e.transform.position=new Vector3(x,w.LaneY(1),0);e.speed=0;
        typeof(EnemyMove).GetField("birthTime",Private).SetValue(e,Time.timeSinceLevelLoad-3);return e;
    }
    int Next(EnemyMove e)=>(int)typeof(EnemyMove).GetField("waveNextFireBeat",Private).GetValue(e);
    IEnumerator Arm(EnemyMove e)
    {
        yield return Until(()=>!e.HoldingFire);yield return Frames(2);Require(Next(e)>=0,"可见士兵已安排下一次开火");
        story.Clock.DebugComplete(Math.Max(story.Clock.Completed,Next(e)));yield return Until(()=>(bool)typeof(EnemyMove).GetField("pendingFire",Private).GetValue(e),20);
    }
    IEnumerator Test()
    {
        yield return null;
        foreach(var chapter in story.table.chapters.Where(c=>c.defenseBeats>0))foreach(var wave in new[]{chapter.waveA,chapter.waveB})
        {
            var clone=Instantiate(wave);int count=clone.events.Length;CombatPressureRules.Apply(clone);Require(clone.events.Length==count,"压力包重复应用不叠加："+chapter.number+wave.variant);
            Require(clone.events.All(e=>!(e.tag??"").StartsWith("pressure-v3-")),"旧压力包已替换："+chapter.number+wave.variant);
            var packets=clone.events.Where(e=>(e.tag??"").StartsWith(CombatPressureRules.PacketPrefix)).OrderBy(e=>e.beat).ToArray();
            Require(packets.First().beat==3&&packets.Last().beat>=wave.defenseBeats-10,"压力覆盖开场至收尾前："+chapter.number+wave.variant);
            clone.Validate();Destroy(clone);
        }
        yield return story.DebugChapter(3,true);CampaignDebug.Value.god=true;yield return Frames(2);ClearEnemies();yield return Frames(2);
        var camera=Camera.main;float left=camera.transform.position.x-camera.orthographicSize*camera.aspect;
        var spawn=FindObjectsByType<Spawn>(FindObjectsSortMode.None).First();Require(Mathf.Abs(spawn.WaveEntryX(false)-(left-1.25f))<.001f,"普通增援从镜头左侧画外固定边界入场");
        var soldier=Soldier("ShootingProbe",left+2);yield return Arm(soldier);Require(soldier.HoldingFire,"正式波次士兵举枪时停步");
        yield return Until(()=>soldier.ShotsFired==1);
        var bullet=FindObjectsByType<ArrowDefinition>(FindObjectsSortMode.None).FirstOrDefault(b=>!b.fromPlayer);
        Require(bullet!=null&&bullet.GetComponent<Rigidbody2D>().velocity.x>0,"正式波次Update发射真实敌弹");
        Require(bullet.GetComponent<Rigidbody2D>().collisionDetectionMode==CollisionDetectionMode2D.Continuous,"高速敌弹使用连续碰撞");
        int shots=soldier.ShotsFired;CampaignDebug.Value.freeze=true;story.Clock.DebugComplete(Next(soldier)+10);yield return Frames(25);Require(soldier.ShotsFired==shots,"冻结期间不射击");CampaignDebug.Value.freeze=false;
        yield return Until(()=>soldier.ShotsFired==shots+1);yield return Frames(5);Require(soldier.ShotsFired==shots+1,"跨多拍不追补连射");
        yield return Arm(soldier);var save=story.CaptureSavedGame();int before=soldier.ShotsFired;
        yield return story.RestoreSavedGame(JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(save)));yield return Frames(2);
        soldier=FindObjectsByType<EnemyMove>(FindObjectsSortMode.None).Single(e=>e.name=="ShootingProbe");yield return Until(()=>soldier.ShotsFired==before+1);
        Require(soldier.ShotsFired==before+1,"读档恢复举枪中的待发子弹，恰好发射一次");
        yield return Frames(10);Require(soldier.ShotsFired==before+1,"读档后不重复发射");
        ClearEnemies();yield return Frames(2);left=Camera.main.transform.position.x-Camera.main.orthographicSize*Camera.main.aspect;
        var hidden=Soldier("OffscreenProbe",left-3);story.Clock.DebugComplete(story.Clock.Completed+8);yield return Frames(25);
        Require(hidden.ShotsFired==0&&Next(hidden)<0,"画外士兵不偷打街垒");
        hidden.transform.position=new Vector3(left+2,hidden.transform.position.y,0);yield return Arm(hidden);int prior=hidden.ShotsFired;hidden.GetComponent<EnemyHP>().TakeDamage(100);yield return Frames(25);
        Require(hidden==null||hidden.ShotsFired==prior,"举枪时击杀会取消待发子弹");
        ClearEnemies();yield return Frames(2);
        var real=Soldier("WallHitProbe",WaveDirector.Current.WallX(1)-1);CampaignDebug.Value.god=false;GameManager.WallHP=GameManager.WallMaxHP;
        yield return Arm(real);yield return Until(()=>real.ShotsFired==1);yield return Until(()=>GameManager.WallHP<GameManager.WallMaxHP);
        Require(GameManager.WallHP==GameManager.WallMaxHP-3,"第三章真实敌弹碰撞街垒扣3血");CampaignDebug.Value.god=true;
        foreach(int chapter in new[]{3,4,7,9})
        {
            Time.timeScale=1;yield return story.DebugChapter(chapter,true);CampaignDebug.Value.god=true;yield return Frames(2);Time.timeScale=10;
            var sample=new Sample{chapter=chapter};var seen=new HashSet<int>();var counts=new Dictionary<int,int>();float at=Time.time;bool capSafe=true;
            while(story.Clock.Completed<30)
            {
                var units=FindObjectsByType<EnemyMove>(FindObjectsSortMode.None).Where(m=>m.GetComponent<EnemyHP>().HP>0).ToArray();int visible=0;
                foreach(var unit in units)
                {
                    int id=unit.GetInstanceID();seen.Add(id);counts.TryGetValue(id,out int previous);sample.shots+=Math.Max(0,unit.ShotsFired-previous);counts[id]=unit.ShotsFired;
                    Vector3 p=Camera.main.WorldToViewportPoint(unit.transform.position);if(p.x>=0&&p.x<=1)visible++;
                }
                sample.peakActive=Math.Max(sample.peakActive,units.Length);sample.peakVisible=Math.Max(sample.peakVisible,visible);
                capSafe&=units.Length<=WaveDirector.Current.Script.maxOnScreen;
                yield return null;
            }
            Require(capSafe,"屏上上限受控：第"+chapter+"章");
            sample.spawned=seen.Count;sample.seconds=Time.time-at;samples.Add(sample);
            Require(sample.peakVisible>=6,"前30拍至少6名可见敌人：第"+chapter+"章（实际"+sample.peakVisible+"）");
            Require(sample.shots>=5,"前30拍实际开枪至少5次：第"+chapter+"章（实际"+sample.shots+"）");
            if(chapter==7){Time.timeScale=0;ScreenCapture.CaptureScreenshot(Path.Combine(output,"高密度战斗Play.png"));yield return Frames(2);}
        }
        Time.timeScale=1;CampaignVerification.Check();WaveVerification.Check();
    }
}
#endif
