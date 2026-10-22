#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EndingMusicVerification
{
    const string Pending="Barricade.EndingMusic.Pending";
    static int frame=-1;static double changed;static bool steps;
    static string Temp(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/"+name));
    static EndingMusicVerification(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Pending+".active",false)){PlayerSettings.runInBackground=SessionState.GetBool(Pending+".background",false);EditorApplication.isPaused=false;SessionState.EraseBool(Pending+".active");}};}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Temp("EndingMusicSetup.request")))
        {
            string output=File.ReadAllText(Temp("EndingMusicSetup.request")).Trim();File.Delete(Temp("EndingMusicSetup.request"));
            try{Setup();File.WriteAllText(Path.Combine(output,"原生资源配置.txt"),"success");}catch(Exception e){File.WriteAllText(Path.Combine(output,"原生资源配置.txt"),e.ToString());}return;
        }
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Temp("EndingMusicPlay.request")))
        {
            string output=File.ReadAllText(Temp("EndingMusicPlay.request")).Trim();File.Delete(Temp("EndingMusicPlay.request"));
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("请先保存场景。");
            SessionState.SetBool(Pending+".background",PlayerSettings.runInBackground);SessionState.SetBool(Pending+".active",true);
            PlayerSettings.runInBackground=true;EditorApplication.isPaused=false;frame=-1;steps=false;changed=EditorApplication.timeSinceStartup;
            SessionState.SetString(Pending,output);EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending+".active",false))return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(frame!=Time.frameCount){frame=Time.frameCount;changed=EditorApplication.timeSinceStartup;}
        if(steps||EditorApplication.timeSinceStartup-changed>1){steps=true;EditorApplication.isPaused=true;EditorApplication.Step();}
        if(StoryDirector.Current!=null&&!string.IsNullOrEmpty(SessionState.GetString(Pending,"")))
        {string output=SessionState.GetString(Pending,"");SessionState.EraseString(Pending);StoryDirector.Current.gameObject.AddComponent<EndingMusicPlayProbe>().Begin(output);}
    }
    static CinematicRoute Clone(CinematicRoute source)=>JsonUtility.FromJson<CinematicRoute>(JsonUtility.ToJson(source));
    static void Setup()
    {
        var clips=new List<AudioClip>();
        foreach(string name in new[]{"Do You Hear The People Sing","Empty Chairs At Empty Tables","Finale"})
        {
            string path="Assets/Audio/CampaignRecordings/"+name+".wav";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.75f;
            settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.loadInBackground=false;importer.SaveAndReimport();
            clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }
        string prefab="Assets/Resources/Campaign/CampaignRoot.prefab";var root=PrefabUtility.LoadPrefabContents(prefab);
        try{var d=root.GetComponent<StoryDirector>();d.chapterMusic=clips[0];d.mourningMusic=clips[1];d.finaleMusic=clips[2];PrefabUtility.SaveAsPrefabAsset(root,prefab);}finally{PrefabUtility.UnloadPrefabContents(root);}
        var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");Undo.RecordObject(table,"终章名字");table.chapters[9].title="终章";EditorUtility.SetDirty(table);
        var c=AssetDatabase.LoadAssetAtPath<CampaignCinematics>("Assets/Resources/Campaign/Cinematics.asset");Undo.RecordObject(c,"终章两条分镜路线");
        var entry=c.routes.First(r=>r.chapter==10&&r.key=="chapter-entry"&&r.ending!=CampaignEnding.Mourning);
        var ending=c.routes.First(r=>r.chapter==10&&r.key=="ending"&&r.ending!=CampaignEnding.Mourning);
        entry.ending=ending.ending=CampaignEnding.Hope;entry.shots[3].triggerHopeFinale=true;
        if(!c.routes.Any(r=>r.chapter==10&&r.ending==CampaignEnding.Mourning))
        {
            var mournEntry=Clone(entry);mournEntry.ending=CampaignEnding.Mourning;mournEntry.shots=mournEntry.shots.Take(3).ToArray();
            var mournEnd=Clone(mournEntry);mournEnd.key="ending";mournEnd.shots=new[]{mournEnd.shots[2]};
            c.routes=c.routes.Concat(new[]{mournEntry,mournEnd}).ToArray();
        }
        c.Validate();table.Validate();EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();CampaignVerification.Check();
    }
}

