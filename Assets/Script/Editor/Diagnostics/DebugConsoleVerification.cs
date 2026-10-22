#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// This bridge uses the original verification menu implementations. No private reflection.
[InitializeOnLoad]
public static class DebugConsoleVerification
{
    const string Pending="Barricade.ConsoleVerification.Pending";
    static double statusAt;
    static double frameChangedAt;
    static int observedFrame=-1;
    static bool forcedSteps;
    static string RequestPath=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/DebugConsole.request"));
    static DebugConsoleVerification(){DebugConsole.EditorVerify=VerifyExisting;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=PlayChanged;}
    static void VerifyExisting(bool wave){if(wave)WaveVerification.Check();else CampaignVerification.Check();}
    [MenuItem("街垒/测试/检查调试控制台（Play模式）")]
    public static void Check()=>Begin(Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/DebugConsole")));
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string stop=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/DebugConsoleStop.request"));
        if(File.Exists(stop)){File.Delete(stop);EditorApplication.isPlaying=false;return;}
        if(EditorApplication.isPlaying&&EditorApplication.timeSinceStartup>=statusAt)
        {
            statusAt=EditorApplication.timeSinceStartup+1;
            var c=DebugConsole.Current;var s=StoryDirector.Current;
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/DebugConsoleStatus.txt")),"frame="+Time.frameCount+" paused="+EditorApplication.isPaused+" background="+Application.runInBackground+" console="+(c!=null)+" enabled="+(c!=null&&c.isActiveAndEnabled)+" busy="+(c!=null&&c.Busy)+" story="+(s!=null?s.Phase.ToString():"none")+" probe="+(UnityEngine.Object.FindFirstObjectByType<ConsolePlayProbe>()!=null));
        }
        string refresh=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/DebugConsoleRefresh.request"));
        if(File.Exists(refresh)&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete(refresh);AssetDatabase.Refresh();return;}
        if(File.Exists(RequestPath)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {string path=File.ReadAllText(RequestPath).Trim();File.Delete(RequestPath);Begin(path);}
        if(EditorApplication.isPlaying&&(!string.IsNullOrEmpty(SessionState.GetString(Pending,""))||UnityEngine.Object.FindFirstObjectByType<ConsolePlayProbe>()!=null))
        {
            EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            if(Time.frameCount!=observedFrame){observedFrame=Time.frameCount;frameChangedAt=EditorApplication.timeSinceStartup;}
            if(forcedSteps||EditorApplication.timeSinceStartup-frameChangedAt>1)
            {forcedSteps=true;EditorApplication.isPaused=true;EditorApplication.Step();}
        }
        if(EditorApplication.isPlaying&&!string.IsNullOrEmpty(SessionState.GetString(Pending,""))&&DebugConsole.Current!=null)
        {
            string path=SessionState.GetString(Pending,"");SessionState.EraseString(Pending);
            var probe=DebugConsole.Current.gameObject.AddComponent<ConsolePlayProbe>();probe.Begin(path);
        }
    }
    static void Begin(string path)
    {
        Directory.CreateDirectory(path);
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出Play，再运行此验证。");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.isDirty)throw new InvalidOperationException("请先保存当前场景；验证不会替你覆盖未保存编辑。");
        if(scene.name!="Title")throw new InvalidOperationException("请先打开Title场景；验证不替换正在编辑的场景。");
        SessionState.SetBool(Pending+".background",PlayerSettings.runInBackground);SessionState.SetBool(Pending+".restoreBackground",true);
        PlayerSettings.runInBackground=true;EditorApplication.isPaused=false;
        observedFrame=-1;frameChangedAt=EditorApplication.timeSinceStartup;forcedSteps=false;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        var game=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().Name=="GameView");if(game!=null){game.Show();game.Focus();}
        SessionState.SetString(Pending,path);EditorApplication.isPlaying=true;
    }
    static void PlayChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.isPaused=false;forcedSteps=false;
            DebugConsole.EditorVerify=VerifyExisting;
            if(SessionState.GetBool(Pending+".restoreBackground",false)){PlayerSettings.runInBackground=SessionState.GetBool(Pending+".background",false);SessionState.EraseBool(Pending+".restoreBackground");}
        }
    }
}

