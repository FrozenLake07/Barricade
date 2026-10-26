#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TavernSceneLayoutSync
{
    const string ScenePath="Assets/Scenes/3.unity",PrefabPath="Assets/Cinematics/TavernUI/Prefabs/TavernUI_AnimationStage.prefab";
    static readonly string[] DecorGroups={"00_地板","01_窗外_夜晚","02_窗外_清晨（备用）","03_房间结构","04_家具与灯具","06_前景桌椅","07_前景木柱","08_门（可动画）","09_备用道具（默认隐藏）"};
    [Serializable] sealed class Report
    {
        public bool success,usedLiveScene,sourceDirty,sourceFileUnchanged,layersUnchanged,siblingOrderUnchanged,timelinesUnchanged,prefabReferenceUnchanged;
        public string error;public string[] changed,removed,checkedAnimationFiles,layersBefore,layersAfter;
    }
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernSceneLayoutSync.request"));
    static TavernSceneLayoutSync(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
    static string PathIn(Transform t,Transform root)=>AnimationUtility.CalculateTransformPath(t,root);
    static Dictionary<string,Transform> Nodes(GameObject root)=>root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>PathIn(t,root.transform));
    static bool Decor(string path)=>DecorGroups.Any(group=>path=="Stage_320x180/"+group||path.StartsWith("Stage_320x180/"+group+"/",StringComparison.Ordinal));
    static string[] Layers(GameObject root)=>root.GetComponentsInChildren<Transform>(true).Select(t=>
    {
        string path=PathIn(t,root.transform);var canvas=t.GetComponent<Canvas>();var depth=t.GetComponent<TavernActorDepth>();
        return path+"|layer="+t.gameObject.layer+(canvas!=null?"|canvas="+canvas.sortingLayerID+","+canvas.sortingOrder+","+canvas.overrideSorting:"")+(depth!=null?"|depth="+depth.sortingOrder:"");
    }).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
    static bool Same(RectTransform a,RectTransform b)=>Vector2.Distance(a.anchorMin,b.anchorMin)<.001f&&Vector2.Distance(a.anchorMax,b.anchorMax)<.001f&&Vector2.Distance(a.pivot,b.pivot)<.001f&&Vector2.Distance(a.sizeDelta,b.sizeDelta)<.001f&&Vector3.Distance(a.anchoredPosition3D,b.anchoredPosition3D)<.001f&&Vector3.Distance(a.localScale,b.localScale)<.001f&&Quaternion.Angle(a.localRotation,b.localRotation)<.001f;
    static void Render(GameObject root,string path)=>typeof(TavernScene3Import).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{root,path});
    static void Run(string output)
    {
        Directory.CreateDirectory(output);var report=new Report();GameObject target=null;Scene added=default;var activeBefore=SceneManager.GetActiveScene();
        string sourceHash=Hash(ScenePath),prefabGuid=AssetDatabase.AssetPathToGUID(PrefabPath);
        var changed=new List<string>();var removed=new List<string>();
        try
        {
            var sourceScene=SceneManager.GetSceneByPath(ScenePath);report.usedLiveScene=sourceScene.IsValid()&&sourceScene.isLoaded;
            if(!report.usedLiveScene){added=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);sourceScene=added;SceneManager.SetActiveScene(activeBefore);}
            report.sourceDirty=sourceScene.isDirty;
            Require(!AnimationMode.InAnimationMode(),"请先退出动画预览，避免将采样姿势写入布景。");
            var source=sourceScene.GetRootGameObjects().Single(g=>g.name=="TavernUI_AnimationStage");
            var sourceNodes=Nodes(source);
            string backup=Path.Combine(output,"修改前备份");Directory.CreateDirectory(backup);File.Copy(PrefabPath,Path.Combine(backup,Path.GetFileName(PrefabPath)),true);
            target=PrefabUtility.LoadPrefabContents(PrefabPath);var before=Nodes(target);
            var children=before.ToDictionary(p=>p.Key,p=>p.Value.Cast<Transform>().Select(c=>PathIn(c,target.transform)).ToArray());
            report.layersBefore=Layers(target);
            var motionFiles=Directory.GetFiles("Assets/Cinematics/TavernUI/Animations","*.anim",SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/Cinematics/TavernUI/Timelines","*.playable",SearchOption.AllDirectories)).ToDictionary(path=>path,Hash);
            report.checkedAnimationFiles=motionFiles.Keys.ToArray();
            Render(target,Path.Combine(output,"预制体_修改前.png"));Render(source,Path.Combine(output,"场景三_布置参考.png"));
            foreach(var pair in sourceNodes.Where(pair=>Decor(pair.Key)))
            {
                Require(before.ContainsKey(pair.Key),"场景含新增布景，请先明确其图层位置："+pair.Key);
                var from=pair.Value;var to=before[pair.Key];var a=from as RectTransform;var b=to as RectTransform;
                if(a!=null&&b!=null&&!Same(a,b))
                {
                    changed.Add(pair.Key+"：位置 "+b.anchoredPosition+" → "+a.anchoredPosition);
                    b.anchorMin=a.anchorMin;b.anchorMax=a.anchorMax;b.pivot=a.pivot;b.sizeDelta=a.sizeDelta;b.anchoredPosition3D=a.anchoredPosition3D;b.localRotation=a.localRotation;b.localScale=a.localScale;
                }
                if(from.gameObject.activeSelf!=to.gameObject.activeSelf){changed.Add(pair.Key+"：显隐 → "+from.gameObject.activeSelf);to.gameObject.SetActive(from.gameObject.activeSelf);}
                var sourceImage=from.GetComponent<UnityEngine.UI.Image>();var image=to.GetComponent<UnityEngine.UI.Image>();
                if(sourceImage!=null&&image!=null&&(sourceImage.sprite!=image.sprite||sourceImage.color!=image.color||sourceImage.type!=image.type||sourceImage.preserveAspect!=image.preserveAspect))
                {changed.Add(pair.Key+"：图片外观");image.sprite=sourceImage.sprite;image.color=sourceImage.color;image.type=sourceImage.type;image.preserveAspect=sourceImage.preserveAspect;}
            }
            foreach(var path in before.Keys.Where(path=>Decor(path)&&!sourceNodes.ContainsKey(path)).OrderBy(path=>path.Length))
            {if(before[path]!=null){removed.Add(path);UnityEngine.Object.DestroyImmediate(before[path].gameObject);}}
            var after=Nodes(target);
            foreach(var pair in sourceNodes.Where(pair=>Decor(pair.Key)))
            {
                var actual=after[pair.Key];Require(actual.gameObject.activeSelf==pair.Value.gameObject.activeSelf,"显隐不一致："+pair.Key);
                if(actual is RectTransform a&&pair.Value is RectTransform b)Require(Same(a,b),"布置不一致："+pair.Key);
            }
            report.layersAfter=Layers(target);
            var expectedLayers=report.layersBefore.Where(line=>after.ContainsKey(line.Substring(0,line.IndexOf('|')))).ToArray();
            Require(report.layersAfter.SequenceEqual(expectedLayers),"图层字段发生变化，取消保存。");report.layersUnchanged=true;
            foreach(var pair in after)
            {
                var expected=children[pair.Key].Where(after.ContainsKey).ToArray();
                Require(pair.Value.Cast<Transform>().Select(c=>PathIn(c,target.transform)).SequenceEqual(expected),"前后顺序发生变化，取消保存："+pair.Key);
            }
            report.siblingOrderUnchanged=true;report.changed=changed.ToArray();report.removed=removed.ToArray();
            PrefabUtility.SaveAsPrefabAsset(target,PrefabPath,out bool saved);Require(saved,"保存预制体失败。");
            PrefabUtility.UnloadPrefabContents(target);target=PrefabUtility.LoadPrefabContents(PrefabPath);
            Require(Layers(target).SequenceEqual(report.layersAfter),"重新加载后图层不一致。");
            Require(motionFiles.All(pair=>Hash(pair.Key)==pair.Value),"动画资产发生变化。");report.timelinesUnchanged=true;
            report.prefabReferenceUnchanged=AssetDatabase.AssetPathToGUID(PrefabPath)==prefabGuid;Require(report.prefabReferenceUnchanged,"预制体引用GUID发生变化。");
            report.sourceFileUnchanged=Hash(ScenePath)==sourceHash;Require(report.sourceFileUnchanged,"场景三文件发生变化。");
            Render(target,Path.Combine(output,"预制体_同步后.png"));report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            if(target!=null)PrefabUtility.UnloadPrefabContents(target);if(added.IsValid())EditorSceneManager.CloseScene(added,true);if(activeBefore.IsValid()&&activeBefore.isLoaded)SceneManager.SetActiveScene(activeBefore);
            report.changed=changed.ToArray();report.removed=removed.ToArray();File.WriteAllText(Path.Combine(output,"布置同步与图层核验.json"),JsonUtility.ToJson(report,true));
        }
    }
}
#endif

