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
public static class GavrocheArrivalPlayVerification
{
    const string Pending="1832.GavrocheArrival.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/GavrocheArrivalPlay.request"));
    static GavrocheArrivalPlayVerification(){EditorApplication.update+=Poll;}
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
        story.gameObject.AddComponent<GavrocheArrivalPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class GavrocheArrivalPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Actual Gavroche animation, journey-state samples and collision API integration; no hardware input injection. User scene not saved or replaced.";}
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
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"伽弗洛什Play检查.json"),JsonUtility.ToJson(report,true));GavrocheArrivalPlayVerification.Finished();
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
    IEnumerator Test()
    {
        yield return Frames(3);StartCoroutine(story.DebugChapter(4,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&Camera.main!=null);
        var wall=UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).First(c=>GameManager.IsWall(c));
        var go=Instantiate(Resources.Load<GameObject>("CampaignActors/Gavroche"),new Vector3(Camera.main.transform.position.x-2,0,0),Quaternion.identity);
        var actor=go.GetComponent<CampaignActor>();actor.enabled=false;actor.ConfigureWave(null,0,8);var sprite=go.GetComponent<SpriteRenderer>();var collision=typeof(CampaignActor).GetMethod("OnTriggerEnter2D",Private);
        yield return Progress(actor,0);Check(Math.Abs(sprite.color.a-.35f)<.001,"Gavroche starts translucent at thirty-five-percent opacity");
        yield return Progress(actor,.21f);float outbound=sprite.color.a;Check(outbound<1&&outbound>.35f,"Outbound animation progressively restores opacity");
        collision.Invoke(actor,new object[]{wall});Check(!actor.IsResolved,"Leaving the initial wall overlap cannot count as a successful return");
        yield return Progress(actor,.42f);Check(Math.Abs(sprite.color.a-1)<.001,"Outbound finishes fully opaque");
        yield return Progress(actor,.5f);Check(go.GetComponent<GavrocheVisual>().IsPicking&&Math.Abs(sprite.color.a-1)<.001,"Ammunition pickup animation remains fully opaque");
        yield return Progress(actor,.58f);Check(Math.Abs(sprite.color.a-1)<.001,"Return begins fully opaque");
        yield return Progress(actor,.79f);float returning=sprite.color.a;Check(returning>.35f&&returning<1,"Return animation progressively reduces opacity");
        yield return Progress(actor,.99f);Check(sprite.color.a<returning&&sprite.color.a>=.35f&&sprite.color.a<.36f,"Return becomes translucent near the barricade");
        int health=GameManager.WallHP,successes=RunState.EscortSuccesses;collision.Invoke(actor,new object[]{wall});collision.Invoke(actor,new object[]{wall});
        Check(actor.IsResolved&&RunState.EscortSuccesses==successes+1,"Return contact resolves escort success exactly once");Check(GameManager.WallHP==health,"Gavroche arrival cannot damage the barricade");Check(!go.activeSelf,"Returned actor hides immediately without a duplicate sprite");
        var effects=UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None);Check(effects.Length==1&&effects[0].RendererCount>0,"Successful return creates only a character-local visual");var effect=effects[0];effect.Sample(.5f);var copy=effect.GetComponentInChildren<SpriteRenderer>();
        Check(copy.color.a<1&&copy.sharedMaterial.shader.name=="Sprites/EnemyWallImpactBlur"&&copy.sharedMaterial.GetFloat("_BlurPixels")>0,"Gavroche return uses the same local blur and fade as enemy arrival");Check(Camera.main.GetComponent<WallImpactBlur>()==null||!Camera.main.GetComponent<WallImpactBlur>().enabled,"Arrival has no whole-screen effect");
        yield return Frames(2);Check(go==null&&effect!=null&&copy.sprite!=null,"Sprite visual survives original Gavroche destruction");float begin=Time.unscaledTime;while(Time.unscaledTime-begin<.4f)yield return null;Check(UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None).Length==0,"Arrival visual cleans up after its short fade");
        var failed=Instantiate(Resources.Load<GameObject>("CampaignActors/Gavroche"),new Vector3(0,20,0),Quaternion.identity);var failedActor=failed.GetComponent<CampaignActor>();failedActor.enabled=false;failedActor.ConfigureWave(null,0,8);
        var soldier=Instantiate(Resources.Load<GameObject>("enemy/Soldier"),new Vector3(5,20,0),Quaternion.identity);int failures=RunState.EscortFailures;collision.Invoke(failedActor,new object[]{soldier.GetComponent<Collider2D>()});Check(failedActor.IsResolved&&RunState.EscortFailures==failures+1,"Soldier interception still resolves escort failure");Check(UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None).Length==0,"Failure does not incorrectly play the safe-return effect");Destroy(soldier);
    }
}
#endif