public sealed class ConsolePlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report{public bool success,nativePlayVerified;public bool keyboardEndToEndVerified=false;public int startFrame,endFrame;public int checks;public string error,output;public string[] tested;}
    readonly List<string> tested=new List<string>();Report report;string directory;float deadline;
    void Require(bool ok,string why){report.checks++;if(!ok)throw new InvalidOperationException(why);}
    public void Begin(string path){directory=path;Application.runInBackground=true;report=new Report{startFrame=Time.frameCount};deadline=Time.realtimeSinceStartup+120;StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            bool more=false;object next=null;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)next=top.Current;else{(top as IDisposable)?.Dispose();stack.Pop();}}
            catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}
            if(!more)continue;if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();
        DebugConsole.Current?.SetOpen(false);Time.timeScale=1;AudioListener.pause=false;report.endFrame=Time.frameCount;report.success=string.IsNullOrEmpty(report.error);report.nativePlayVerified=true;report.tested=tested.ToArray();report.output=DebugConsole.Current!=null?DebugConsole.Current.PlainOutput:"";
        File.WriteAllText(Path.Combine(directory,"原生Play验证.json"),JsonUtility.ToJson(report,true));
        Debug.Log("[ConsolePlayProbe] "+(report.success?"通过 "+report.checks+"项":"失败："+report.error));EditorApplication.isPlaying=false;
    }
    IEnumerator Wait()
    {
        yield return null;
        while(DebugConsole.Current.Busy||StoryDirector.Current.DebugBusy)
        {if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("控制台测试超时："+DebugConsole.Current.PlainOutput);yield return null;}
        yield return null;
    }
    IEnumerator Command(string text,bool allowError=false)
    {tested.Add(text);string previous=DebugConsole.Current.LastError;Require(DebugConsole.Current.Execute(text)=="queued","命令未入队："+text);yield return Wait();if(!allowError)Require(DebugConsole.Current.LastError==previous,"命令失败："+text+" / "+DebugConsole.Current.LastError);}
    IEnumerator Test()
    {
        yield return null;var console=DebugConsole.Current;var story=StoryDirector.Current;
        Require(console!=null&&story!=null,"Title未初始化控制台。");
        string design=CampaignTelemetry.Fingerprint(story.table);
        Time.timeScale=.5f;AudioListener.pause=false;
        var timelineObject=new GameObject("ConsolePauseTimelineProbe");
        var asset=ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();asset.durationMode=UnityEngine.Timeline.TimelineAsset.DurationMode.FixedLength;asset.fixedDuration=10;
        var timeline=timelineObject.AddComponent<UnityEngine.Playables.PlayableDirector>();timeline.playableAsset=asset;timeline.timeUpdateMode=UnityEngine.Playables.DirectorUpdateMode.UnscaledGameTime;timeline.Play();
        console.SetOpen(true);yield return null;yield return null;
        Require(Time.timeScale==0&&AudioListener.pause,"打开控制台未暂停模拟与声音。");
        Require(timeline.state==UnityEngine.Playables.PlayState.Paused,"未暂停Unscaled Timeline。");
        double timelineTime=timeline.time;yield return null;yield return null;Require(Math.Abs(timeline.time-timelineTime)<.000001,"控制台中Timeline仍推进："+timelineTime+"→"+timeline.time);
        console.SetOpen(false);Require(Time.timeScale==.5f&&!AudioListener.pause,"关闭未恢复原倍率/声音。");
        Require(timeline.state==UnityEngine.Playables.PlayState.Playing,"关闭未恢复原有Timeline。");
        timeline.Stop();Destroy(timelineObject);Destroy(asset);
        console.SetOpen(true);yield return null;yield return null;
        yield return Command("time 0.75");Require(Time.timeScale==0,"time命令意外解除控制台暂停。");
        console.SetOpen(false);Require(Time.timeScale==.75f,"time命令未更新关闭后的倍率。");
        console.SetOpen(true);yield return null;
        Require(console.IsOpen&&DebugConsole.CapturingInput,"打开未捕获游戏输入。");
        var canvas=console.transform.Find("DebugConsoleCanvas").GetComponent<Canvas>();Require(canvas.sortingOrder>900,"控制台被浮层遮住。");
        var fields=console.GetComponentsInChildren<UnityEngine.UI.InputField>(true);Require(fields.Any(f=>f.name=="CommandInput"),"缺少输入框。");
        var scroll=console.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);Require(scroll!=null&&scroll.content!=null&&scroll.viewport!=null,"滚动区域引用不完整。");
        yield return Command("ch 9 hard; hp max 600; hp set 600; god; infinite; ammo set 2; assert hp == 600; assert ammo == 2");
        Require(story.Phase==CampaignPhase.Defense&&RunState.Chapter==9,"hard未进入第9章防守。");Require(GameManager.WallHP==600,"批量HP未按跳章后执行。");
        int frozenBeat=story.Clock.Completed;float frozenElapsed=story.Clock.Elapsed;yield return null;yield return null;yield return null;
        Require(story.Clock.Completed==frozenBeat&&story.Clock.Elapsed==frozenElapsed,"控制台内节拍仍推进。");
        Require(CampaignDebug.Value.god&&CampaignDebug.Value.nonDesign,"无敌或非设计标记缺失。");int hp=GameManager.WallHP;GameManager.DamageWallAmount(999);Require(GameManager.WallHP==hp,"god仍掉血。");
        yield return Command("cap 16; density 1.5; tempo now 0.2; window 0.1; aim up; lane 3; crit; mult 0.8");
        Require(Math.Abs(story.Clock.Wait-.2f)<.001&&Math.Abs(story.Clock.Window-.1f)<.001,"节拍覆盖未生效。");Require(CapsuleTrack.AttackIndex==2&&TapSec.ShootStatu=="Up","切轨/瞄准未生效。");
        Require(CampaignDebug.Value.crit&&RunState.ReloadMultiplier==.8f,"crit/mult未生效。");
        yield return Command("time 0; beat; wave; fps; state Decision; set PlayerName \"测试 主角\"; assert PlayerName == \"测试 主角\"");
        Require(Time.timeScale==0&&RunState.PlayerName=="测试 主角","time0或带空格参数失败。");
        yield return Command("step 3");Require(Time.timeScale==0,"逐帧未恢复暂停。");
        yield return Command("kill; spawn soldier 1 2; spawn squad 2 at 100; spawn shell 3 at 20");
        Require(FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).Count(x=>x.HP>0)>=5,"手动士兵/三人小队未生成。");
        Require(FindFirstObjectByType<CannonShell>()!=null,"炮弹未生成。");
        yield return Command("spawn escort 3");Require(CampaignActor.ActiveEscort!=null,"护送对象未生成。");
        yield return Command("hud off; heartbeat off; debug wave on; debug beat on");Require(CampaignDebug.Value.hideHud&&CampaignDebug.Value.muteHeartbeat,"HUD/心跳开关失败。");
        string saveName="console_probe_"+Guid.NewGuid().ToString("N").Substring(0,8);
        yield return Command("save "+saveName);yield return Command("hp set 400");
        console.Execute("load "+saveName+"; assert hp == 600");yield return null;yield return null;yield return null;
        Require(console.Busy&&GameManager.WallHP==400,"load未等待二次确认。");
        console.Execute("confirm");yield return Wait();Require(GameManager.WallHP==600&&CampaignDebug.Value.cap==16,"读档未恢复HP/覆盖。");
        yield return Command("verify; verify wave");Require(File.Exists(Path.Combine(DebugConsole.RootPath,"WaveValidation.json")),"未生成波次报告。");
        console.Execute("clear");yield return null;yield return null;Require(console.Busy,"clear缺二次确认。");console.Execute("cancel");yield return Wait();
        Require(FindFirstObjectByType<SBullet>()!=null,"cancel误清场。");
        Directory.CreateDirectory(DebugConsole.ScriptPath);string script="probe_"+saveName+".txt";File.WriteAllText(Path.Combine(DebugConsole.ScriptPath,script),"# batch\nhp set 555\nassert hp == 555; ammo set 1\n");
        yield return Command("exec "+script);Require(GameManager.WallHP==555,"exec批量未完成。");
        yield return Command("phase results");Require(story.Phase==CampaignPhase.Results,"results阶段错误。");
        yield return Command("ch 7 hard; phase choice; set Decision Released");Require(story.Phase==CampaignPhase.Choice&&RunState.Decision==JavertDecision.Released,"选择阶段或枚举赋值错误。");
        yield return Command("ch 2 hard",true);Require(console.LastError!=null&&console.LastError.Contains("没有防守"),"第二章错误允许hard。");
        console.Execute("ch 2");
        for(int i=0;i<60&&(i<2||console.Busy);i++)
        {yield return null;if(i%4==2)console.Execute("skip");}
        yield return Wait();Require(story.Phase==CampaignPhase.Preparation&&RunState.Chapter==2,"正常开章/黑屏/图片skip未到第2章建造。");
        yield return Command("help hp; hud on; heartbeat on; debug beat off");
        Require(CampaignTelemetry.Fingerprint(story.table)==design,"设计资产被调试命令改写。");
        console.SetOpen(false);yield return null;yield return null;Require(!DebugConsole.CapturingInput,"关闭后仍吞输入。");
        Require(Time.timeScale==0&&!AudioListener.pause,"关闭未保留time 0手动暂停状态。");
        AudioListener.pause=true;console.SetOpen(true);yield return null;console.SetOpen(false);
        Require(Time.timeScale==0&&AudioListener.pause,"原有暂停状态被关闭控制台错误解除。");
        AudioListener.pause=false;console.SetOpen(true);yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"控制台Play截图.png"));yield return null;yield return null;
        File.Delete(Path.Combine(DebugConsole.ScriptPath,script));File.Delete(Path.Combine(DebugConsole.RootPath,"Saves",saveName+".sav"));
        Require(CampaignTelemetry.Current!=null,"统计器丢失。");CampaignTelemetry.Current.Flush();
    }
}
#endif
