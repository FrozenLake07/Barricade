#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class TavernRevisionInspect
{
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernRevisionInspect.request"));
    [Serializable] sealed class Actor {public string name,scope;public Vector2 position;public Vector3 scale;}
    [Serializable] sealed class Report {public bool playing,dirty,animationPreview;public string scene;public double time;public Actor[] actors;public string[] overrides;}
    static TavernRevisionInspect(){EditorApplication.update+=Poll;}
    static Actor[] Actors(GameObject root,string scope)=>root.GetComponentsInChildren<Animator>(true).Where(a=>a.GetComponent<UnityEngine.UI.Image>()!=null&&a.runtimeAnimatorController!=null&&new[]{"UI_Hero","UI_Grantaire","UI_Enjolras"}.Contains(a.runtimeAnimatorController.name)).Select(a=>new Actor{name=a.transform.parent.name,scope=scope,position=((RectTransform)a.transform.parent).anchoredPosition,scale=a.transform.parent.localScale}).ToArray();
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);var scene=SceneManager.GetActiveScene();var stage=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="TavernUI_AnimationStage");
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Cinematics/TavernUI/Prefabs/TavernUI_AnimationStage.prefab");
        try
        {
            var r=new Report{playing=EditorApplication.isPlaying,scene=scene.path,dirty=scene.isDirty,animationPreview=AnimationMode.InAnimationMode(),actors=Actors(prefab,"prefab").Concat(stage!=null?Actors(stage,"live"):new Actor[0]).ToArray()};
            if(stage!=null){r.time=stage.GetComponentInChildren<PlayableDirector>(true).time;r.overrides=(PrefabUtility.GetPropertyModifications(stage)??new PropertyModification[0]).Where(p=>p.target is RectTransform&&(p.propertyPath.Contains("AnchoredPosition")||p.propertyPath.Contains("LocalScale"))).Select(p=>p.target.name+":"+p.propertyPath+"="+p.value).ToArray();}
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"站位现场.json"),JsonUtility.ToJson(r,true));
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
    }
}
#endif
