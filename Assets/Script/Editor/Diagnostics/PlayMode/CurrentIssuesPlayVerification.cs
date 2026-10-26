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
public static class CurrentIssuesPlayVerification
{
    const string Pending="Barricade.CurrentIssues.Pending";
    static int frame=-1;static double changed;static bool stepping;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/CurrentIssuesPlay.request"));
    static CurrentIssuesPlayVerification(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Pending+".active",false)){EditorApplication.isPaused=false;PlayerSettings.runInBackground=SessionState.GetBool(Pending+".background",false);SessionState.EraseBool(Pending+".active");}};}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            string output=File.ReadAllText(Request).Trim();File.Delete(Request);
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty){File.WriteAllText(Path.Combine(output,"原生Play验证.json"),"{\"success\":false,\"error\":\"当前场景存在未保存修改，未切换场景或启动回归。\"}");return;}
            if(scene.name!="Title")UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            SessionState.SetBool(Pending+".active",true);SessionState.SetBool(Pending+".background",PlayerSettings.runInBackground);PlayerSettings.runInBackground=true;
            frame=-1;changed=EditorApplication.timeSinceStartup;stepping=false;SessionState.SetString(Pending,output);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending+".active",false))return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(frame!=Time.frameCount){frame=Time.frameCount;changed=EditorApplication.timeSinceStartup;}
        if(stepping||EditorApplication.timeSinceStartup-changed>1){stepping=true;EditorApplication.isPaused=true;EditorApplication.Step();}
        if(StoryDirector.Current!=null&&!string.IsNullOrEmpty(SessionState.GetString(Pending,"")))
        {string output=SessionState.GetString(Pending,"");SessionState.EraseString(Pending);StoryDirector.Current.gameObject.AddComponent<CurrentIssuesPlayProbe>().Begin(output);}
    }
}

