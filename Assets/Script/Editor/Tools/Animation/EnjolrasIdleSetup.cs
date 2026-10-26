#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class EnjolrasIdleSetup
{
    const string Frames="Assets/Art/Cinematics/TavernUI/Characters/Enjolras/Frames/Idle";
    const string Atlas="Assets/Art/Cinematics/TavernUI/Characters/Enjolras/Source/Enjolras_Idle_Transparent.png";
    const string Root="Assets/Cinematics/TavernUI";
    const string ClipPath=Root+"/Animations/UI_Enjolras_Idle.anim";
    const string ControllerPath=Root+"/Animator/UI_Enjolras.controller";
    const string Stage=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/EnjolrasIdleSetup.request"));
    [Serializable] sealed class Report
    {
        public bool success,guidPreserved,walkUnchanged,scenePreserved,sceneDirty;
        public int frames,checks,sourcePixelsVerified;public float fps,duration;
        public string error,clip=ClipPath,guid;public string[] passed;
    }
    static readonly List<string> passed=new List<string>();
    static EnjolrasIdleSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);passed.Add(message);}
    static string Hash(string path){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));}
    static Sprite Import(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;
        importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Check(sprite!=null&&sprite.rect.width>0,"Native transparent sprite import: "+sprite.name);return sprite;
    }
    static int VerifyPixels(Sprite[] frames,string original)
    {
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);int count=0;
        try
        {
            source.LoadImage(File.ReadAllBytes(original));var src=source.GetPixels32();
            Check(source.width==256&&source.height==192,"Original registered 4x3 source dimensions preserved");
            for(int i=0;i<frames.Length;i++)
            {
                var frame=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    frame.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frames[i])));Check(frame.width==64&&frame.height==64,"Untrimmed 64x64 idle frame: "+i);
                    var pixels=frame.GetPixels32();int foreground=0,bottom=64;
                    for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                    {
                        var a=src[(source.height-(i/4+1)*64+y)*source.width+(i%4)*64+x];var b=pixels[y*64+x];
                        bool key=a.a>0&&((a.g==0&&a.b==0&&(a.r==63||a.r==64))||(a.r==60&&a.g==2&&a.b==2)||(a.r==68&&a.g==8&&a.b==5)||(a.r==76&&a.g==0&&a.b==1));
                        if(key){if(b.a!=0)throw new Exception("Red background survived in frame "+i);}
                        else if(!a.Equals(b))throw new Exception("Original character pixel changed in frame "+i);
                        if(b.a>0){foreground++;bottom=Math.Min(bottom,y);}count++;
                    }
                    Check(foreground>0&&bottom==13,"Visible frame keeps fixed foot baseline: "+i);
                }
                finally{UnityEngine.Object.DestroyImmediate(frame);}
            }
            Check(src.Skip(0).Where((p,index)=>index%source.width>=192&&index/source.width<64).All(p=>p.a==0),"Source twelfth cell is empty and excluded");return count;
        }
        finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    static UnityEngine.UI.Image[] Images(GameObject root,AnimatorController controller)=>root.GetComponentsInChildren<Animator>(true).Where(a=>a.runtimeAnimatorController==controller&&a.GetComponent<UnityEngine.UI.Image>()!=null).Select(a=>a.GetComponent<UnityEngine.UI.Image>()).ToArray();
    static void Run(string output)
    {
        Directory.CreateDirectory(output);passed.Clear();var r=new Report();GameObject contents=null;
        var scene=SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();bool wasDirty=scene.isDirty;
        try
        {
            const string walk=Root+"/Animations/UI_Enjolras_Walk.anim";string walkHash=Hash(walk);
            string guid=AssetDatabase.AssetPathToGUID(ClipPath);Check(!string.IsNullOrEmpty(guid),"Existing idle clip is updated in place");
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var oldFirst=(Sprite)AnimationUtility.GetObjectReferenceCurve(clip,binding).First().value;
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Check(controller.layers[0].stateMachine.defaultState.motion==clip,"Existing default Animator state already references this idle clip");
            var sprites=Directory.GetFiles(Frames,"*.png").OrderBy(p=>p).Select(p=>Import(p.Replace('\\','/'))).ToArray();
            Check(sprites.Length==11,"Exactly eleven nonempty idle frames");Import(Atlas);r.sourcePixelsVerified=VerifyPixels(sprites,Path.Combine(output,"Source/昂若拉idle_原图.png"));
            // Unity includes the final PPtr sample interval in clip.length. Do not append a duplicate endpoint.
            var keys=new ObjectReferenceKeyframe[11];for(int i=0;i<11;i++)keys[i]=new ObjectReferenceKeyframe{time=i/6f,value=sprites[i]};
            clip.frameRate=6;AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(UnityEngine.UI.Image),propertyName="m_Sprite"},keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.stopTime=11/6f;AnimationUtility.SetAnimationClipSettings(clip,settings);
            EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(ClipPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            r.guid=AssetDatabase.AssetPathToGUID(ClipPath);r.guidPreserved=r.guid==guid;Check(r.guidPreserved,"Idle clip GUID and existing Timeline references preserved");
            Check(Mathf.Abs(clip.length-11/6f)<.001f&&AnimationUtility.GetAnimationClipSettings(clip).loopTime,"Loop is 11 frames at 6fps, 1.833 seconds; actual="+clip.length+" loop="+AnimationUtility.GetAnimationClipSettings(clip).loopTime);
            foreach(string path in new[]{Root+"/Prefabs/UI_Enjolras.prefab",Stage})
            {
                contents=PrefabUtility.LoadPrefabContents(path);var images=Images(contents,controller);Check(images.Length==1,"Exactly one Enjolras UI actor found: "+path);
                images[0].sprite=sprites[0];PrefabUtility.SaveAsPrefabAsset(contents,path);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            }
            contents=PrefabUtility.LoadPrefabContents(Stage);var image=Images(contents,controller).Single();
            for(int i=0;i<11;i++)
            {
                clip.SampleAnimation(image.gameObject,i/6f+.001f);Check(image.sprite==sprites[i],"Actual UI Image animation sample: "+i);
            }
            image.sprite=sprites[0];
            var director=contents.GetComponentInChildren<PlayableDirector>(true);var timeline=director.playableAsset as TimelineAsset;
            Check(timeline!=null&&timeline.GetOutputTracks().OfType<AnimationTrack>().SelectMany(t=>t.GetClips()).All(c=>((AnimationPlayableAsset)c.asset).clip!=null),"Authored Timeline animation references still resolve");
            Check(contents.GetComponentInChildren<TavernNameDialogue>(true)!=null,"Naming interaction remains attached");
            typeof(TavernScene3Import).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{contents,Path.Combine(output,"酒馆_新待机首帧.png")});
            PrefabUtility.UnloadPrefabContents(contents);contents=null;
            // Preserve hand-authored scene overrides; only refresh a live sprite still showing the old default.
            foreach(var root in roots)foreach(var live in Images(root,controller))if(live.sprite==oldFirst)
            {Undo.RecordObject(live,"更新昂若拉待机首帧");live.sprite=sprites[0];PrefabUtility.RecordPrefabInstancePropertyModifications(live);EditorSceneManager.MarkSceneDirty(scene);}
            r.walkUnchanged=Hash(walk)==walkHash;Check(r.walkUnchanged,"Walk animation asset is unchanged");
            r.scenePreserved=SceneManager.GetActiveScene()==scene&&roots.All(root=>root!=null&&root.scene==scene);r.sceneDirty=scene.isDirty;
            Check(r.scenePreserved&&(!wasDirty||scene.isDirty),"Original scene roots and unsaved edits preserved");
            r.frames=11;r.fps=6;r.duration=clip.length;r.success=true;Selection.activeObject=clip;
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally
        {
            if(contents!=null)PrefabUtility.UnloadPrefabContents(contents);r.checks=passed.Count;r.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"原生配置报告.json"),JsonUtility.ToJson(r,true));
        }
    }
}
#endif
