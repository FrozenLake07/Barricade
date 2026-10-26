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
public static class GrenadeWallDamagePlayVerification
{
    const string Pending="1832.GrenadeWallDamage.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/GrenadeWallDamagePlay.request"));
    static GrenadeWallDamagePlayVerification(){EditorApplication.update+=Poll;}
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
        story.gameObject.AddComponent<GrenadeWallDamagePlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class GrenadeWallDamagePlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Real grenade prefab, physics wall contact and scheduled WaveThreat arrival in actual chapter defense; no full playthrough or hardware input injection. User scene not saved.";}
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
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"手榴弹撞垒Play检查.json"),JsonUtility.ToJson(report,true));GrenadeWallDamagePlayVerification.Finished();
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
    BombHP Bomb(Vector3 position,int group)
    {
        var go=Instantiate(Resources.Load<GameObject>("arrow/Bomb"),position,Quaternion.identity);var hp=go.GetComponent<BombHP>();hp.Configure(group,position.y);return hp;
    }
    Color32[] CaptureHeart(int hp,string name)
    {
        GameManager.WallHP=hp;foreach(var h in UnityEngine.Object.FindObjectsByType<HeartFill>(FindObjectsSortMode.None))h.RefreshNow();
        var hud=UnityEngine.Object.FindFirstObjectByType<AmmoHudView>();foreach(var pulse in UnityEngine.Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None)){pulse.enabled=false;pulse.transform.localScale=Vector3.one;}
        typeof(AmmoHudView).GetMethod("LateUpdate",Private).Invoke(hud,null);
        var canvas=Get<CampaignUI>(story,"ui").View.GetComponent<Canvas>();var mode=canvas.renderMode;var cam=canvas.worldCamera;float distance=canvas.planeDistance;
        var go=new GameObject("HeartRatioCapture");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var target=new RenderTexture(1280,720,24);target.Create();var oldActive=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            var corners=new Vector3[4];hud.heartPulse.GetWorldCorners(corners);var a=camera.WorldToScreenPoint(corners[0]);var b=camera.WorldToScreenPoint(corners[2]);
            int x=Mathf.Clamp(Mathf.FloorToInt(a.x),0,1279),y=Mathf.Clamp(Mathf.FloorToInt(a.y),0,719),w=Mathf.Min(1280-x,Mathf.CeilToInt(b.x)-x),h=Mathf.Min(720-y,Mathf.CeilToInt(b.y)-y);
            var pixels=image.GetPixels(x,y,w,h);return pixels.Select(c=>(Color32)c).ToArray();
        }
        finally{canvas.renderMode=mode;canvas.worldCamera=cam;canvas.planeDistance=distance;RenderTexture.active=oldActive;target.Release();Destroy(target);Destroy(image);Destroy(go);Canvas.ForceUpdateCanvases();}
    }
    IEnumerator Test()
    {
        yield return Frames(3);DebugConsole.Current?.SetOpen(false);StartCoroutine(story.DebugChapter(8,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&WaveDirector.Current!=null);
        CampaignDebug.Value.god=false;CampaignDebug.Value.freeze=true;
        var wave=WaveDirector.Current;var wall=wave.Wall;float y=wave.LaneY(2);var edge=wall.ClosestPoint(new Vector2(wall.bounds.min.x-1,y));
        var full=CaptureHeart(600,"心脏600");var damaged=CaptureHeart(580,"心脏580");var half=CaptureHeart(300,"心脏300");
        int changed20=full.Zip(damaged,(a,b)=>a.Equals(b)?0:1).Sum(),changed300=full.Zip(half,(a,b)=>a.Equals(b)?0:1).Sum();
        var heartHud=UnityEngine.Object.FindFirstObjectByType<AmmoHudView>();var sprite=heartHud.heartFill.sprite;
        Check(changed20>0,"Twenty damage visibly changes the actual heart graphic");
        File.WriteAllText(Path.Combine(output,"血条诊断.txt"),"changed20="+changed20+"; changed300="+changed300+"; spriteRect="+sprite.rect+"; padding="+UnityEngine.Sprites.DataUtility.GetPadding(sprite)+"; vertices="+string.Join(";",sprite.vertices.Select(v=>v.ToString())));
        GameManager.WallHP=600;var direct=Bomb(new Vector3(edge.x-4,y,0),Spawn.NewGroupId());direct.Explode(true);Check(GameManager.WallHP==580,"Direct bomb impact deducts current twenty-point wall damage");yield return Frames(2);
        GameManager.WallHP=600;int group=Spawn.NewGroupId();var flying=Bomb(new Vector3(edge.x-.7f,edge.y,0),group);var companion=Bomb(new Vector3(edge.x-4,y,0),group);var last=Bomb(new Vector3(edge.x-5,y,0),group);
        var body=flying.GetComponent<Rigidbody2D>();body.gravityScale=0;body.velocity=Vector2.right*8;
        float started=Time.unscaledTime;while(flying!=null&&flying.IsAlive&&Time.unscaledTime-started<2)yield return null;
        Check(flying==null||!flying.IsAlive,"Moving grenade actually contacts the wall and explodes");Check(GameManager.WallHP==580,"Physics wall contact deducts damage exactly once for the whole volley");Check(!companion.IsAlive&&!last.IsAlive,"Physics contact chains the remaining two grenades");yield return Frames(2);
        GameManager.WallHP=600;group=Spawn.NewGroupId();var intercepted=Bomb(new Vector3(edge.x-4,y,0),group);var chain=Bomb(new Vector3(edge.x-5,y,0),group);intercepted.TakeDamage(100);
        Check(GameManager.WallHP==600&&!intercepted.IsAlive&&!chain.IsAlive,"Player interception chains without damaging the wall");yield return Frames(2);
        CampaignDebug.Value.freeze=false;GameManager.WallHP=600;var scheduled=Bomb(new Vector3(edge.x-5,y,0),Spawn.NewGroupId());scheduled.gameObject.AddComponent<WaveThreat>().Initialize(wave,WaveEventType.Squad,2,wave.Position+.5,"probe-wall-arrival");
        started=Time.unscaledTime;while(scheduled!=null&&scheduled.IsAlive&&Time.unscaledTime-started<3)yield return null;
        Check(scheduled==null||!scheduled.IsAlive,"Scheduled wave grenade resolves arrival");Check(GameManager.WallHP==580,"Scheduled wave arrival also applies wall damage");
    }
}
#endif
