#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TavernExitSetup
{
    const string Root="Assets/Cinematics/TavernUI";
    const string TimelinePath=Root+"/Timelines/TavernIntroduction.playable";
    const string Stage=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernExitSetup.request"));
    [Serializable] sealed class Report {public bool success,scenePreserved;public string error;public int checks;public string[] passed;}
    static readonly List<string> passed=new List<string>();
    static TavernExitSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);passed.Add(reason);}
    static string Hash(string path){using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(path)));}
    static AnimationTrack Motion(TimelineAsset timeline,string actor)=>timeline.GetRootTracks().OfType<GroupTrack>().Single(t=>t.name.EndsWith("_"+actor,StringComparison.Ordinal)).GetChildTracks().OfType<AnimationTrack>().Single(t=>t.name.StartsWith("动作",StringComparison.Ordinal));
    static AnimationClip Pose(string actor)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Intro/Intro_"+actor+"_Position.anim");
    static void MoveExit(string actor,float from,float offset)
    {
        var pose=Pose(actor);
        foreach(var binding in AnimationUtility.GetCurveBindings(pose))
        {
            var curve=AnimationUtility.GetEditorCurve(pose,binding);var keys=curve.keys;
            for(int i=0;i<keys.Length;i++)if(keys[i].time>=from-.001f&&keys[i].time<20.49f)keys[i].time+=offset;
            // Hero only turns to leave when he actually begins following Enjolras.
            if(actor=="Hero"&&binding.propertyName=="m_LocalScale.x")for(int i=0;i<keys.Length;i++)if(Math.Abs(keys[i].time-11)<.001f)keys[i].time=12.1f;
            curve.keys=keys;AnimationUtility.SetEditorCurve(pose,binding,curve);
        }
        EditorUtility.SetDirty(pose);
    }
    static void Span(TimelineClip clip,double start,double end){clip.start=start;clip.duration=end-start;}
    static RectTransform Actor(GameObject root,string id)=>(RectTransform)root.GetComponentsInChildren<Animator>(true).Single(a=>a.runtimeAnimatorController!=null&&a.runtimeAnimatorController.name=="UI_"+id&&a.GetComponent<UnityEngine.UI.Image>()!=null).transform.parent;
    static void Verify(TimelineAsset timeline)
    {
        var enj=Motion(timeline,"Enjolras").GetClips().OrderBy(c=>c.start).ToArray();var hero=Motion(timeline,"Hero").GetClips().OrderBy(c=>c.start).ToArray();var gr=Motion(timeline,"Grantaire").GetClips().OrderBy(c=>c.start).ToArray();
        Check(enj.Length==5&&hero.Length==5&&gr.Length==6,"Only Grantaire gains one final drinking clip");
        Check(Math.Abs(enj[3].start-10.1)<.001&&Math.Abs(hero[3].start-12.1)<.001&&Math.Abs(gr[4].start-13.8)<.001,"Departure order is Enjolras, Hero, Grantaire");
        Check(gr[3].animationClip.name=="UI_Grantaire_Drink"&&Math.Abs(gr[3].start-11.7)<.001&&Math.Abs(gr[3].end-13.8)<.001,"Grantaire drinks before his final walk");
        var sip=gr[3].asset as AnimationPlayableAsset;Check(sip!=null&&sip.loop==AnimationPlayableAsset.LoopMode.Off&&Math.Abs(gr[3].duration*gr[3].timeScale-gr[3].animationClip.length)<.002,"Final sip plays exactly one full animation cycle");
        var fourth=timeline.GetOutputTracks().OfType<SignalTrack>().Single().GetMarkers().OfType<SignalEmitter>().Single(m=>m.asset.name=="Tavern_FourthDialogue");
        Check(Math.Abs(fourth.time-9.8)<.001&&enj[3].start>fourth.time,"Departures begin after the final dialogue pause point");
        Check(Math.Abs(timeline.duration-20.5)<.01,"Existing cinematic duration retained");
        GameObject contents=PrefabUtility.LoadPrefabContents(Stage);
        try
        {
            var director=contents.GetComponentInChildren<PlayableDirector>(true);var e=Actor(contents,"Enjolras");var h=Actor(contents,"Hero");var g=Actor(contents,"Grantaire");
            director.timeUpdateMode=DirectorUpdateMode.Manual;director.Play();
            Action<double> sample=t=>{director.time=t;director.Evaluate();Canvas.ForceUpdateCanvases();};
            try
            {
                sample(9.8);Check(Vector2.Distance(e.anchoredPosition,new Vector2(187,-126))<.01&&Vector2.Distance(h.anchoredPosition,new Vector2(245,-126))<.01&&Vector2.Distance(g.anchoredPosition,new Vector2(217,-126))<.01,"All three hold their conversation positions through the last line");
                sample(10.4);Check(e.anchoredPosition.x>187&&Math.Abs(h.anchoredPosition.x-245)<.01&&Math.Abs(g.anchoredPosition.x-217)<.01,"Enjolras moves first while Hero and Grantaire wait");
                sample(12.3);Check(h.anchoredPosition.x>245&&Math.Abs(g.anchoredPosition.x-217)<.01,"Hero follows while Grantaire stays to drink");
                var image=g.GetComponentInChildren<UnityEngine.UI.Image>();var binding=AnimationUtility.GetObjectReferenceCurveBindings(gr[3].animationClip).Single();
                var frames=AnimationUtility.GetObjectReferenceCurve(gr[3].animationClip,binding);double local=(12.3-gr[3].start)*gr[3].timeScale;
                Check(image.sprite==frames.Last(k=>k.time<=local+.001).value,"Timeline samples the final drinking animation at the expected frame");
                sample(13.8);Check(e.GetComponent<TavernActorDepth>().sortingOrder<10,"Enjolras goes behind the doorframe at the moved threshold");
                sample(14.2);Check(e.GetComponent<CanvasGroup>().alpha<.01&&h.GetComponent<CanvasGroup>().alpha>.99&&g.GetComponent<CanvasGroup>().alpha>.99,"Enjolras has exited while the other two remain visible");
                sample(15.4);Check(h.GetComponent<TavernActorDepth>().sortingOrder<10,"Hero doorframe occlusion follows his new exit timing");
                sample(15.9);Check(h.GetComponent<CanvasGroup>().alpha<.01&&g.GetComponent<CanvasGroup>().alpha>.99&&g.anchoredPosition.x>217,"Hero has exited while Grantaire walks last");
                sample(17.1);Check(g.GetComponent<TavernActorDepth>().sortingOrder<10,"Grantaire doorframe occlusion follows his new exit timing");
                sample(17.5);Check(e.GetComponent<CanvasGroup>().alpha<.01&&h.GetComponent<CanvasGroup>().alpha<.01&&g.GetComponent<CanvasGroup>().alpha<.01,"All actors exit before the door closes");
            }
            finally{director.Stop();}
        }
        finally{PrefabUtility.UnloadPrefabContents(contents);}
    }
    static void Run(string output)
    {
        Directory.CreateDirectory(output);passed.Clear();var report=new Report();var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;var roots=scene.GetRootGameObjects();
        var paths=new[]{TimelinePath,Root+"/Animations/Intro/Intro_Enjolras_Position.anim",Root+"/Animations/Intro/Intro_Hero_Position.anim",Root+"/Animations/Intro/Intro_Grantaire_Position.anim"};
        string[] preserve={Stage,Root+"/Data/TavernDialogue.asset",Root+"/Data/NameResponses.asset",Root+"/Animations/UI_Grantaire_Drink.anim"};var hashes=preserve.Select(Hash).ToArray();bool changed=false;
        try
        {
            var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);Check(timeline!=null,"Existing Timeline resolves");
            var gr=Motion(timeline,"Grantaire").GetClips().OrderBy(c=>c.start).ToArray();bool already=gr.Length==6&&gr[3].displayName=="最后喝一口";
            if(!already)
            {
                Check(gr.Length==5,"Expected original Grantaire track layout");Directory.CreateDirectory(Path.Combine(output,"备份"));foreach(var path in paths)File.Copy(path,Path.Combine(output,"备份/"+Path.GetFileName(path)),false);
                changed=true;MoveExit("Enjolras",14.9f,-4.8f);MoveExit("Hero",13f,-.9f);MoveExit("Grantaire",11.2f,2.6f);
                var enj=Motion(timeline,"Enjolras").GetClips().OrderBy(c=>c.start).ToArray();Span(enj[2],9,10.1);Span(enj[3],10.1,14);Span(enj[4],14,20.5);
                var hero=Motion(timeline,"Hero").GetClips().OrderBy(c=>c.start).ToArray();Span(hero[2],4.8,12.1);Span(hero[3],12.1,15.6);Span(hero[4],15.6,20.5);
                Span(gr[1],2.8,4.4);Span(gr[2],4.4,11.7);Span(gr[3],13.8,17.3);Span(gr[4],17.3,20.5);
                var drink=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/UI_Grantaire_Drink.anim");var sip=Motion(timeline,"Grantaire").CreateClip(drink);Span(sip,11.7,13.8);sip.displayName="最后喝一口";sip.timeScale=drink.length/sip.duration;sip.blendInDuration=sip.blendOutDuration=0;((AnimationPlayableAsset)sip.asset).loop=AnimationPlayableAsset.LoopMode.Off;
                var door=timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>t.name=="门扇开合").GetClips().OrderBy(c=>c.start).ToArray();
                Check(door.Length==9,"Existing entry-door animation retained");Span(door[4],2.7,9.9);Span(door[5],9.9,10.4);Span(door[6],10.4,17.8);Span(door[7],17.8,18.3);Span(door[8],18.3,20.5);
                foreach(var clip in new[]{door[5],door[7]})clip.timeScale=clip.animationClip.length/clip.duration;
                EditorUtility.SetDirty(timeline);
            }
            Verify(timeline);
            foreach(string actor in new[]{"Enjolras","Hero","Grantaire"})AssetDatabase.SaveAssetIfDirty(Pose(actor));AssetDatabase.SaveAssetIfDirty(timeline);
            for(int i=0;i<preserve.Length;i++)Check(Hash(preserve[i])==hashes[i],"Preserved asset: "+Path.GetFileName(preserve[i]));
            report.scenePreserved=SceneManager.GetActiveScene()==scene&&scene.isDirty==dirty&&roots.All(g=>g!=null&&g.scene==scene);Check(report.scenePreserved,"Current scene and unsaved edits retained");report.success=true;
        }
        catch(Exception error)
        {
            report.error=error.ToString();Debug.LogException(error);
            if(changed)foreach(string path in paths){string backup=Path.Combine(output,"备份/"+Path.GetFileName(path));if(File.Exists(backup)){File.Copy(backup,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);}}
        }
        finally{report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"离场检查.json"),JsonUtility.ToJson(report,true));}
    }
}
#endif
