#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SelectedChapterMusicSetup
{
    [Serializable] public sealed class Cue {public int chapter;public string source,asset;public float duration;}
    [Serializable] public sealed class Manifest {public Cue[] clips;}
    [Serializable] sealed class Report {public bool success,scenePreserved;public int checks;public string error;public string[] passed;}
    const string Pending="1832.SelectedChapterMusic.Play";
    static int lastFrame=-1;static double lastAdvance;
    static string Temp(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/"+name));
    static SelectedChapterMusicSetup(){EditorApplication.update+=Poll;}
    public static Manifest Read(string output)=>JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(output,"配乐映射.json")));
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string setup=Temp("SelectedChapterMusicSetup.request"),play=Temp("SelectedChapterMusicPlay.request");
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(setup))
        {string output=File.ReadAllText(setup).Trim();File.Delete(setup);Apply(output);return;}
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(play))
        {string output=File.ReadAllText(play).Trim();File.Delete(play);SessionState.SetString(Pending,output);SessionState.SetBool(Pending+".active",true);lastFrame=-1;lastAdvance=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;return;}
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending+".active",false))return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(Time.frameCount!=lastFrame){lastFrame=Time.frameCount;lastAdvance=EditorApplication.timeSinceStartup;}
        if(EditorApplication.timeSinceStartup-lastAdvance>1){EditorApplication.isPaused=true;EditorApplication.Step();}
        string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;SessionState.EraseString(Pending);Application.runInBackground=true;
        var story=StoryDirector.Current;if(story==null)story=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Campaign/CampaignRoot")).GetComponent<StoryDirector>();
        story.gameObject.AddComponent<SelectedChapterMusicPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
    static void Apply(string output)
    {
        var report=new Report();var passed=new List<string>();var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;var roots=scene.GetRootGameObjects();
        Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);passed.Add(text);};
        try
        {
            var cues=Read(output);check(cues.clips.Length==6,"Six selected clips supplied");
            var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");
            var before=table.chapters.Select(c=>c.openingMusic).ToArray();
            Undo.RecordObject(table,"接入第3至8章已审定配乐");
            foreach(var cue in cues.clips)
            {
                AssetDatabase.ImportAsset(cue.asset,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(cue.asset);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;settings.preloadAudioData=true;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(cue.asset);
                check(clip!=null&&Math.Abs(clip.length-cue.duration)<.05&&clip.channels==2,"Imported selected audio length/stereo: chapter "+cue.chapter);
                table.chapters[cue.chapter-1].openingMusic=clip;
            }
            table.Validate();EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);
            foreach(int n in new[]{1,2,9,10})check(table.chapters[n-1].openingMusic==before[n-1],"Other chapter music retained: "+n);
            AssetDatabase.ImportAsset("Assets/Resources/Campaign/ChapterTable.asset",ImportAssetOptions.ForceSynchronousImport);
            table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");
            foreach(var cue in cues.clips)check(AssetDatabase.GetAssetPath(table.chapters[cue.chapter-1].openingMusic)==cue.asset,"Persisted chapter binding: "+cue.chapter);
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            report.scenePreserved=SceneManager.GetActiveScene()==scene&&scene.isDirty==dirty&&roots.All(r=>r!=null&&r.scene==scene);report.success&=report.scenePreserved;report.checks=passed.Count;report.passed=passed.ToArray();
            File.WriteAllText(Path.Combine(output,"原生导入与绑定检查.json"),JsonUtility.ToJson(report,true));
        }
    }
}
public sealed class SelectedChapterMusicPlayProbe:MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Actual chapter entry and hard debug jump for chapters 3-8, live AudioSource binding and save-track resolution. Not full-song human listening or full chapter playthrough.";}
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    string output;StoryDirector story;float listener;Report report;readonly List<string> passed=new List<string>();
    void Check(bool ok,string text){if(!ok)throw new Exception(text);passed.Add(text);}
    object Call(string name,params object[] args)=>typeof(StoryDirector).GetMethod(name,Private).Invoke(story,args);
    public void Begin(string path)
    {output=path;story=GetComponent<StoryDirector>();listener=AudioListener.volume;AudioListener.volume=0;AudioListener.pause=false;Time.timeScale=1;DebugConsole.Current?.SetOpen(false);CampaignDebug.Value.god=true;report=new Report{startFrame=Time.frameCount};StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator task)
    {
        var stack=new Stack<IEnumerator>();stack.Push(task);
        while(stack.Count>0)
        {
            bool more=false;object current=null;Exception failure=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)current=top.Current;else stack.Pop();}catch(Exception e){failure=e;}
            if(failure!=null){report.error=failure.ToString();break;}if(!more)continue;if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
        }
        foreach(var item in stack)(item as IDisposable)?.Dispose();AudioListener.volume=listener;AudioListener.pause=false;Time.timeScale=1;
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.checks=passed.Count;report.passed=passed.ToArray();report.success=string.IsNullOrEmpty(report.error)&&report.framesAdvanced;
        File.WriteAllText(Path.Combine(output,"章节配乐Play检查.json"),JsonUtility.ToJson(report,true));SelectedChapterMusicSetup.Finished();
    }
    IEnumerator Test()
    {
        yield return null;yield return null;yield return null;
        foreach(bool hard in new[]{false,true})foreach(var cue in SelectedChapterMusicSetup.Read(output).clips)
        {
            bool completed=false;
            StartCoroutine(Enter(cue.chapter,hard,()=>completed=true));
            for(int frame=0;!completed&&frame<1000;frame++){story.DebugSkip();yield return null;}
            Check(completed,"Chapter entry completes: "+cue.chapter+" hard="+hard);
            var audio=(AudioSource)typeof(StoryDirector).GetField("audioSource",Private).GetValue(story);
            var expected=AssetDatabase.LoadAssetAtPath<AudioClip>(cue.asset);
            Check(RunState.Chapter==cue.chapter&&audio.clip==expected&&!audio.loop,"Actual chapter music matches selection: "+cue.chapter+" hard="+hard);
            Check((AudioClip)Call("ResolveMusicTrack","chapter-"+cue.chapter)==expected,"Save identity resolves selected clip: "+cue.chapter+" hard="+hard);
            Check((AudioClip)Call("ResolveMusicTrack","people")==expected,"Legacy save identity uses current chapter: "+cue.chapter+" hard="+hard);
        }
        Check((AudioClip)Call("ResolveMusicTrack","unknown")==null,"Unknown identity never falls back to old People Sing");
        Check(story.gameplayMusicFadeSeconds==2,"Existing two-second music fade retained");
    }
    IEnumerator Enter(int chapter,bool hard,Action done){yield return story.DebugChapter(chapter,hard);done();}
}
#endif
