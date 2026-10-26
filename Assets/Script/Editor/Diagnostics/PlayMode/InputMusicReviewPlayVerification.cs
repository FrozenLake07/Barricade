#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class InputMusicReviewPlayVerification
{
    const string Pending="1832.InputMusicReview.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/InputMusicReviewPlay.request"));
    static InputMusicReviewPlayVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {string output=File.ReadAllText(Request).Trim();File.Delete(Request);SessionState.SetString(Pending,output);SessionState.SetBool(Pending+".active",true);previousFrame=-1;lastAdvance=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;return;}
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending+".active",false))return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(previousFrame!=Time.frameCount){previousFrame=Time.frameCount;lastAdvance=EditorApplication.timeSinceStartup;}
        if(EditorApplication.timeSinceStartup-lastAdvance>1){EditorApplication.isPaused=true;EditorApplication.Step();}
        string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;SessionState.EraseString(Pending);Application.runInBackground=true;
        var story=StoryDirector.Current;if(story==null)story=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Campaign/CampaignRoot")).GetComponent<StoryDirector>();story.gameObject.AddComponent<InputMusicReviewPlayProbe>().Begin(path);
    }
    public static void Finish(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class InputMusicReviewPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public float animationMiddle,animationEnd,gameplayMiddle,gameplayEnd;}
    readonly List<string> passed=new List<string>();Report report;string output;StoryDirector story;float listenerVolume;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    void Check(bool ok,string text){if(!ok)throw new Exception(text);passed.Add(text);}
    public void Begin(string path){output=path;story=GetComponent<StoryDirector>();report=new Report{startFrame=Time.frameCount};listenerVolume=AudioListener.volume;AudioListener.volume=0;StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            object value=null;bool more=false;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)value=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();AudioListener.volume=listenerVolume;AudioListener.pause=false;Time.timeScale=1;
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.checks=passed.Count;report.passed=passed.ToArray();report.success=string.IsNullOrEmpty(report.error);File.WriteAllText(Path.Combine(output,"Play反馈与渐弱检查.json"),JsonUtility.ToJson(report,true));InputMusicReviewPlayVerification.Finish();
    }
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    IEnumerator Seconds(float n){float begin=Time.unscaledTime;while(Time.unscaledTime-begin<n)yield return null;}
    IEnumerator Until(Func<bool> ready,bool skip=false)
    {for(int i=0;i<1000;i++){if(ready())yield break;if(skip)story.DebugSkip();yield return null;}throw new TimeoutException("Waiting for "+story.Phase);}
    IEnumerator Test()
    {
        yield return Frames(3);Check(story.gameplayMusicFadeSeconds==2,"Actual campaign instance uses two-second fade");
        StartCoroutine(story.DebugChapter(2));yield return Until(()=>UnityEngine.Object.FindFirstObjectByType<TavernNameDialogue>()!=null,true);
        var tavern=UnityEngine.Object.FindFirstObjectByType<TavernNameDialogue>();var source=Get<AudioSource>(story,"audioSource");var fader=Get<CampaignMusicFader>(story,"musicFader");
        Check(Get<bool>(fader,"animationActive"),"Entering actual tavern Timeline enables quiet animation mix");Check(source.volume>.6f,"Animation entry starts at the previous audible gain");
        yield return Seconds(1);report.animationMiddle=source.volume;Check(source.volume>.2f&&source.volume<.62f,"Animation gain changes progressively during the first second");
        yield return Seconds(1.1f);report.animationEnd=source.volume;Check(Math.Abs(source.volume-story.gameplayMusicVolume)<.002,"Animation reaches quiet gain after two seconds");
        tavern.director.Stop();yield return Until(()=>story.Phase==CampaignPhase.Preparation,true);
        Set(story,"phase",CampaignPhase.Transition);fader.SetAnimationActive(false);fader.SnapToPhase();Check(Math.Abs(source.volume-story.cinematicMusicVolume)<.001,"Controlled gameplay-entry test starts at cinematic gain");Set(story,"phase",CampaignPhase.Preparation);
        yield return Seconds(1);report.gameplayMiddle=source.volume;Check(source.volume>.2f&&source.volume<.62f,"Gameplay entry also fades instead of snapping");yield return Seconds(1.1f);report.gameplayEnd=source.volume;Check(Math.Abs(source.volume-story.gameplayMusicVolume)<.002,"Gameplay reaches quiet gain at two seconds");
        var tap=UnityEngine.Object.FindFirstObjectByType<Tap>();var cue=UnityEngine.Object.FindFirstObjectByType<ConstructionCueView>();Check(tap!=null&&cue!=null,"Actual construction scene has its existing cue UI");
        story.Clock.Reset(10,.5f);Set(story,"constructionCycle",story.Clock.Completed);Set(story,"windowJudged",false);var judge=Get<ConstructionJudgement>(story,"construction");judge.Reset(1,-1,.12,Time.unscaledTime-1);judge.Reset(0,-1,.12,Time.unscaledTime);
        Check(judge.Press(1,Time.unscaledTime,false)==ConstructionResult.Grace&&!judge.Resolved,"Previous D is acknowledged during grace while new A remains available");int health=GameManager.WallHP,built=RunState.BuiltHP,judgements=RunState.Judgements.Count,accepted=0,grace=0;tap.InputAccepted+=p=>{if(!p)accepted++;};tap.GraceInputReceived+=k=>{if(k==KeyCode.D)grace++;};
        int sound=0;Action<string> heard=id=>{if(id=="SFX-QTE-02")sound++;};SfxPlayer.Played+=heard;tap.NotifyGraceInput(KeyCode.D);SfxPlayer.Played-=heard;
        Check(accepted==1&&grace==1&&sound==1,"Grace invokes normal actor action, dedicated cue event and fixed tap sound once");yield return Frames(2);
        Check(cue.actionLabel.text.Contains("切换保护")&&cue.actionLabel.text.Contains("D"),"Visible construction cue identifies the protected key");Check(cue.keyLabel.color.r>.9f&&cue.keyLabel.color.b<.5f,"Displayed key visibly flashes in the grace color");
        Check(!judge.Resolved&&!story.IsConstructionResting&&GameManager.WallHP==health&&RunState.BuiltHP==built&&RunState.Judgements.Count==judgements,"Grace feedback has no duplicate reward, score consumption or rest penalty");
        var ordinary=judge.Press(0,Time.unscaledTime,false);Check(ordinary==ConstructionResult.Ordinary,"Current A still accepts normally after the protected D");typeof(StoryDirector).GetMethod("ApplyConstructionResult",Private).Invoke(story,new object[]{tap,ordinary});yield return Frames(1);Check(!cue.actionLabel.text.Contains("切换保护"),"A real current-prompt hit replaces grace feedback immediately");
    }
}
#endif
