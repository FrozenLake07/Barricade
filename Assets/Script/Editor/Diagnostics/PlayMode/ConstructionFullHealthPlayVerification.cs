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
public static class ConstructionFullHealthPlayVerification
{
    const string Pending="1832.ConstructionFullHealth.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ConstructionFullHealthPlay.request"));
    static ConstructionFullHealthPlayVerification(){EditorApplication.update+=Poll;}
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
        story.gameObject.AddComponent<ConstructionFullHealthPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class ConstructionFullHealthPlayProbe : MonoBehaviour
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
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"建造满血Play检查.json"),JsonUtility.ToJson(report,true));ConstructionFullHealthPlayVerification.Finished();
    }
    IEnumerator Until(Func<bool> ready,bool skipCards=false)
    {
        float deadline=Time.unscaledTime+25f;for(int i=0;i<20000&&Time.unscaledTime<deadline;i++){if(ready())yield break;if(skipCards)story.DebugSkip();yield return null;}throw new TimeoutException("Play check waiting for "+story.Phase);
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
    void CaptureUi(int width,int height,string name)
    {
        var canvas=Get<CampaignUI>(story,"ui").View.GetComponent<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
        var node=new GameObject("UiCapture");var cam=node.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.nearClipPlane=.1f;cam.farClipPlane=100;
        var target=new RenderTexture(width,height,24);target.Create();var active=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{cam.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());}
        finally{canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;RenderTexture.active=active;target.Release();Destroy(target);Destroy(texture);Destroy(node);Canvas.ForceUpdateCanvases();}
    }


    int transitions;
    void TransitionObserved(int chapter,string key){if(key=="prepare-to-defense")transitions++;}
    IEnumerator Test()
    {
        yield return Frames(3);DebugConsole.Current?.SetOpen(false);StoryDirector.TransitionRequested+=TransitionObserved;
        try
        {
            StartCoroutine(story.DebugChapter(3,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&WaveDirector.Current!=null);
            GameManager.WallHP=590;StartCoroutine(story.DebugPhase("prep"));yield return Until(()=>story.Phase==CampaignPhase.Preparation);yield return Frames(4);
            Check(story.Phase==CampaignPhase.Preparation,"Health below maximum keeps construction active");
            var cue=FindFirstObjectByType<ConstructionCueView>();var rect=cue.GetComponent<RectTransform>();var parent=(RectTransform)rect.parent;var pcenter=parent.TransformPoint(parent.rect.center);var center=rect.TransformPoint(rect.rect.center);
            Check(Vector3.Distance(center,pcenter)<.1f,"Construction UI group is exactly at the Canvas center");
            Check(cue.visibility.alpha==1&&cue.keyLabel.font==GameFont.Silver,"Centered prompt stays visible with the existing game font");CaptureUi(1280,720,"建造居中1280");CaptureUi(960,540,"建造居中960");
            // Save below full, restore full via an in-memory save, and verify the normal update handles it.
            var data=story.CaptureSavedGame();foreach(var field in data.wall)if(field.name=="WallHP")field.value="600";
            StartCoroutine(story.RestoreSavedGame(data));yield return Until(()=>story.Phase==CampaignPhase.Defense);
            Check(transitions==1&&GameManager.WallHP==600,"Restoring a full-health construction save skips to defense exactly once");
            for(int chapter=3;chapter<=9;chapter++)
            {
                StartCoroutine(story.DebugChapter(chapter,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&WaveDirector.Current!=null&&!story.DebugBusy);
                GameManager.WallHP=GameManager.WallMaxHP;int before=transitions;
                StartCoroutine(story.DebugPhase("prep"));yield return Until(()=>story.Phase==CampaignPhase.Defense&&transitions>before);
                Check(transitions==before+1&&GameManager.WallHP==GameManager.WallMaxHP,"Chapter "+chapter+" full health skips preparation using its existing transition");
                Check(story.Clock.Completed<3&&WaveDirector.Current!=null,"Chapter "+chapter+" starts defense clock and wave without fabricating preparation beats");
            }
            GameManager.WallHP=595;StartCoroutine(story.DebugPhase("prep"));yield return Until(()=>story.Phase==CampaignPhase.Preparation);yield return Frames(2);
            var session=CampaignSession.Current;session.Pause();int pausedTransitions=transitions;GameManager.AddWallHP(5);yield return Frames(4);
            Check(story.Phase==CampaignPhase.Preparation&&transitions==pausedTransitions,"Pause prevents a full-health construction handoff until resume");session.Resume();yield return Until(()=>story.Phase==CampaignPhase.Defense);
            Check(transitions==pausedTransitions+1,"Resume completes the full-health handoff once");
            // Chapter two has no defense phase; reaching full must retain its original chapter-exit flow.
            RunState.ProtagonistIntroduced=true;RunState.ChapterStartHP=595;GameManager.WallHP=595;
            StartCoroutine(story.DebugChapter(2,false,true));yield return Until(()=>story.Phase==CampaignPhase.Preparation,true);yield return Frames(2);
            int fullBeat=story.Clock.Completed;var tap=FindFirstObjectByType<Tap>();
            typeof(StoryDirector).GetMethod("ApplyConstructionResult",Private).Invoke(story,new object[]{tap,ConstructionResult.Ordinary});
            Check(GameManager.WallHP==600,"Actual ordinary construction input can fill the final five health");
            yield return Until(()=>story.Phase==CampaignPhase.Results,true);
            Check(story.Clock.Completed<story.Chapter.preparationBeats&&fullBeat<story.Chapter.preparationBeats,"Chapter two completes early instead of waiting for its beat budget");
            Check(RunState.Chapter==2&&story.Chapter.defenseBeats==0,"Full-health chapter two never creates an unintended defense phase");
            report.coverage="Real preparation scenes at 1280x720/960x540, in-memory full-health save restoration, full-health chapters3-9 with existing transition/clock/wave, pause/resume, actual +5 input and pure-construction chapter2 completion. Component APIs accelerate entry and picture skipping; no disk save overwrite or full campaign playthrough.";
        }
        finally{StoryDirector.TransitionRequested-=TransitionObserved;}
    }
}
#endif
