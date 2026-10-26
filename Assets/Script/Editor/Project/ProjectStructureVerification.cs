#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ProjectStructureVerification
{
    [Serializable] sealed class Report
    {
        public bool success,scenePreserved;
        public int checkedScripts,checkedGameObjects,missingScripts,removedEmptyFolders;
        public string error;
        public List<string> checkedAssets=new List<string>();
    }
    static ProjectStructureVerification(){EditorApplication.update+=Poll;}
    [MenuItem("街垒/工程导航/检查工程结构与引用")]
    public static void CheckMenu()=>Check(Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/ProjectStructure")));
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ProjectStructureCheck.request"));
        if(!File.Exists(request))return;
        string output=File.ReadAllText(request).Trim();File.Delete(request);Check(output);
    }
    static void CheckHierarchy(GameObject root,Report report)
    {
        foreach(var node in root.GetComponentsInChildren<Transform>(true))
        {
            report.checkedGameObjects++;
            report.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
        }
    }
    static void Check(string output)
    {
        Directory.CreateDirectory(output);
        var report=new Report();var editing=SceneManager.GetActiveScene();bool dirty=editing.isDirty;
        try
        {
            foreach(var folder in Directory.GetDirectories("Assets/Script","*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length))
            {
                if(Directory.GetFileSystemEntries(folder).Length==0&&AssetDatabase.DeleteAsset(folder.Replace('\\','/')))report.removedEmptyFolders++;
            }
            foreach(var path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/Script/")&&p.EndsWith(".cs")))
            {
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(path);if(script==null)throw new Exception("Script not imported: "+path);report.checkedScripts++;
            }
            foreach(string sceneName in new[]{CampaignSceneNames.Title,CampaignSceneNames.Construction,CampaignSceneNames.Defense,CampaignSceneNames.Tavern})
            {
                string path=CampaignSceneNames.AssetPath(sceneName);var existing=SceneManager.GetSceneByPath(path);bool opened=!existing.IsValid()||!existing.isLoaded;var preview=opened?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):existing;
                try{foreach(var root in preview.GetRootGameObjects())CheckHierarchy(root,report);report.checkedAssets.Add(path);}
                finally{SceneManager.SetActiveScene(editing);if(opened)EditorSceneManager.CloseScene(preview,true);}
            }
            foreach(var path in new[]{"Assets/Resources/Campaign/CampaignRoot.prefab","Assets/UI/Campaign/StartScreen.prefab","Assets/Resources/enemy/Soldier.prefab","Assets/Resources/enemy/Leader.prefab","Assets/Cinematics/TavernUI/Prefabs/TavernUI_AnimationStage.prefab"})
            {
                var contents=PrefabUtility.LoadPrefabContents(path);
                try{CheckHierarchy(contents,report);report.checkedAssets.Add(path);}
                finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            if(report.missingScripts!=0)throw new Exception("Missing script count: "+report.missingScripts);
            var sync=typeof(Editor).Assembly.GetType("UnityEditor.SyncVS");
            sync?.GetMethod("SyncSolution",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.Invoke(null,null);
            report.scenePreserved=SceneManager.GetActiveScene().path==editing.path&&SceneManager.GetActiveScene().isDirty==dirty;
            if(!report.scenePreserved)throw new Exception("Editing scene changed");report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText(Path.Combine(output,"工程结构与组件引用检查.json"),JsonUtility.ToJson(report,true));
    }
}
#endif

