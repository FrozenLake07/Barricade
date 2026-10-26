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
public static class GameplayPolishPlayVerification
{
    const string Pending="1832.GameplayPolish.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/GameplayPolishPlay.request"));
    static GameplayPolishPlayVerification(){EditorApplication.update+=Poll;}
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
        story.gameObject.AddComponent<GameplayPolishPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class GameplayPolishPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Actual Play coroutine and UI/API integration; skip requested via the same API as any-key input, not injected hardware input. Existing editing scene not saved or replaced.";}
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
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"Play检查.json"),JsonUtility.ToJson(report,true));GameplayPolishPlayVerification.Finished();
    }
    IEnumerator Until(Func<bool> ready,bool skipCards=false)
    {
        for(int i=0;i<600;i++){if(ready())yield break;if(skipCards)story.DebugSkip();yield return null;}throw new TimeoutException("Play check waiting for "+story.Phase);
    }
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    IEnumerator Test()
    {
        yield return Frames(3);RunState.Ending=CampaignEnding.Mourning;RunState.EndingSourceChapter=3;
        StartCoroutine(story.DebugChapter(10));yield return Until(()=>RunState.Chapter==10&&Film.Active&&!Film.IsBlackCardVisible&&Get<CinematicPlaybackState>(Film,"playback")!=null,true);
        Check(!Film.RequestFinalPictureSkip(),"First ending picture cannot trigger any-key finish");
        var playback=Get<CinematicPlaybackState>(Film,"playback");var durations=Get<double[]>(Film,"activeDurations");playback.Advance(durations.Take(durations.Length-1).Sum()+.01);yield return Until(()=>Get<int>(Film,"shownFrame")==durations.Length-1);
        yield return Frames(3);Set(Film,"shotStartedAt",Time.unscaledTime-.5f);
        Check(story.EndingMusicRemaining>1&&Film.CanSkipFinalPicture,"Last picture allows early finish while music is still playing");
        Check(Film.RequestFinalPictureSkip(),"Any-key request API accepts last-picture skip");yield return Until(()=>story.Phase==CampaignPhase.Finished);
        var view=UnityEngine.Object.FindFirstObjectByType<CampaignEndingView>();
        Check(view!=null&&view.Visible&&view.background.sprite==Film.CurrentImage.sprite,"Early finish reveals menu on the retained final picture");
        Check(story.EndingMusicRemaining==0&&!Get<AudioSource>(story,"audioSource").isPlaying,"Early finish stops remaining ending music");
        Check(new[]{view.restartChapter,view.returnTitle,view.quit}.All(b=>b!=null&&b.interactable&&b.gameObject.activeInHierarchy),"All three ending choices are visible and interactive");
        Check(view.background.material.GetFloat("_BlurPixels")>=7.9f,"Ending menu completes its existing background blur");
        foreach(int chapter in Enumerable.Range(3,6))
        {
            typeof(StoryDirector).GetMethod("PlayChapterMusic",Private).Invoke(story,new object[]{chapter});yield return null;
            Check(Get<AudioSource>(story,"audioSource").clip==story.table.chapters[chapter-1].openingMusic&&Get<AudioSource>(story,"audioSource").time<1,"Shared chapter-music entry starts the core clip at its beginning: "+chapter);
        }
        typeof(StoryDirector).GetMethod("StopCampaignMusic",Private).Invoke(story,null);
    }
}
#endif
