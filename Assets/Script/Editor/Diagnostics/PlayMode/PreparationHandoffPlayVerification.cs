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
public static class PreparationHandoffPlayVerification
{
    const string Pending="1832.PreparationHandoff.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/PreparationHandoffPlay.request"));
    static PreparationHandoffPlayVerification(){EditorApplication.update+=Poll;}
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
        story.gameObject.AddComponent<PreparationHandoffPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class PreparationHandoffPlayProbe : MonoBehaviour
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
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"建造防守转场Play检查.json"),JsonUtility.ToJson(report,true));PreparationHandoffPlayVerification.Finished();
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
        var node=new GameObject("UiCapture");var cam=node.AddComponent<Camera>();if(Camera.main!=null){cam.CopyFrom(Camera.main);cam.transform.SetPositionAndRotation(Camera.main.transform.position,Camera.main.transform.rotation);}else{cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;}cam.nearClipPlane=.1f;cam.farClipPlane=100;
        var target=new RenderTexture(width,height,24);target.Create();var active=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{cam.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());}
        finally{canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;RenderTexture.active=active;target.Release();Destroy(target);Destroy(texture);Destroy(node);Canvas.ForceUpdateCanvases();}
    }



    IEnumerator Prep(int hp=595)
    {
        GameManager.WallHP=hp;StartCoroutine(story.DebugPhase("prep"));yield return Until(()=>story.Phase==CampaignPhase.Preparation);yield return Frames(3);
    }
    IEnumerator Test()
    {
        yield return Frames(3);DebugConsole.Current?.SetOpen(false);
        StartCoroutine(story.DebugChapter(3,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&WaveDirector.Current!=null);yield return Prep();
        var beat=FindFirstObjectByType<HeartFill>().GetComponent<Heartbeat>();int heartbeatId=beat.GetInstanceID();var clock=story.Clock;
        var music=Get<AudioSource>(story,"audioSource");Get<CampaignMusicFader>(story,"musicFader").SnapToPhase();var clip=music.clip;float musicPosition=music.time;
        var actor=FindFirstObjectByType<HeroConstructionDriver>().actor;actor.SetCarrying(true);
        var tap=FindFirstObjectByType<Tap>();typeof(StoryDirector).GetMethod("ApplyConstructionResult",Private).Invoke(story,new object[]{tap,ConstructionResult.Ordinary});
        yield return Until(()=>story.PreparationFeedback);
        Check(story.Phase==CampaignPhase.Transition&&story.PreparationCompletionText=="修补完成","Full health starts short completion feedback");
        Check(actor.CurrentMotion==HeroMotion.Place&&actor.IsStoryControlled,"Carrying hero plays the existing place-down animation");
        Check(story.Clock==clock&&StoryDirector.HeartbeatRunning,"Construction heartbeat clock keeps running during handoff");
        yield return Frames(1);Check(FindFirstObjectByType<ConstructionCueView>().actionLabel.text.Contains("+5"),"Last ordinary +5 feedback remains visible before fading");CaptureUi(1280,720,"完成反馈");
        yield return Until(()=>Get<CampaignUI>(story,"ui").Black.color.a>.05f&&Get<CampaignUI>(story,"ui").Black.color.a<.95f);
        Check(actor.CurrentMotion==HeroMotion.RaiseGun,"Hero raises the gun visibly in construction scene before black covers the load");
        var ui=Get<CampaignUI>(story,"ui");float fade=ui.Black.color.a;var session=CampaignSession.Current;session.Pause();yield return Frames(4);
        Check(Mathf.Abs(ui.Black.color.a-fade)<.001f,"Pause freezes the fade instead of skipping it");session.Resume();yield return Frames(2);
        yield return Until(()=>story.DefenseReady);float readyAt=Time.unscaledTime;float period=story.Clock.Period;yield return Frames(2);
        Check(story.PreparationHandoff&&story.Phase==CampaignPhase.Transition&&WaveDirector.Current==null,"Defense scene loads before any wave or combat starts");
        Check(beat!=null&&beat.GetInstanceID()==heartbeatId&&beat.gameObject.scene.name=="2","The same heartbeat object and audio scheduler survive the scene switch");
        Check(FindObjectsByType<Heartbeat>(FindObjectsSortMode.None).Count(h=>h.GetComponent<HeartFill>()!=null)==1,"Duplicate scene heartbeat is removed before overlapping audio");
        Check(beat.AudioTrace.Length>0,"Preserved heartbeat continues scheduled audio through the transition");
        Check(music.clip==clip&&music.time>=musicPosition&&music.volume<=story.gameplayMusicVolume+.01f,"Music keeps its clip/cursor and quiet gameplay level");
        Check(FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).Length==0&&FindObjectsByType<BombHP>(FindObjectsSortMode.None).Length==0,"No enemies or bombs appear during readiness");
        var gun=FindFirstObjectByType<SBullet>();Check(!gun.CanFire,"Readiness blocks actual shots");
        var track=FindFirstObjectByType<CapsuleTrack>();track.DebugLane(1);FindFirstObjectByType<TapSec>().CampaignSetAim("Up");
        Check(CapsuleTrack.AttackIndex==0&&TapSec.ShootStatu=="Up","Player can select lane and aim while getting ready");
        yield return Until(()=>ui.Black.color.a==0);CaptureUi(1280,720,"防守就位");
        Check(ui.Hud.text.Contains("准备迎战")&&FindFirstObjectByType<AmmoHudView>().visibility.alpha==1,"Readiness shows controls and the original heart HUD");
        yield return Until(()=>story.Phase==CampaignPhase.Defense);yield return Frames(3);
        Check(Time.unscaledTime-readyAt>=period-.05f,"At least a whole heartbeat is available before defense begins");
        Check(!story.PreparationHandoff&&WaveDirector.Current!=null&&story.Clock.Completed<2,"Defense starts with a fresh clock and configured wave");
        Check(beat!=null&&FindObjectsByType<Heartbeat>(FindObjectsSortMode.None).Count(h=>h.GetComponent<HeartFill>()!=null)==1,"Only one original heartbeat remains after handoff");
        Check(gun.CanFire&&GameManager.WallHP==600,"Combat unlocks normally without changing wall health");
        // Time-limit exit must not claim the wall was fully built.
        yield return Prep(590);story.Clock.DebugComplete(story.Chapter.preparationBeats);yield return Until(()=>story.PreparationFeedback);
        Check(story.PreparationCompletionText=="敌人来了，准备迎战"&&GameManager.WallHP==590,"Time limit uses the under-full completion message");
        yield return Until(()=>story.DefenseReady);StartCoroutine(story.DebugPhase("title"));yield return Until(()=>story.Phase==CampaignPhase.Title);yield return Frames(3);
        Check(!story.PreparationHandoff&&!story.DefenseReady,"Returning to title cancels an in-progress handoff");
        Check(!FindObjectsByType<Heartbeat>(FindObjectsSortMode.None).Any(h=>h.GetComponent<HeartFill>()!=null),"Cancelling leaves no detached heartbeat in the persistent scene");
        // Existing authored transitions remain authoritative.
        StartCoroutine(story.DebugChapter(3,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&WaveDirector.Current!=null);
        var slot=story.Chapter.prepareToDefense;bool previous=slot.waitForSignal;slot.waitForSignal=true;
        try
        {
            yield return Prep(590);GameManager.AddWallHP(10);yield return Until(()=>story.Phase==CampaignPhase.Transition);
            Check(!story.PreparationHandoff&&!story.DefenseReady,"Signal-driven authored transitions keep their original path");
            story.CompleteTransition();yield return Until(()=>story.Phase==CampaignPhase.Defense);
        }
        finally{slot.waitForSignal=previous;}
        // Pure construction chapter keeps its exit route.
        RunState.ProtagonistIntroduced=true;RunState.ChapterStartHP=595;GameManager.WallHP=595;
        StartCoroutine(story.DebugChapter(2,false,true));yield return Until(()=>story.Phase==CampaignPhase.Preparation,true);
        GameManager.AddWallHP(5);yield return Until(()=>story.PreparationFeedback);Check(story.PreparationCompletionText=="街垒已成","Chapter two shows construction-complete feedback");
        yield return Until(()=>story.Phase==CampaignPhase.Results,true);Check(RunState.Chapter==2&&WaveDirector.Current==null,"Chapter two still ends without creating defense");
        report.coverage="Real preparation completion/+5 feedback, carrying Place animation, fade and pause, actual scene load retaining heartbeat instance/schedule, BGM cursor/volume, empty readiness scene and aim/lane APIs, whole-beat lead-in, new wave/clock, timed incomplete wall, cancellation cleanup, authored Signal override, pure-construction chapter2. Native screenshots include scene camera and Canvas. No hardware key simulation, disk-save overwrite or executable rebuild.";
    }
}
#endif
