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
public static class OpeningPictureTimingPlayVerification
{
    const string Pending="1832.OpeningPictureTiming.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/OpeningPictureTimingPlay.request"));
    static OpeningPictureTimingPlayVerification(){EditorApplication.update+=Poll;}
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
        story.gameObject.AddComponent<OpeningPictureTimingPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class OpeningPictureTimingPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Actual chapter 3-8 opening-picture duration allocation, playback/skip handoff, disabled chapter 6 introduction and chapter 1/2 exclusions. Picture elapsed time accelerated through the actual playback state API; no full-song listening or full playthrough. User scene not saved.";}
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
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"开场时长Play检查.json"),JsonUtility.ToJson(report,true));OpeningPictureTimingPlayVerification.Finished();
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
    [Serializable] sealed class Row {public int chapter;public float music,remaining,original,planned;}
    [Serializable] sealed class Plans {public Row[] chapters;}
    IEnumerator Enter(int n,Action done){yield return story.DebugChapter(n);done();}
    double[] Plan(CinematicRoute route,string key)=>(double[])typeof(StoryDirector).GetMethod("OpeningPictureDurations",Private).Invoke(story,new object[]{route,key});
    IEnumerator Test()
    {
        yield return Frames(3);DebugConsole.Current?.SetOpen(false);CampaignDebug.Value.god=true;
        Check(!ChapterMechanicIntroduction.TryGet(6,story.table.chordMaxGap,out _,out _),"Chapter 6 has no unimplemented night-mechanic page");
        var sample=ChapterOpeningPictureTiming.Extend(new double[]{4,6},25);Check(Math.Abs(sample[0]-10)<.001&&Math.Abs(sample[1]-15)<.001,"Extra picture time preserves authored proportions");
        Check(ChapterOpeningPictureTiming.Extend(new double[]{4,6},5).SequenceEqual(new double[]{4,6}),"Short music cannot shorten authored minimum picture times");
        Check(Plan(new CinematicRoute{chapter=1},"chapter-entry")==null&&Plan(new CinematicRoute{chapter=2},"chapter-entry")==null,"Chapters 1 and 2 are excluded from picture retiming");
        Check(Plan(new CinematicRoute{chapter=9},"chapter-entry")==null&&Plan(new CinematicRoute{chapter=10},"ending")==null,"Final assault and endings are excluded");
        var rows=new List<Row>();var events=new List<string>();Action<int,string> track=(n,key)=>events.Add(n+":"+key);StoryDirector.TransitionRequested+=track;
        foreach(int n in new[]{3,4,5,6,7,8})
        {
            events.Clear();bool done=false;StartCoroutine(Enter(n,()=>done=true));
            var route=story.cinematics.Find(n,"chapter-entry",RunState.Decision,RunState.Ending);
            for(int i=0;i<1000;i++)
            {
                if(Film.Active&&!Film.IsBlackCardVisible&&Get<CinematicPlaybackState>(Film,"playback")!=null&&Film.CurrentImage.sprite==route.shots[0].image)break;
                if(Film.IsBlackCardVisible)story.DebugSkip();yield return null;
            }
            Check(Film.Active&&!Film.IsBlackCardVisible&&!done,"Actual opening pictures are playing: chapter "+n);
            var planned=Get<double[]>(Film,"activeDurations");var audio=Get<AudioSource>(story,"audioSource");double original=route.shots.Sum(shot=>(double)shot.Duration),sum=planned.Sum();
            float remaining=audio.clip.length-audio.time;
            Check(sum>=original-.001&&Math.Abs(sum-Math.Max(original,remaining))<.25,"Opening pictures use remaining music with original minimum: chapter "+n);
            for(int i=0;i<planned.Length;i++)Check(planned[i]+.001>=route.shots[i].Duration,"Authored shot is never shortened: chapter "+n+" shot "+i);
            rows.Add(new Row{chapter=n,music=audio.clip.length,remaining=remaining,original=(float)original,planned=(float)sum});
            Check(audio.clip==story.table.chapters[n-1].openingMusic&&!audio.loop,"Existing chapter track and non-loop playback retained: chapter "+n);
            if(n==4)Film.DebugSkip();else Get<CinematicPlaybackState>(Film,"playback").Advance(sum+.01);
            for(int i=0;i<1000&&!done;i++){story.DebugSkip();yield return null;}
            Check(done&&story.Phase==CampaignPhase.Preparation,"Extended pictures hand over to gameplay: chapter "+n);
            if(n==6)Check(!events.Contains("6:mechanic-introduction"),"Chapter 6 goes directly from pictures to preparation without a night tutorial");
            if(n==4)Check(!Film.Active,"Manual/debug skip still ends extended pictures");
            Check(Plan(route,"chapter-exit")==null,"Chapter exit pictures are untouched: chapter "+n);
        }
        File.WriteAllText(Path.Combine(output,"开场分配采样.json"),JsonUtility.ToJson(new Plans{chapters=rows.ToArray()},true));
        StoryDirector.TransitionRequested-=track;
    }
}
#endif
