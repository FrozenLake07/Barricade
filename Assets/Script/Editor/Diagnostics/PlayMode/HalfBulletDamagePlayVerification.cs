#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HalfBulletDamagePlayVerification
{
    const string Pending="1832.HalfBulletDamage.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/HalfBulletDamagePlay.request"));
    static HalfBulletDamagePlayVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var output=File.ReadAllText(Request).Trim();File.Delete(Request);SessionState.SetString(Pending,output);SessionState.SetBool(Pending+".active",true);
            previousFrame=-1;lastAdvance=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Pending+".active",false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(Time.frameCount!=previousFrame){previousFrame=Time.frameCount;lastAdvance=EditorApplication.timeSinceStartup;}
        if(EditorApplication.timeSinceStartup-lastAdvance>1){EditorApplication.isPaused=true;EditorApplication.Step();}
        string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;
        SessionState.EraseString(Pending);Application.runInBackground=true;
        var story=StoryDirector.Current;
        if(story==null)story=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Campaign/CampaignRoot")).GetComponent<StoryDirector>();
        story.gameObject.AddComponent<HalfBulletDamagePlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class HalfBulletDamagePlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Actual enemy-bullet collision, fractional damage, chapter scaling, save/load and chapter restart. No full playthrough; user scene not saved.";}
    readonly List<string> passed=new List<string>();Report report;string output;StoryDirector story;float listenerVolume;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    CampaignCinematicView Film=>story.GetComponent<CampaignCinematicView>();
    T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    void Check(bool ok,string text){if(!ok)throw new Exception(text);passed.Add(text);}
    public void Begin(string path){output=path;story=GetComponent<StoryDirector>();report=new Report{startFrame=Time.frameCount};listenerVolume=AudioListener.volume;AudioListener.volume=0;StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            object current=null;bool more=false;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)current=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();AudioListener.volume=listenerVolume;AudioListener.pause=false;Time.timeScale=1;
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"子弹伤害Play检查.json"),JsonUtility.ToJson(report,true));HalfBulletDamagePlayVerification.Finished();
    }
    IEnumerator Until(Func<bool> ready,bool skipCards=false)
    {
        for(int i=0;i<600;i++){if(ready())yield break;if(skipCards)story.DebugSkip();yield return null;}throw new TimeoutException("Play check waiting for "+story.Phase);
    }
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    IEnumerator Progress(CampaignActor actor,float progress)
    {
        Set(actor,"elapsed",actor.duration*progress);yield return Frames(2);
    }
    void Bullet(Collider2D wall)
    {
        var bullet=new GameObject("HalfDamageEnemyBullet");var def=bullet.AddComponent<ArrowDefinition>();def.fromPlayer=false;
        var hit=bullet.AddComponent<BulletHit>();typeof(BulletHit).GetMethod("OnTriggerEnter2D",Private).Invoke(hit,new object[]{wall});
    }
    IEnumerator EnterEight()
    {
        bool done=false;StartCoroutine(Enter(()=>done=true));yield return Until(()=>done);CampaignDebug.Value.god=false;
    }
    IEnumerator Enter(Action done){yield return story.DebugChapter(8,true);done();}
    IEnumerator Test()
    {
        yield return Frames(3);yield return EnterEight();
        var original=story.table;var clone=Instantiate(original);story.table=clone;
        var wall=UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).First(c=>GameManager.IsWall(c));
        GameManager.WallMaxHP=600;GameManager.WallHP=100;GameManager.ResetBulletDamage();
        Bullet(wall);Check(GameManager.WallHP==100&&Math.Abs(GameManager.PendingBulletDamage-.5f)<.00001f,"First enemy bullet retains half a point instead of rounding up");
        Bullet(wall);Check(GameManager.WallHP==99&&GameManager.PendingBulletDamage<.00001f,"Two actual enemy bullets deal one point at multiplier one");
        foreach(float multiplier in new[]{1f,2f,2.2f,2.5f,3f})
        {
            clone.chapters[7].combatDamageMultiplier=multiplier;GameManager.WallHP=100;GameManager.ResetBulletDamage();
            for(int i=0;i<10;i++)Bullet(wall);
            Check(GameManager.WallHP==100-Mathf.FloorToInt(10*.5f*multiplier+.000001f),"Ten bullets respect scaled half-point damage: multiplier "+multiplier);
        }
        clone.chapters[7].combatDamageMultiplier=1;
        GameManager.WallHP=100;GameManager.ResetBulletDamage();CampaignDebug.Value.god=true;Bullet(wall);
        Check(GameManager.WallHP==100&&GameManager.PendingBulletDamage==0,"Invulnerability does not accumulate hidden bullet damage");CampaignDebug.Value.god=false;
        Bullet(wall);GameManager.DamageWall(GameManager.Kind.Enemy);Check(GameManager.WallHP==90&&Math.Abs(GameManager.PendingBulletDamage-.5f)<.00001f,"Enemy body retains ten damage and preserves bullet remainder");
        GameManager.DamageWall(GameManager.Kind.Bomb);Check(GameManager.WallHP==70,"Grenade remains twenty damage");
        GameManager.DamageWallAmount(7);Check(GameManager.WallHP==63,"Explicit integer damage remains unchanged");
        Bullet(wall);Check(GameManager.WallHP==62&&GameManager.PendingBulletDamage<.00001f,"Body and grenade damage do not discard half-point carry");
        Bullet(wall);GameManager.AddWallHP(1);Bullet(wall);Check(GameManager.WallHP==62,"Partial healing preserves carried fractional damage");
        Bullet(wall);GameManager.AddWallHP(600);Check(GameManager.WallHP==600&&GameManager.PendingBulletDamage==0,"Full repair clears fractional debt");
        GameManager.WallHP=100;GameManager.ResetBulletDamage();Bullet(wall);GameManager.WallHP=200;Bullet(wall);
        Check(GameManager.WallHP==200&&Math.Abs(GameManager.PendingBulletDamage-.5f)<.00001f,"Direct health reset cannot inherit previous fractional debt");
        yield return Frames(2);GameManager.WallHP=100;GameManager.ResetBulletDamage();Bullet(wall);yield return Frames(1);
        var saved=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(story.CaptureSavedGame()));
        Check(saved.wall.Any(f=>f.name=="bulletDamageRemainder"),"Actual campaign save includes fractional bullet damage");
        GameManager.WallHP=200;GameManager.ResetBulletDamage();yield return story.RestoreSavedGame(saved);CampaignDebug.Value.god=false;
        Check(GameManager.WallHP==100&&Math.Abs(GameManager.PendingBulletDamage-.5f)<.00001f,"Full save-load restores the half-point remainder");
        wall=UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).First(c=>GameManager.IsWall(c));Bullet(wall);
        Check(GameManager.WallHP==99&&GameManager.PendingBulletDamage<.00001f,"Next bullet after loading completes one point of damage");
        saved.wall.RemoveAll(f=>f.name=="bulletDamageRemainder"||f.name=="bulletDamageHealth");yield return story.RestoreSavedGame(saved);CampaignDebug.Value.god=false;
        Check(GameManager.PendingBulletDamage==0,"Older saves without fractional fields start without stale debt");
        wall=UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).First(c=>GameManager.IsWall(c));Bullet(wall);Check(GameManager.PendingBulletDamage>.49f,"Pre-restart half-point is present");
        yield return EnterEight();Check(GameManager.PendingBulletDamage==0,"Debug chapter entry resets fractional debt");
        story.table=original;Destroy(clone);
    }
}
#endif
