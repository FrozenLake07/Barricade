#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TavernRevisionSetup
{
    const string Root="Assets/Cinematics/TavernUI";
    const string Stage=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    const string Sheet="Assets/Art/Cinematics/TavernUI/Characters/Grantaire/Source/Grantaire_Idle_Sheet.png";
    const string Frames="Assets/Art/Cinematics/TavernUI/Characters/Grantaire/Frames/Idle";
    const string Idle=Root+"/Animations/UI_Grantaire_Idle.anim";
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernRevisionSetup.request"));
    [Serializable] sealed class Report {public bool success,scenePreserved,idleGuidPreserved;public string error;public int checks,frames;public float duration;public string[] passed;}
    static readonly List<string> passed=new List<string>();
    static TavernRevisionSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);passed.Add(message);}
    static void Save(UnityEngine.Object asset){EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);}
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static UnityEngine.UI.Image Actor(GameObject stage,string id)=>stage.GetComponentsInChildren<Animator>(true).Where(a=>a.runtimeAnimatorController!=null&&a.runtimeAnimatorController.name=="UI_"+id).Select(a=>a.GetComponent<UnityEngine.UI.Image>()).Single(i=>i!=null);
    static RectTransform ActorRoot(GameObject stage,string id)=>(RectTransform)Actor(stage,id).transform.parent;
    static Canvas Layer(GameObject go,int order,bool interactive=false)
    {
        var canvas=go.GetComponent<Canvas>();if(canvas==null)canvas=go.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=order;canvas.pixelPerfect=true;
        if(interactive&&go.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null)go.AddComponent<UnityEngine.UI.GraphicRaycaster>();return canvas;
    }
    static void Layers(GameObject stage)
    {
        var frame=stage.transform.Find("Stage_320x180");
        foreach(string id in new[]{"Hero","Grantaire","Enjolras"})
        {
            var actor=ActorRoot(stage,id).gameObject;Layer(actor,20);
            var depth=actor.GetComponent<TavernActorDepth>();if(depth==null)depth=actor.AddComponent<TavernActorDepth>();depth.sortingOrder=20;depth.Apply();
        }
        Layer(frame.Find("03_房间结构").gameObject,8);Layer(frame.Find("04_家具与灯具").gameObject,9);
        Layer(frame.Find("08_门（可动画）").gameObject,10);Layer(frame.Find("06_前景桌椅").gameObject,30);Layer(frame.Find("07_前景木柱").gameObject,40);
        Layer(frame.Find("Dialogue").gameObject,100,true);Layer(frame.Find("IntroFade").gameObject,200);
    }
    static AnimationCurve Curve(float[] times,float[] values,bool step=false)
    {
        var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i])).ToArray());
        for(int i=0;i<times.Length;i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve,i,step?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve,i,step?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }
    static AnimationClip Pose(string id)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Intro/Intro_"+id+"_Position.anim");
    static void Position(string id,float x,float y)
    {
        var clip=Pose(id);
        foreach(string axis in new[]{"x","y"})
        {
            var binding=EditorCurveBinding.FloatCurve("",typeof(RectTransform),"m_AnchoredPosition."+axis);var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;
            for(int i=0;i<2;i++)keys[i].value=axis=="x"?x:y;curve.keys=keys;
            for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
            AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
        Save(clip);
    }
    static void SortAndFace(string id,float[] times,float[] orders,float[] faceTimes,float[] faces)
    {
        var clip=Pose(id);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.DiscreteCurve("",typeof(Canvas),"m_SortingOrder"),null);
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(TavernActorDepth),"sortingOrder"),Curve(times,orders,true));
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalScale.x"),Curve(faceTimes,faces,true));Save(clip);
    }
    static Sprite[] FramesImport(string output)
    {
        Folder(Frames);Directory.CreateDirectory(Path.Combine(output,"Frames"));var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            source.LoadImage(File.ReadAllBytes(Sheet));Check(source.width==256&&source.height==192,"Grantaire idle has exact 4x3 64-pixel grid");var frames=new Sprite[12];
            for(int i=0;i<12;i++)
            {
                var pixels=source.GetPixels((i%4)*64,source.height-(i/4+1)*64,64,64);Check(pixels.Any(p=>p.a==0)&&pixels.Any(p=>p.a>0),"Frame has real transparency and visible character: "+i);
                var frame=new Texture2D(64,64,TextureFormat.RGBA32,false);
                try
                {
                    frame.SetPixels(pixels);frame.Apply();var bytes=frame.EncodeToPNG();string path=Frames+"/Idle_"+i.ToString("00")+".png";File.WriteAllBytes(path,bytes);File.WriteAllBytes(Path.Combine(output,"Frames/Idle_"+i.ToString("00")+".png"),bytes);
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();frames[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Check(frames[i]!=null,"Native idle sprite resolves: "+i);
                }
                finally{UnityEngine.Object.DestroyImmediate(frame);}
            }
            return frames;
        }
        finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    static void RestoreLayout(GameObject stage)
    {
        string[] ids={"Hero","Grantaire","Enjolras"};Vector2[] positions={new Vector2(132,-130),new Vector2(239,-130),new Vector2(192,-112)};
        for(int i=0;i<ids.Length;i++)
        {
            var root=ActorRoot(stage,ids[i]);root.anchoredPosition=positions[i];root.localScale=Vector3.one;root.GetComponent<CanvasGroup>().alpha=1;root.GetComponent<TavernActorDepth>().sortingOrder=20;root.GetComponent<TavernActorDepth>().Apply();
        }
    }
    static void Verify(GameObject stage,string output,Sprite[] frames)
    {
        var director=stage.GetComponentInChildren<PlayableDirector>(true);director.timeUpdateMode=DirectorUpdateMode.Manual;director.Play();
        var hero=ActorRoot(stage,"Hero");var gr=ActorRoot(stage,"Grantaire");var enj=ActorRoot(stage,"Enjolras");var frame=stage.transform.Find("Stage_320x180/08_门（可动画）").GetComponent<Canvas>();
        Action<double> sample=t=>{director.time=t;director.Evaluate();Canvas.ForceUpdateCanvases();};
        try
        {
            sample(0);Check(Vector2.Distance(gr.anchoredPosition,new Vector2(239,-130))<.01&&Vector2.Distance(enj.anchoredPosition,new Vector2(192,-112))<.01,"Timeline opens at original Grantaire and Enjolras positions");
            sample(1.1);Check(hero.GetComponent<Canvas>().sortingOrder<frame.sortingOrder,"Hero inside doorway is behind frame");
            sample(1.7);Check(hero.GetComponent<Canvas>().sortingOrder>frame.sortingOrder,"Hero in the room covers frame instead of being cut by it");
            sample(3.2);Check(gr.localScale.x<0,"Grantaire turns left to walk from restored initial position");
            sample(5);Check(gr.localScale.x>0,"Grantaire turns toward the hero for dialogue");
            sample(8);Check(enj.localScale.x<0,"Enjolras approaches from restored initial position");
            sample(9.3);Check(enj.localScale.x>0,"Enjolras turns to the group before his lines");
            sample(14.3);Check(gr.GetComponent<Canvas>().sortingOrder>frame.sortingOrder,"Departing Grantaire is in front of frame until the threshold");
            sample(14.55);Check(gr.GetComponent<Canvas>().sortingOrder<frame.sortingOrder,"Grantaire is occluded after crossing into doorway");
            sample(16.3);Check(hero.GetComponent<Canvas>().sortingOrder<frame.sortingOrder,"Departing hero is occluded inside doorway");
            sample(18.6);Check(enj.GetComponent<Canvas>().sortingOrder<frame.sortingOrder,"Departing Enjolras is occluded inside doorway");
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Idle);var image=Actor(stage,"Grantaire");
            for(int i=0;i<12;i++){idle.SampleAnimation(image.gameObject,i/6f+.001f);Check(image.sprite==frames[i],"Actual Grantaire idle sample: "+i);}
            Check(stage.transform.Find("Stage_320x180/Dialogue").GetComponent<UnityEngine.UI.GraphicRaycaster>()!=null,"Dialogue Canvas retains input raycasting after layer split");
            var render=typeof(TavernScene3Import).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var shot in new[]{new KeyValuePair<float,string>(0,"01_恢复原站位"),new KeyValuePair<float,string>(1.7f,"02_进门遮挡"),new KeyValuePair<float,string>(5,"03_新待机与拦截"),new KeyValuePair<float,string>(14.55f,"04_离门遮挡")})
            {sample(shot.Key);render.Invoke(null,new object[]{stage,Path.Combine(output,shot.Value+".png")});}
        }
        finally{director.Stop();}
    }
    static void Run(string output)
    {
        Directory.CreateDirectory(output);passed.Clear();var r=new Report();var scene=SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();GameObject contents=null;
        try
        {
            Check(scene.path=="Assets/Scenes/3.unity","Editing scene 3 is retained");var frames=FramesImport(output);string guid=AssetDatabase.AssetPathToGUID(Idle);
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Idle);var keys=frames.Select((sprite,i)=>new ObjectReferenceKeyframe{time=i/6f,value=sprite}).ToArray();clip.frameRate=6;
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(UnityEngine.UI.Image),propertyName="m_Sprite"},keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.stopTime=2;AnimationUtility.SetAnimationClipSettings(clip,settings);Save(clip);
            AssetDatabase.ImportAsset(Idle,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Idle);
            r.idleGuidPreserved=AssetDatabase.AssetPathToGUID(Idle)==guid;Check(r.idleGuidPreserved&&Math.Abs(clip.length-2)<.001,"Idle updates in place as 12 frames at 6fps, 2 seconds");
            Position("Grantaire",239,-130);Position("Enjolras",192,-112);
            SortAndFace("Hero",new[]{0f,1.26f,16.22f,20.5f},new[]{5f,20,5,5},new[]{0f,11f,20.5f},new[]{1f,-1,-1});
            SortAndFace("Grantaire",new[]{0f,14.44f,20.5f},new[]{20f,5,5},new[]{0f,2.8f,4.4f,20.5f},new[]{1f,-1,1,1});
            SortAndFace("Enjolras",new[]{0f,18.53f,20.5f},new[]{20f,5,5},new[]{0f,7f,9f,20.5f},new[]{1f,-1,1,1});
            contents=PrefabUtility.LoadPrefabContents(Stage);Layers(contents);RestoreLayout(contents);Actor(contents,"Grantaire").sprite=frames[0];PrefabUtility.SaveAsPrefabAsset(contents,Stage);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            const string person=Root+"/Prefabs/UI_Grantaire.prefab";contents=PrefabUtility.LoadPrefabContents(person);Actor(contents,"Grantaire").sprite=frames[0];PrefabUtility.SaveAsPrefabAsset(contents,person);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            contents=PrefabUtility.LoadPrefabContents(Stage);Verify(contents,output,frames);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();
            var live=scene.GetRootGameObjects().Single(g=>g.name=="TavernUI_AnimationStage");Layers(live);
            foreach(string id in new[]{"Hero","Grantaire","Enjolras"})Undo.RecordObject(ActorRoot(live,id),"恢复原角色站位");RestoreLayout(live);
            foreach(string id in new[]{"Hero","Grantaire","Enjolras"})
            {
                var actor=ActorRoot(live,id);PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                PrefabUtility.RecordPrefabInstancePropertyModifications(actor.GetComponent<CanvasGroup>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(actor.GetComponent<TavernActorDepth>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(actor.GetComponent<Canvas>());
            }
            r.scenePreserved=roots.All(g=>g!=null&&g.scene==scene);Check(r.scenePreserved,"Existing unsaved scene objects remain");EditorSceneManager.MarkSceneDirty(scene);
            r.frames=12;r.duration=clip.length;r.success=true;Selection.activeObject=live;
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally{if(contents!=null)PrefabUtility.UnloadPrefabContents(contents);r.checks=passed.Count;r.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"原生修正报告.json"),JsonUtility.ToJson(r,true));}
    }
}
#endif