public sealed class EndingMusicPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report{public bool success,nativePlayVerified;public int startFrame,endFrame,checks;public string error;public string[] tested;public float peopleLength,mourningLength,finaleLength;}
    Report report;string output;readonly List<string> tested=new List<string>();StoryDirector story;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    AudioSource Music=>(AudioSource)typeof(StoryDirector).GetField("audioSource",Private).GetValue(story);
    bool PlaybackRequested=>(bool)typeof(StoryDirector).GetField("musicPlaybackActive",Private).GetValue(story);
    void Require(bool ok,string why){report.checks++;tested.Add(why);if(!ok)throw new Exception(why);}
    IEnumerator Invoke(string method,params object[] args)=>(IEnumerator)typeof(StoryDirector).GetMethod(method,Private).Invoke(story,args);
    void Call(string method,params object[] args)=>typeof(StoryDirector).GetMethod(method,Private).Invoke(story,args);
    public void Begin(string directory){output=directory;story=StoryDirector.Current;report=new Report{startFrame=Time.frameCount};StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            bool more=false;object next=null;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)next=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();DebugConsole.Current?.SetOpen(false);Time.timeScale=1;AudioListener.pause=false;
        report.endFrame=Time.frameCount;report.nativePlayVerified=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.tested=tested.ToArray();
        File.WriteAllText(Path.Combine(output,"结局原生Play验证.json"),JsonUtility.ToJson(report,true));EditorApplication.isPlaying=false;
    }
    IEnumerator Until(Func<bool> ready,bool skip=false)
    {
        for(int i=0;i<400;i++){if(ready())yield break;if(skip&&i%3==2)story.DebugSkip();yield return null;}
        throw new TimeoutException("等待阶段超时："+story.Phase+"/"+RunState.Chapter);
    }
    IEnumerator Stable()=>Until(()=>!story.DebugBusy,true);
    IEnumerator Test()
    {
        yield return null;story.cinematics=Instantiate(story.cinematics);Require(story.Phase==CampaignPhase.Title&&!Music.isPlaying,"标题无音乐");
        report.peopleLength=story.chapterMusic.length;report.mourningLength=story.mourningMusic.length;report.finaleLength=story.finaleMusic.length;
        Require(story.mourningMusic!=null&&story.finaleMusic!=null,"两首结局音乐引用存在");
        Require(CampaignHudPresentation.Announcement(10,story.table.chapters[9].title)=="终章","终章报幕名字正确");
        story.StartNewCampaign();yield return Until(()=>story.Phase==CampaignPhase.Calibration);
        Require(!Music.isPlaying,"校音期间保持背景音乐静音");story.DebugSkip();
        yield return Until(()=>story.Phase!=CampaignPhase.Calibration);
        Require(RunState.Chapter==1&&story.GetComponent<CampaignCinematicView>().IsBlackCardVisible&&Music.clip==story.chapterMusic,"校音后首张黑屏已开始People Sing");
        Require(Music.isPlaying&&!Music.loop,"People Sing单次播放");
        yield return Until(()=>story.Phase==CampaignPhase.Cinematic,true);
        Music.time=30;int sample=Music.timeSamples;story.DebugSkip();
        yield return Until(()=>RunState.Chapter==2);
        Require(Music.clip==story.chapterMusic&&Music.timeSamples>=sample,"第一二章连续播放且不归零");
        yield return Stable();
        Music.Stop();yield return null;Call("PlayMusic",story.chapterMusic);
        Require(!Music.isPlaying,"自然结束保留曲目，不因同曲调用重播");
        foreach(int failedChapter in new[]{3,8,9})
        {
            yield return story.DebugChapter(failedChapter,true);CampaignDebug.Value.god=true;
            if(failedChapter==9){var shortRoute=story.cinematics.Find(10,"chapter-entry",RunState.Decision,CampaignEnding.Hope);for(int i=0;i<3;i++)shortRoute.shots[i].seconds=.1f;}
            story.DebugFail();yield return null;
            Require(RunState.Chapter==10&&RunState.Ending==(failedChapter==9?CampaignEnding.Hope:CampaignEnding.Mourning),"失败章节"+failedChapter+"进入正确终章分支");
            Require(failedChapter==9?!Music.isPlaying:Music.clip==story.mourningMusic&&PlaybackRequested,"终章开始音乐符合分支"+failedChapter+" clip="+(Music.clip!=null?Music.clip.name:"none")+" playing="+Music.isPlaying+" pause="+AudioListener.pause+" state="+story.Phase);
            var route=story.cinematics.Find(10,"chapter-entry",RunState.Decision,RunState.Ending);
            Require(route!=null&&route.shots.Length==(failedChapter==9?4:3),"终章分镜按分支过滤"+failedChapter);
            if(failedChapter==9)
            {
                Require(!RunState.HopeMusicStarted,"希望镜头之前未触发Finale");
                story.DebugSkip();yield return Until(()=>story.GetComponent<CampaignCinematicView>().CurrentImage.sprite==route.shots[0].image);

                yield return Until(()=>RunState.HopeMusicStarted);
                Require(Music.clip==story.finaleMusic&&PlaybackRequested,"马里于斯转向门外的镜头开始Finale");
                Music.time=20;story.PlayHopeFinale();Require(Music.time>=19,"重复Signal不会重播Finale");
            }
            else{story.PlayHopeFinale();Require(Music.clip==story.mourningMusic&&!RunState.HopeMusicStarted,"哀悼分支拒绝希望Signal");}
            yield return Stable();
            var save=story.CaptureSavedGame();Require(save.musicTrack==(failedChapter==9?"finale":"mourning"),"存档保存正确音乐身份"+failedChapter);
            float cursor=save.musicTime;yield return story.RestoreSavedGame(JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(save)));
            Require(Music.clip==(failedChapter==9?story.finaleMusic:story.mourningMusic)&&Math.Abs(Music.time-cursor)<2,"读档恢复音乐与进度"+failedChapter);
            Require(RunState.EndingSourceChapter==failedChapter,"读档保留失败来源章节"+failedChapter);
            StartCoroutine(Invoke("FinishCampaign"));yield return Until(()=>story.Phase==CampaignPhase.Finished,true);
            Require(Music.clip==(failedChapter==9?story.finaleMusic:story.mourningMusic),"结尾画面未改变音乐分支"+failedChapter);
            Require(story.cinematics.Find(10,"ending",RunState.Decision,RunState.Ending).shots.Length==(failedChapter==9?3:1),"两条结尾画面正确隔离"+failedChapter);
        }
        yield return story.DebugChapter(9,true);CampaignDebug.Value.god=true;
        StartCoroutine(Invoke("AfterDefense"));yield return Until(()=>RunState.Chapter==10);
        Require(RunState.Ending==CampaignEnding.Hope&&!Music.isPlaying&&!RunState.HopeMusicStarted,"第九章正常结束进入静音希望结局");
        yield return Stable();Require(RunState.HopeMusicStarted&&Music.clip==story.finaleMusic,"跳过希望分镜仍落到一次Finale触发");
        var c=DebugConsole.Current;float cursor2=Music.time;c.SetOpen(true);yield return null;yield return null;
        Require(AudioListener.pause&&Time.timeScale==0,"控制台仍暂停音乐与游戏");c.SetOpen(false);
        Require(!AudioListener.pause&&Time.timeScale==1,"关闭控制台恢复播放状态");
        CampaignTelemetry.Current.Flush();CampaignVerification.Check();
    }
}
#endif



