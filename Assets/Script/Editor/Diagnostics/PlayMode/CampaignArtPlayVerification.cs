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
public static class CampaignArtPlayVerification
{
    const string Pending="Barricade.ArtProbe.Pending";
    static int frame=-1;static double changed;static bool stepping;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/CampaignArtPlay.request"));
    static CampaignArtPlayVerification(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Pending+".active",false)){EditorApplication.isPaused=false;PlayerSettings.runInBackground=SessionState.GetBool(Pending+".background",false);SessionState.EraseBool(Pending+".active");}};}
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
        {string output=SessionState.GetString(Pending,"");SessionState.EraseString(Pending);StoryDirector.Current.gameObject.AddComponent<CampaignArtPlayProbe>().Begin(output);}
    }
}

public sealed class CampaignArtPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,nativePlayVerified;public int checks,startFrame,endFrame;public string error;public string[] tested;}
    Report report;StoryDirector story;string output;readonly List<string> tested=new List<string>();
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
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
    {for(int i=0;i<500;i++){if(ready())yield break;if(skip)story.DebugSkip();yield return null;}throw new TimeoutException("等待超时 "+story.Phase+"/"+RunState.Chapter);}
    IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
    GameObject Enemy(bool grenadier=false)
    {
        var go=Instantiate(Resources.Load<GameObject>(grenadier?"enemy/Leader":"enemy/Soldier"),new Vector3(-1,CapsuleTrack.AttackY,0),Quaternion.identity);
        go.GetComponent<EnemyMove>().waveControlled=true;return go;
    }
    IEnumerator Test()
    {
        yield return null;var art=CampaignActorArt.Current;Require(art!=null,"动作资源库已加载");
        Require(new[]{art.gavrocheRun,art.gavrochePick,art.soldierHit,art.soldierDeath,art.grenadierHit,art.grenadierDeath,art.grenadierWindup}.All(f=>f.Length==12&&f.All(s=>s!=null)),"七套动作各12帧引用完整");
        Require(story.table.chapters.Sum(c=>c.TotalBeats)==CampaignRevisionRules.ChapterBeatBudget,"战役总拍数符合当前预算");
        foreach(int chapter in new[]{3,4,5,9})
        {
            yield return story.DebugChapter(chapter,true);CampaignDebug.Value.god=true;yield return Frames(2);var gun=FindFirstObjectByType<SBullet>();
            Require(gun.AmmoSystemEnabled==(chapter>=5),"第"+chapter+"章弹药开关正确");
            gun.DebugAmmo(0);Require(chapter>=5?!gun.CanFire:gun.CanFire,"第"+chapter+"章空弹药行为正确");
            var hud=FindFirstObjectByType<AmmoHudView>();Require(hud!=null&&hud.counter.gameObject.activeSelf==(chapter>=5),"第"+chapter+"章弹药HUD正确");
            Require(hud.heartFill.gameObject.activeInHierarchy,"隐藏弹药时保留心脏");
            if(chapter==3||chapter==4)
            {
                gun.Fire("Down",false);Require(gun.CampaignAmmo==gun.CampaignCapacity&&!gun.IsReloading,"教学章射击不消耗弹药");
                Require(!gun.CanFire,"无限弹药仍限制同拍连射");
            }
            if(chapter==5)
            {
                Require(!story.Chapter.waveA.events.Any(e=>e.type==WaveEventType.Decoy)&&!story.Chapter.waveB.events.Any(e=>e.type==WaveEventType.Decoy),"第五章不再生成可射杀的沙威诱饵");
                gun.BeginCampaignReload();Require(gun.IsReloading,"第五章可开始有限弹药补充");story.Clock.DebugComplete(3);yield return null;
                Require(gun.CampaignAmmo==gun.CampaignCapacity&&!gun.IsReloading,"补弹完成恢复携弹量");
            }
        }
        yield return story.DebugChapter(4,true);CampaignDebug.Value.god=true;
        var child=Instantiate(Resources.Load<GameObject>("CampaignActors/Gavroche"),new Vector3(7,WaveDirector.Current.LaneY(2),0),Quaternion.identity);
        var actor=child.GetComponent<CampaignActor>();actor.lane=1;actor.ConfigureWave(WaveDirector.Current,(float)WaveDirector.Current.Position,20);yield return Frames(2);
        var childVisual=child.GetComponent<GavrocheVisual>();Require(child.GetComponent<SpriteRenderer>().color==Color.white,"孩子使用原色模型而非剪影");
        Require(art.gavrocheRun.Contains(child.GetComponent<SpriteRenderer>().sprite),"孩子出发播放奔跑动作");
        story.Clock.DebugComplete(10);yield return Frames(3);Require(childVisual.IsPicking&&art.gavrochePick.Contains(child.GetComponent<SpriteRenderer>().sprite),"孩子停下播放捡拾动作");
        int successes=RunState.EscortSuccesses;RunState.ChapterStartEscortSuccesses=successes;RunState.ChapterStartEscortFailures=RunState.EscortFailures;RunState.AmmoBonus=0;
        RunState.RegisterEscortResult(true);RunState.RegisterEscortResult(true);Require(RunState.AmmoBonus==0,"两段救援不提前奖励容量");RunState.RegisterEscortResult(true);Require(RunState.AmmoBonus==1,"完整三段救援保留一发容量奖励");
        yield return story.DebugChapter(5,true);CampaignDebug.Value.god=true;
        var soldier=Enemy();yield return null;var soldierHP=soldier.GetComponent<EnemyHP>();soldierHP.TakeDamage(50);yield return Frames(2);
        Require(soldierHP.HP==50&&soldier.GetComponent<CampaignReactionVisual>().IsPlaying,"士兵非致命中弹播放反应且保持存活");
        Require(art.soldierHit.Contains(soldier.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"士兵实际采样新中弹帧");
        var save=story.CaptureSavedGame();Require(save.world.components.Any(c=>c.type==typeof(CampaignReactionVisual).FullName),"存档包含受击状态");
        yield return story.RestoreSavedGame(JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(save)));yield return Frames(2);
        soldierHP=FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).First(h=>h.HP==50);Require(soldierHP.GetComponent<CampaignReactionVisual>().IsPlaying,"读档恢复进行中的受击动作");
        soldier=soldierHP.gameObject;soldierHP.TakeDamage(50);yield return null;
        Require(soldier!=null&&soldierHP.HP==0&&soldier.GetComponent<CampaignReactionVisual>().IsDying,"致命中弹保留对象播放死亡动作");
        Require(soldier.GetComponentsInChildren<Collider2D>().All(c=>!c.enabled)&&!soldier.GetComponent<Rigidbody2D>().simulated,"死亡立即停用碰撞和运动");
        Require(art.soldierDeath.Contains(soldier.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"士兵实际采样新死亡帧");
        yield return Until(()=>soldier==null);Require(true,"死亡播放一次、停留并清理对象");
        var grenadier=Enemy(true);var other=Enemy(true);other.transform.position+=Vector3.left*2.5f;yield return Frames(2);grenadier.GetComponent<EnemyMove>().BeginWaveThrow();yield return Frames(2);
        Require(art.grenadierWindup.Contains(grenadier.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"掷弹兵接入投弹预备帧");
        grenadier.GetComponent<EnemyHP>().TakeDamage(50);yield return null;Require(art.grenadierHit.Contains(grenadier.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"掷弹兵接入中弹帧");
        grenadier.GetComponent<EnemyHP>().TakeDamage(50);yield return null;Require(grenadier.GetComponent<CampaignReactionVisual>().IsDying&&other.GetComponent<EnemyHP>().HP==100,"掷弹兵独立死亡，不共享生命");
        Require(art.grenadierDeath.Contains(grenadier.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"掷弹兵接入倒地帧");
        yield return Frames(40);Require(grenadier!=null&&art.grenadierDeath.Skip(6).Contains(grenadier.transform.Find("ReactionVisual").GetComponent<SpriteRenderer>().sprite),"倒地动作确实推进至后半段");
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"受击与倒地Play截图.png"));yield return Frames(2);
        yield return story.DebugChapter(7,true);CampaignDebug.Value.god=true;GameManager.WallHP=300;
        Require(story.Chapter.defenseBeats==135&&story.Chapter.choiceBeats==0,"第七章135拍防守、无抉择");
        story.Clock.DebugComplete(131);yield return Frames(2);Require(WaveDirector.Current.FinaleStarted,"第七章进入末段齐射");story.Clock.DebugComplete(135);
        yield return Until(()=>story.Phase==CampaignPhase.Results,true);
        Require(RunState.Decision==JavertDecision.Released&&GameManager.WallHP==300,"第七章固定释放沙威且不回血");
        yield return story.DebugChapter(8,true);CampaignDebug.Value.god=false;GameManager.WallHP=GameManager.WallMaxHP;
        Require(story.Chapter.waveA.requiredShellInterceptions==0&&story.Chapter.waveB.requiredShellInterceptions==0,"第八章零拦截门槛");
        Require(story.Chapter.waveA.events.Count(e=>e.type==WaveEventType.Shell)==2&&story.Chapter.waveB.events.Count(e=>e.type==WaveEventType.Shell)==2,"第八章两次炮击");
        typeof(WaveDirector).GetMethod("EnsureCannon",Private).Invoke(WaveDirector.Current,new object[]{false});yield return Frames(2);var cannon=FindFirstObjectByType<Cannon>();
        Require(cannon!=null,"火炮部署正常");
        for(int i=0;i<2;i++){var shell=Instantiate(cannon.shellPrefab,new Vector3(-3,0,0),Quaternion.identity);shell.Launch(Vector2.right,1,.25f,0);shell.ResolveWaveImpact();}
        Require(GameManager.WallHP==GameManager.WallMaxHP/2&&WaveDirector.Current.ShellInterceptions==0,"两发炮弹均未拦截仍保留半数生命");
        CampaignDebug.Value.god=true;
        story.Clock.DebugComplete(110);yield return Frames(2);Require(cannon.IsRetreating,"火炮第110拍停止攻击并撤离");
        yield return Until(()=>cannon==null);Require(true,"火炮向左画外离场");
        story.Clock.DebugComplete(167);yield return Frames(2);Require(WaveDirector.Current.FinaleStarted&&RunState.Chapter==8,"零拦截仍进入正常章节收尾");story.Clock.DebugComplete(171);
        yield return Until(()=>story.Phase==CampaignPhase.EndCard);story.Clock.DebugComplete(2);yield return Until(()=>story.Phase==CampaignPhase.Results,true);
        Require(RunState.Chapter==8,"零拦截没有被强制送入失败终章");CampaignVerification.Check();WaveVerification.Check();
    }
}
#endif