public sealed class CurrentIssuesPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,nativePlayVerified;public int checks,startFrame,endFrame;public string error;public string[] tested;public string coverage="原生资源、实际组件与协程验证；音乐结束、输入边沿和长镜头通过API加速，不代表人工按键或完整音乐试听。";}
    Report report;StoryDirector story;string output;readonly List<string> tested=new List<string>();
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    AudioSource Music=>(AudioSource)typeof(StoryDirector).GetField("audioSource",Private).GetValue(story);
    CampaignCinematicView Film=>story.GetComponent<CampaignCinematicView>();
    CinematicPlaybackState Playback=>(CinematicPlaybackState)typeof(CampaignCinematicView).GetField("playback",Private).GetValue(Film);
    void Call(string name,params object[] args)=>typeof(StoryDirector).GetMethod(name,Private).Invoke(story,args);
    void Require(bool ok,string text){report.checks++;tested.Add(text);if(!ok)throw new Exception(text);}
    public void Begin(string path){output=path;story=StoryDirector.Current;report=new Report{startFrame=Time.frameCount};StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            object next=null;bool more=false;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)next=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();DebugConsole.Current?.SetOpen(false);Time.timeScale=1;AudioListener.pause=false;
        report.endFrame=Time.frameCount;report.nativePlayVerified=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.tested=tested.ToArray();
        File.WriteAllText(Path.Combine(output,"原生Play验证.json"),JsonUtility.ToJson(report,true));EditorApplication.isPlaying=false;
    }
    IEnumerator Until(Func<bool> ready,bool skip=false)
    {for(int i=0;i<600;i++){if(ready())yield break;if(skip)story.DebugSkip();yield return null;}throw new TimeoutException("等待超时 "+story.Phase+"/"+RunState.Chapter);}
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    GameObject Enemy(string kind,float x)
    {
        var go=Instantiate(Resources.Load<GameObject>("enemy/"+kind),new Vector3(x,CapsuleTrack.AttackY,0),Quaternion.identity);
        go.GetComponent<EnemyMove>().waveControlled=true;return go;
    }
    IEnumerator MournMenu()
    {
        yield return Until(()=>RunState.Chapter==10&&Film.Active&&Playback!=null);
        Require(RunState.Ending==CampaignEnding.Mourning&&Music.clip==story.mourningMusic,"结局1选中哀悼音乐");
        Playback.Advance(100);yield return Frames(3);
        Require(Film.Active&&Film.CurrentImage.sprite.name=="CH10_C"&&story.Phase==CampaignPhase.Transition,"结局1图片结束停在最后C图，等待音乐");
        Require(story.GetComponent<CampaignEndingView>()==null||!story.GetComponent<CampaignEndingView>().Visible,"音乐未结束时不弹菜单");
        Call("StopCampaignMusic");yield return Until(()=>story.Phase==CampaignPhase.Finished);
        var view=FindFirstObjectByType<CampaignEndingView>();Require(view!=null&&view.Visible,"音乐结束后出现结局菜单");
        Require(view.background.sprite.name=="CH10_C"&&view.background.material.GetFloat("_BlurPixels")>=7.9f,"菜单保留最后图并完成背景虚化");
        Require(new[]{view.restartChapter,view.returnTitle,view.quit}.All(b=>b.interactable&&b.gameObject.activeInHierarchy),"三个结局按钮可交互");
        Require(string.Join("/",new[]{view.restartChapter,view.returnTitle,view.quit}.Select(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text))=="重新开始本章/返回标题/结束游戏","三个按钮文案准确");
        Require(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("UI/EndingBlur")),"结局虚化着色器编译通过");
    }
    IEnumerator Test()
    {
        yield return null;story.cinematics=Instantiate(story.cinematics);story.cinematics.chapterAnnouncementSeconds=0;
        Require(story.table.chapters.Take(8).All(c=>c.openingMusic!=null),"第1至8章开头配乐引用齐全");
        var art=CampaignActorArt.Current;
        var originalSoldier=Resources.Load<GameObject>("enemy/Soldier").GetComponent<SpriteRenderer>().sprite;
        var originalLeader=Resources.Load<GameObject>("enemy/Leader").GetComponent<SpriteRenderer>().sprite;
        var lists=new[]{art.soldierHit,art.soldierDeath,art.grenadierHit,art.grenadierDeath,art.grenadierWindup};
        for(int index=0;index<lists.Length;index++)
        {
            var reference=index<2?originalSoldier:originalLeader;var rb=CurrentIssuesVerification.Opaque(reference);
            float height=rb.height/reference.pixelsPerUnit,ground=(rb.y-reference.pivot.y)/reference.pixelsPerUnit;
            var first=CurrentIssuesVerification.Opaque(lists[index][0]);
            Require(Mathf.Abs(first.height/lists[index][0].pixelsPerUnit-height)<.001f,"动作组"+index+"切入身高对齐原模型");
            foreach(var sprite in lists[index])
            {
                var bounds=CurrentIssuesVerification.Opaque(sprite);
                Require(Mathf.Abs((bounds.y-sprite.pivot.y)/sprite.pixelsPerUnit-ground)<.001f,"动作组"+index+"帧"+sprite.name+"脚底对齐");
            }
            Require(lists[index].All(s=>Mathf.Abs(s.pixelsPerUnit-lists[index][0].pixelsPerUnit)<.001f),"动作组"+index+"使用固定缩放，不随姿态缩放");
        }
        yield return story.DebugChapter(4,true);CampaignDebug.Value.god=true;yield return Frames(2);
        var a=Enemy("Soldier",-2);var b=Enemy("Leader",0);var c=Enemy("Leader",2);yield return null;
        a.GetComponent<EnemyHP>().TakeDamage(50);b.GetComponent<CampaignReactionVisual>().Play(CampaignReactionVisual.Action.Windup);c.GetComponent<EnemyHP>().TakeDamage(100);yield return Frames(2);
        foreach(var enemy in new[]{a,b,c})Require(enemy.transform.Find("ReactionVisual").localPosition==Vector3.zero,"实际反应图层没有额外重心偏移");
        Require(art.soldierHit.Contains(a.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"实际士兵使用对齐后的受击帧");
        Require(art.grenadierWindup.Contains(b.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"实际掷弹兵使用对齐后的预备帧");
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"敌人动作Play.png"));yield return Frames(2);
        CampaignDebug.Value.god=false;GameManager.WallHP=GameManager.WallMaxHP;
        var bombs=new List<BombHP>();for(int i=0;i<3;i++){var bomb=Instantiate(Resources.Load<GameObject>("arrow/Bomb"),new Vector3(0,40,0),Quaternion.identity).GetComponent<BombHP>();bomb.Configure(12345,i);bomb.GetComponent<Rigidbody2D>().simulated=false;bombs.Add(bomb);}
        bombs[0].Explode(true);Require(GameManager.WallHP==GameManager.WallMaxHP-60,"第4章手榴弹伤害从150降到60");
        bombs[1].Explode(true);bombs[2].Explode(true);Require(GameManager.WallHP==GameManager.WallMaxHP-60&&bombs.All(x=>!x.IsAlive),"三枚同组连锁只扣一次血");CampaignDebug.Value.god=true;
        yield return story.DebugPhase("prep");yield return Frames(3);Time.timeScale=0;
        var tap=FindFirstObjectByType<Tap>();story.Clock.Reset(.5f,.5f);typeof(StoryDirector).GetField("constructionCycle",Private).SetValue(story,-1);story.ConstructionInput(tap);
        string prompt=tap.WordUI.text;var judge=(ConstructionJudgement)typeof(StoryDirector).GetField("construction",Private).GetValue(story);int oldKey=judge.First;
        story.Clock.Advance(.5f);story.ConstructionInput(tap);Require(tap.WordUI.text==prompt&&story.Clock.IsPerfect,"进入Perfect保持旧字母");
        story.Clock.Advance(.499f);story.ConstructionInput(tap);Require(tap.WordUI.text==prompt&&story.Clock.IsPerfect,"Perfect结束前保持旧字母");
        story.Clock.Advance(.002f);story.ConstructionInput(tap);Require(tap.WordUI.text!=prompt&&!story.Clock.IsPerfect,"Perfect结束后才自然换字母");
        Require(judge.Press(oldKey,Time.unscaledTime,false)==ConstructionResult.Grace&&!judge.Resolved&&!story.IsConstructionResting,"换题帧按旧字母被宽限，不休息也不记分");
        Require(judge.Press(judge.First,Time.unscaledTime,false)==ConstructionResult.Ordinary,"宽限不阻挡当前正确字母");
        var grace=new ConstructionJudgement();grace.Reset(1,-1,.12,10);grace.Reset(0,2,.12,11);
        Require(grace.Press(1,11.1,true)==ConstructionResult.Grace&&!grace.Resolved,"旧D到新双键的宽限有效");
        Require(grace.Press(0,11.11,true)==ConstructionResult.None&&grace.Press(2,11.2,true)==ConstructionResult.Perfect,"宽限后仍可正常完成双键Perfect");
        grace.Reset(1,-1,.12,12);Require(grace.Press(0,12.181,true)==ConstructionResult.Wrong,"超过180毫秒仍保留错按惩罚");
        var records=CampaignStateCodec.Capture(grace,o=>"");var restored=new ConstructionJudgement();CampaignStateCodec.Restore(restored,records,s=>null,0,0,100);
        Require(records.Any(f=>f.name=="hasPrompt")&&records.Any(f=>f.name=="promptGraceUntil"),"存档包含旧字母宽限状态");
        var pending=new ConstructionJudgement();pending.Reset(1,-1,.12,10);pending.Reset(0,2,.12,11);
        var pendingRecords=CampaignStateCodec.Capture(pending,o=>"");var loadedJudge=new ConstructionJudgement();CampaignStateCodec.Restore(loadedJudge,pendingRecords,s=>null,0,0,100);
        Require(loadedJudge.Press(1,111.1,true)==ConstructionResult.Grace&&!loadedJudge.Resolved,"读档重定位180毫秒宽限时钟");
        Require(loadedJudge.Press(0,111.11,true)==ConstructionResult.None&&loadedJudge.Press(2,111.2,true)==ConstructionResult.Perfect,"读档后的双键时差仍正确");
        var legacyObject=new GameObject("LegacyInputProbe");var legacy=legacyObject.AddComponent<Tap>();legacy.enabled=false;
        var labelObject=new GameObject("PromptProbe",typeof(RectTransform),typeof(UnityEngine.UI.Text));legacy.WordUI=labelObject.GetComponent<UnityEngine.UI.Text>();
        typeof(Tap).GetField("Key",Private).SetValue(legacy,KeyCode.A);typeof(Tap).GetField("lastKeyIndex",Private).SetValue(legacy,0);legacy.WordUI.text="A";
        typeof(Tap).GetMethod("AcceptOrdinary",Private).Invoke(legacy,new object[]{false});string changed=legacy.WordUI.text;
        Require(changed!="A"&&!Tap.IsResting,"旧独立场景按对普通输入后仍立即换字，不休息");
        typeof(Tap).GetMethod("AcceptPerfect",Private).Invoke(legacy,new object[]{false});Require(legacy.WordUI.text!=changed,"旧独立场景Perfect后仍立即换字");
        Destroy(legacyObject);Destroy(labelObject);
        story.DebugHud(false);yield return null;var hud=FindFirstObjectByType<CampaignGameplayHud>();Require(!hud.view.hud.enabled&&!hud.heading.enabled,"隐藏HUD不重开旧文字");
        story.DebugHud(true);yield return null;Require(!hud.view.hud.enabled&&hud.heading.enabled&&hud.controls.enabled,"恢复HUD只显示分栏文字");Time.timeScale=1;
        for(int chapter=3;chapter<=8;chapter++)
        {
            RunState.Chapter=chapter;Call("PlayChapterMusic",chapter);Music.time=20;Call("PlayChapterMusic",chapter);
            Require(Music.clip==story.table.chapters[chapter-1].openingMusic&&Music.time<1&&!Music.loop,"第"+chapter+"章章首重新起曲且不循环");
        }
        RunState.Chapter=1;Call("PlayChapterMusic",1);Music.time=20;RunState.Chapter=2;Call("PlayChapterMusic",2);Require(Music.time>=19,"第1、2章共享配乐不中断");
        RunState.Chapter=9;Call("PlayChapterMusic",9);Require(!Music.isPlaying,"第9章保留静音规则");
        var authored=new[]{4d,4d,4d,4d,4d,4d,4d};var planned=EndingSequenceTiming.Durations(authored,3,story.finaleMusic.length,story.cinematics.creditsSeconds);
        Require(Math.Abs(planned.Skip(3).Sum()+story.cinematics.creditsSeconds-story.finaleMusic.length)<.01,"希望音乐后四张图加制作人员表贴合录音时长");
        Require(planned.Take(3).SequenceEqual(authored.Take(3))&&planned.Skip(3).All(v=>v>30),"只延长音乐响起后的镜头");
        yield return story.DebugChapter(4,true);yield return Frames(2);RunState.ChapterStartHP=320;RunState.ChapterStartAmmoBonus=1;
        int loads=0;UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,UnityEngine.SceneManagement.LoadSceneMode> loaded=(s,m)=>loads++;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded+=loaded;
        story.DebugFail();yield return MournMenu();Require(loads==0,"结局1期间没有加载普通战斗或尾声场景");
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"结局1菜单Play.png"));yield return Frames(2);
        FindFirstObjectByType<CampaignEndingView>().restartChapter.onClick.Invoke();yield return Until(()=>RunState.Chapter==4&&story.Phase==CampaignPhase.Preparation,true);yield return Frames(2);
        Require(GameManager.WallHP==320&&RunState.AmmoBonus==1&&RunState.Ending==CampaignEnding.None,"重新开始本章恢复失败来源章节及开章检查点");
        story.DebugFail();yield return MournMenu();FindFirstObjectByType<CampaignEndingView>().returnTitle.onClick.Invoke();yield return Until(()=>story.Phase==CampaignPhase.Title);
        Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Title","结局1返回标题按钮有效");
        yield return story.DebugChapter(9,true);yield return Frames(2);story.DebugFail();
        yield return Until(()=>RunState.Chapter==10&&Film.Active&&Playback!=null);Require(RunState.Ending==CampaignEnding.Hope&&!RunState.HopeMusicStarted&&!Music.isPlaying,"结局2音乐触发前保持静音");
        Playback.Advance(12.01);yield return Frames(2);Require(Film.CurrentImage.sprite.name=="CH10_D"&&RunState.HopeMusicStarted&&Music.clip==story.finaleMusic,"希望转折D图触发Finale一次");
        Music.time=20;story.PlayHopeFinale();Require(Music.time>=19,"重复希望Signal不重置音乐");
        var actual=(double[])typeof(CinematicPlaybackState).GetField("durations",Private).GetValue(Playback);Require(Math.Abs(actual.Skip(3).Sum()+story.cinematics.creditsSeconds-story.finaleMusic.length)<.01,"实际播放协程采用延长后的时长");
        Playback.Advance(1000);yield return Frames(3);Require(Film.CurrentImage.sprite.name=="CH10_G"&&Film.Active,"结局2保留最终G图等待制作人员段");
        Music.time=Music.clip.length-story.cinematics.creditsSeconds+.1f;yield return Until(()=>Film.IsBlackCardVisible);
        Require(Film.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text.Contains("制作人员")),"黑屏呈现制作人员表");
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"结局2制作人员Play.png"));yield return Frames(2);
        Call("StopCampaignMusic");Film.DebugSkip();yield return Until(()=>story.Phase==CampaignPhase.Title);
        Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Title"&&!Music.isPlaying,"结局2音乐与制作人员结束自动返回标题");
        UnityEngine.SceneManagement.SceneManager.sceneLoaded-=loaded;CampaignVerification.Check();WaveVerification.Check();
    }
}
#endif
