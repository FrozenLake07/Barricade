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
public static class MechanicIntroductionPlayVerification
{
    const string Pending="1832.MechanicIntroduction.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/MechanicIntroductionPlay.request"));
    static MechanicIntroductionPlayVerification(){EditorApplication.update+=Poll;}
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
        story.gameObject.AddComponent<MechanicIntroductionPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class MechanicIntroductionPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Actual chapter-entry order, modal input guard/pause, unchanged beat/HP state, all six mechanic pages and native Canvas screenshots. No hardware key injection or full playthrough; user scene not saved.";}
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
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"机制介绍Play检查.json"),JsonUtility.ToJson(report,true));MechanicIntroductionPlayVerification.Finished();
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
    IEnumerator Delay(float seconds){float begin=Time.unscaledTime;while(Time.unscaledTime-begin<seconds)yield return null;}
    void Screenshot(MechanicIntroductionView view,int chapter,int width=1280,int height=720)
    {
        var ui=Get<CampaignUI>(story,"ui");var canvas=ui.View.GetComponent<Canvas>();var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
        var cameraObject=new GameObject("MechanicCaptureCamera");var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.1f;camera.farClipPlane=100;
        var target=new RenderTexture(width,height,24);target.Create();var oldActive=RenderTexture.active;var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"第"+chapter+"章机制介绍_"+width+"x"+height+".png"),pixels.EncodeToPNG());}
        finally{canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;RenderTexture.active=oldActive;target.Release();Destroy(target);Destroy(pixels);Destroy(cameraObject);Canvas.ForceUpdateCanvases();}
    }
    IEnumerator Enter(int chapter,bool retry,Action done){yield return story.DebugChapter(chapter,false,retry);done();}
    IEnumerator Test()
    {
        yield return Frames(3);DebugConsole.Current?.SetOpen(false);CampaignDebug.Value.god=true;
        var events=new List<string>();Action<int,string> track=(n,key)=>events.Add(n+":"+key);StoryDirector.TransitionRequested+=track;
        bool done=false;StartCoroutine(Enter(3,false,()=>done=true));
        yield return Until(()=>Get<MechanicIntroductionView>(story,"mechanicIntroduction")?.Visible==true,true);
        var view=Get<MechanicIntroductionView>(story,"mechanicIntroduction");
        Check(!done&&story.Phase==CampaignPhase.Transition,"Chapter 3 waits on introduction before gameplay");
        Check(events.IndexOf("3:chapter-entry")>=0&&events.IndexOf("3:mechanic-introduction")>events.IndexOf("3:chapter-entry"),"Introduction follows actual chapter-opening picture route");
        Check(!Get<CampaignCinematicView>(story,"cinematicView").Active,"Opening picture is hidden while introduction is shown");
        Check(!view.RequestContinue(),"Same-frame accidental continue is rejected");
        int beats=RunState.TotalHeartbeats,hp=GameManager.WallHP,completed=story.Clock.Completed;
        yield return Delay(.5f);Check(view.Visible&&!done&&RunState.TotalHeartbeats==beats&&GameManager.WallHP==hp&&story.Clock.Completed==completed,"Reading waits indefinitely without heartbeat or health progression");
        DebugConsole.Current.SetOpen(true);Check(!view.RequestContinue(),"Console pause blocks introduction continue");DebugConsole.Current.SetOpen(false);yield return Frames(2);
        var labels=view.GetComponentsInChildren<UnityEngine.UI.Text>(true); // Page shares Canvas, inspect its own node instead.
        var screen=Get<CampaignUI>(story,"ui").View;var page=screen.transform.Find("MechanicIntroduction");
        Check(page!=null&&page.gameObject.activeInHierarchy,"Dedicated introduction page belongs to the existing campaign Canvas");
        Check(page.GetComponentsInChildren<UnityEngine.UI.Text>(true).All(t=>t.font==GameFont.Silver),"All introduction text uses the existing Silver game font");
        Screenshot(view,3);Check(view.RequestContinue(),"Ready introduction accepts continue");yield return Until(()=>done,true);
        Check(story.Phase==CampaignPhase.Preparation&&!view.Visible,"Continue hands over to chapter 3 preparation and hides the page");
        foreach(int n in new[]{2,3,4,5,8,9})
        {
            RunState.Chapter=n;var beforeMusic=Get<AudioSource>(story,"audioSource").clip;int beforeBeats=RunState.TotalHeartbeats;
            bool finished=false;StartCoroutine(Show(n,()=>finished=true));yield return Until(()=>view.Visible);
            Check(!string.IsNullOrEmpty(view.Heading)&&!string.IsNullOrEmpty(view.Body),"Mechanic copy renders: chapter "+n);
            if(n==2)Check(view.Body.Contains("140"),"Construction introduction uses current editable chord timing");
            Screenshot(view,n);if(n==5)Screenshot(view,n,960,540);
            yield return Delay(.4f);Check(!finished&&RunState.TotalHeartbeats==beforeBeats&&Get<AudioSource>(story,"audioSource").clip==beforeMusic,"Page does not advance beats or replace music: chapter "+n);
            page.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single().onClick.Invoke();yield return Until(()=>finished);
        }
        events.Clear();done=false;StartCoroutine(Enter(8,false,()=>done=true));yield return Until(()=>view.Visible,true);
        Check(events.IndexOf("8:chapter-entry")>=0&&events.IndexOf("8:mechanic-introduction")>events.IndexOf("8:chapter-entry"),"Chapter 8 also shows its page after opening pictures");view.RequestContinue(true);yield return Until(()=>done,true);
        events.Clear();done=false;StartCoroutine(Enter(3,true,()=>done=true));yield return Until(()=>done,true);
        Check(!events.Contains("3:mechanic-introduction")&&!view.Visible,"Retry skips repeated introduction");
        events.Clear();done=false;StartCoroutine(Enter(7,false,()=>done=true));yield return Until(()=>done,true);
        Check(!events.Contains("7:mechanic-introduction"),"Chapter without new controls has no duplicate introduction");
        StoryDirector.TransitionRequested-=track;
    }
    IEnumerator Show(int chapter,Action done){yield return (IEnumerator)typeof(StoryDirector).GetMethod("ShowMechanicIntroduction",Private).Invoke(story,new object[]{chapter});done();}
}
#endif
