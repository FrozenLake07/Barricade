#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CombatV3Verification
{
    [Serializable] sealed class Request{public string outputDirectory="";}
    [Serializable] sealed class Row{public int chapter,before,after,cap;public string variant;}
    [Serializable] sealed class Report{public bool success,assetsSaved,nativeEditorVerified;public bool fullPlayVerified=false;public int checks;public string error;public Row[] waves;}
    static string RequestPath=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/CombatV3.request"));
    [InitializeOnLoadMethod] static void Listen(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(RequestPath)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
        string json=File.ReadAllText(RequestPath);File.Delete(RequestPath);
        var request=JsonUtility.FromJson<Request>(json);Run(request.outputDirectory,true);
    }
    [MenuItem("街垒/测试/检查战斗密度、画外入场与分镜")]
    public static void Verify()=>Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/CombatV3")),false);
    static int Units(WaveScript wave)=>wave.events.Where(e=>e.type==WaveEventType.Soldier||e.type==WaveEventType.Squad)
        .Sum(e=>e.type==WaveEventType.Squad?3*e.count:e.lanes.Length>0?e.lanes.Length:e.count);
    static void Run(string directory,bool apply)
    {
        var report=new Report();var clones=new List<WaveScript>();
        Action<bool,string> require=(ok,why)=>{report.checks++;if(!ok)throw new InvalidOperationException(why);};
        try
        {
            Directory.CreateDirectory(directory);
            var waves=AssetDatabase.FindAssets("t:WaveScript",new[]{"Assets/Resources/Campaign/Waves"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<WaveScript>(AssetDatabase.GUIDToAssetPath(g))).Where(w=>w.chapter>=3&&w.chapter<=9).OrderBy(w=>w.chapter).ThenBy(w=>w.variant).ToArray();
            require(waves.Length==14,"需要14份A/B波次。");var rows=new List<Row>();
            foreach(var source in waves)
            {
                var draft=UnityEngine.Object.Instantiate(source);draft.name=source.name;clones.Add(draft);
                if(apply)CombatPressureRules.Apply(draft);draft.Validate();
                require(draft.events.Where(e=>e.advance>0).All(e=>draft.chapter==3&&e.beat==0&&e.tag=="opening"),"存在画内刷怪。");
                rows.Add(new Row{chapter=source.chapter,variant=source.variant,before=Units(source),after=Units(draft),cap=draft.maxOnScreen});
            }
            if(apply)
            {
                Undo.RecordObjects(waves,"战斗密度与画外入场");
                for(int i=0;i<waves.Length;i++){EditorUtility.CopySerialized(clones[i],waves[i]);EditorUtility.SetDirty(waves[i]);AssetDatabase.SaveAssetIfDirty(waves[i]);}
                var table=Resources.Load<ChapterTable>("Campaign/ChapterTable");Undo.RecordObject(table,"更新战斗测试标签");
                table.playtestLabel="combat-v3-offscreen-volley-20261022";EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);table.Validate();
                report.assetsSaved=true;
            }
            report.waves=rows.ToArray();
            foreach(var pair in waves.GroupBy(w=>w.chapter))
            {
                var a=pair.Single(w=>w.variant=="A");var b=pair.Single(w=>w.variant=="B");require(a.events.Length==b.events.Length,"A/B事件长度不一致。");
                for(int i=0;i<a.events.Length;i++)require(a.events[i].beat==b.events[i].beat&&a.events[i].lane==4-b.events[i].lane,"A/B轨道未镜像。");
            }
            CheckPictures(require);CheckHealth(require);
            foreach(string sceneName in new[]{"1","2"})
            {
                string scenePath="Assets/Scenes/"+sceneName+".unity";
                var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
                try
                {
                    var roots=scene.GetRootGameObjects();int tap=roots.Sum(r=>r.GetComponentsInChildren<Tap>(true).Length),sec=roots.Sum(r=>r.GetComponentsInChildren<TapSec>(true).Length);
                    require(sceneName=="1"?tap==1&&sec==0:tap==0&&sec==1,"场景节拍组件不正确："+sceneName);
                }
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            var watch=new HeartbeatClockWatch();require(!watch.Observe(.85f,.016f,true),"新阶段首帧误报。");
            require(!watch.Observe(.85f,10,false),"暂停误报。");
            report.success=report.nativeEditorVerified=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            foreach(var clone in clones)if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"原生编辑器检查.json"),JsonUtility.ToJson(report,true));
        }
        Debug.Log("[CombatV3] "+(report.success?"资源/分镜/独立HP检查通过，完整Play仍待验收":"检查失败，详见报告"));
    }
    static void CheckPictures(Action<bool,string> require)
    {
        var config=Resources.Load<CampaignCinematics>("Campaign/Cinematics");require(config!=null,"分镜配置丢失。");config.Validate();
        var seen=new HashSet<Sprite>();foreach(var route in config.routes)foreach(var shot in route.shots){require(shot.image!=null,"分镜引用丢失。");seen.Add(shot.image);}
        require(seen.Count==34,"应有34张独立分镜。");
        GameObject screen=null,host=null;IEnumerator card=null,film=null;
        try
        {
            screen=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Campaign/StartScreen.prefab"));screen.hideFlags=HideFlags.HideAndDontSave;
            host=new GameObject("CinematicV3Audit");host.hideFlags=HideFlags.HideAndDontSave;var view=host.AddComponent<CampaignCinematicView>();view.Initialize(screen.GetComponent<StartScreenView>(),null);
            foreach(var route in config.routes.Where(r=>r.key=="chapter-entry"))
            {
                card=view.PlayBlackCard("章首报幕",3,false,64);require(card.MoveNext(),"报幕未启动。");require(view.IsBlackCardVisible,"黑屏页未显示。");((IDisposable)card).Dispose();card=null;
                film=view.Play(config,route,false);require(film.MoveNext(),"分镜未启动。");Canvas.ForceUpdateCanvases();
                var image=view.CurrentImage;require(!view.IsBlackCardVisible&&image!=null&&image.enabled&&image.gameObject.activeInHierarchy,"报幕遮挡或隐藏图片。");
                require(image.sprite==route.shots[0].image&&image.color.a>0&&image.rectTransform.rect.width>0&&image.rectTransform.rect.height>0,"分镜图片、透明度或尺寸无效。");
                ((IDisposable)film).Dispose();film=null;
            }
        }
        finally
        {
            (card as IDisposable)?.Dispose();(film as IDisposable)?.Dispose();
            if(host!=null)UnityEngine.Object.DestroyImmediate(host);if(screen!=null)UnityEngine.Object.DestroyImmediate(screen);
        }
    }
    static void CheckHealth(Action<bool,string> require)
    {
        var members=new List<GameObject>();GameObject controller=null;
        try
        {
            var prefab=Resources.Load<GameObject>("enemy/Leader");
            for(int i=0;i<3;i++){var go=UnityEngine.Object.Instantiate(prefab);go.hideFlags=HideFlags.HideAndDontSave;members.Add(go);}
            var hp=members.Select(m=>m.GetComponent<EnemyHP>()).ToArray();hp[0].HP-=50;
            require(hp[0].HP==50&&hp[1].HP==100&&hp[2].HP==100,"掷弹兵HP联动。");
            controller=new GameObject("IndependentSquadAudit");controller.AddComponent<WaveSquad>();UnityEngine.Object.DestroyImmediate(controller);controller=null;
            require(members.All(m=>m!=null),"小队控制器销毁导致成员消失。");
            UnityEngine.Object.DestroyImmediate(members[0]);require(members[1]!=null&&members[2]!=null&&hp[1].HP==100&&hp[2].HP==100,"单人死亡影响其他成员。");
        }
        finally{if(controller!=null)UnityEngine.Object.DestroyImmediate(controller);foreach(var go in members)if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
    }
}
#endif
