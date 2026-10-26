#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TavernDoorPathRepair
{
    const string Root="Assets/Cinematics/TavernUI";
    const string TimelinePath=Root+"/Timelines/TavernIntroduction.playable";
    const string Stage=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    [Serializable] sealed class CurveInfo {public string actor,type,property;public float[] times,values;}
    [Serializable] sealed class ClipInfo {public string track,name,animation;public double start,end,speed;}
    [Serializable] sealed class Data {public string error;public bool applied;public int verifiedFrames;public List<CurveInfo> curves=new List<CurveInfo>();public List<ClipInfo> clips=new List<ClipInfo>();}
    static TavernDoorPathRepair(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernDoorPathRepair.request"));
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        string output=File.ReadAllText(request).Trim();File.Delete(request);Run(output);
    }
    static void ApplyRepair(TimelineAsset timeline,string output)
    {
        string[] ids={"Hero","Grantaire","Enjolras"};
        var clips=ids.Select(id=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Intro/Intro_"+id+"_Position.anim")).ToArray();
        var binding=EditorCurveBinding.FloatCurve("",typeof(TavernActorDepth),"sortingOrder");
        var ys=clips.Select(c=>AnimationUtility.GetEditorCurve(c,EditorCurveBinding.FloatCurve("",typeof(RectTransform),"m_AnchoredPosition.y"))).ToArray();
        var old=clips.Select(c=>AnimationUtility.GetEditorCurve(c,binding)).ToArray();
        string backup=Path.Combine(output,"原资产备份");Directory.CreateDirectory(backup);
        foreach(var asset in clips.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[]{timeline}))
        {
            string path=AssetDatabase.GetAssetPath(asset);string dest=Path.Combine(backup,Path.GetFileName(path));
            if(!File.Exists(dest))File.Copy(Path.GetFullPath(path),dest);
        }
        var times=new SortedSet<float>(Enumerable.Range(0,2051).Select(n=>n/100f));
        foreach(var curve in old)foreach(var key in curve.keys)times.Add(key.time);
        for(int actor=0;actor<3;actor++)
        {
            var keys=new List<Keyframe>();
            foreach(float time in times)
            {
                int value=5;
                if(old[actor].Evaluate(time)>=10)
                {
                    float y=ys[actor].Evaluate(time);int rank=0;
                    for(int other=0;other<3;other++)if(other!=actor)
                    {
                        float otherY=ys[other].Evaluate(time);
                        if(otherY>y || (Mathf.Abs(otherY-y)<.0001f && other<actor))rank++;
                    }
                    value=17+rank*3;
                }
                if(keys.Count==0||keys[keys.Count-1].value!=value||Mathf.Abs(time-20.5f)<.0001f)keys.Add(new Keyframe(time,value));
            }
            var result=new AnimationCurve(keys.ToArray());
            for(int k=0;k<result.length;k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(result,k,AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(result,k,AnimationUtility.TangentMode.Constant);
            }
            AnimationUtility.SetEditorCurve(clips[actor],binding,result);EditorUtility.SetDirty(clips[actor]);
        }
        var doors=timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>t.name=="门扇开合").GetClips().OrderBy(c=>c.start).ToArray();
        if(doors.Length!=9)throw new Exception("Unexpected door clips; refusing to change");
        doors[4].duration=12.7-doors[4].start;
        doors[5].start=12.7;doors[5].duration=.5;
        doors[6].start=13.2;doors[6].duration=17.8-13.2;
        EditorUtility.SetDirty(timeline);
        foreach(var clip in clips)AssetDatabase.SaveAssetIfDirty(clip);AssetDatabase.SaveAssetIfDirty(timeline);
    }
    static void Run(string output)
    {
        bool apply=output.StartsWith("apply|");if(apply)output=output.Substring(6);Directory.CreateDirectory(output);var data=new Data();GameObject stage=null;
        try
        {
            var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if(apply){ApplyRepair(timeline,output);data.applied=true;}
            foreach(var track in timeline.GetOutputTracks().OfType<AnimationTrack>())foreach(var clip in track.GetClips())data.clips.Add(new ClipInfo{track=track.name,name=clip.displayName,animation=AssetDatabase.GetAssetPath(clip.animationClip),start=clip.start,end=clip.end,speed=clip.timeScale});
            foreach(string id in new[]{"Hero","Grantaire","Enjolras"})
            {
                var pose=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Intro/Intro_"+id+"_Position.anim");
                foreach(var binding in AnimationUtility.GetCurveBindings(pose)){var curve=AnimationUtility.GetEditorCurve(pose,binding);data.curves.Add(new CurveInfo{actor=id,type=binding.type.Name,property=binding.propertyName,times=curve.keys.Select(k=>k.time).ToArray(),values=curve.keys.Select(k=>k.value).ToArray()});}
            }
            stage=PrefabUtility.LoadPrefabContents(Stage);var director=stage.GetComponentInChildren<PlayableDirector>(true);director.timeUpdateMode=DirectorUpdateMode.Manual;director.Play();
            if(apply)
            {
                for(int frame=0;frame<=1230;frame++)
                {
                    director.time=frame/60.0;director.Evaluate();
                    var actors=stage.GetComponentsInChildren<TavernActorDepth>(true);
                    foreach(var a in actors)foreach(var b in actors)
                    {
                        int ao=a.GetComponent<Canvas>().sortingOrder,bo=b.GetComponent<Canvas>().sortingOrder;
                        float ay=((RectTransform)a.transform).anchoredPosition.y,by=((RectTransform)b.transform).anchoredPosition.y;
                        if(ao>=10&&bo>=10&&ay<by-.1f&&ao<=bo)throw new Exception("Incorrect actor depth at "+director.time+": "+a.name+" / "+b.name);
                        if(ao>=30)throw new Exception("Actor covers foreground furniture");
                    }
                    data.verifiedFrames++;
                }
            }
            var render=typeof(TavernScene3Import).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(float time in new[]{0f,.7f,1.2f,5f,7.5f,9.8f,10.2f,10.8f,11.5f,12.2f,13f,13.7f,15.7f,17.5f})
            {
                director.time=time;director.Evaluate();Canvas.ForceUpdateCanvases();render.Invoke(null,new object[]{stage,Path.Combine(output,"镜头_"+time.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png")});
            }
            director.Stop();
        }
        catch(Exception e){data.error=e.ToString();Debug.LogException(e);}
        finally{if(stage!=null)PrefabUtility.UnloadPrefabContents(stage);File.WriteAllText(Path.Combine(output,"轨道与路径.json"),JsonUtility.ToJson(data,true));}
    }
}
#endif
