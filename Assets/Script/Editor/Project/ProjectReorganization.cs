#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit, one-shot migration. Nothing is moved merely by opening/recompiling the project.
[InitializeOnLoad]
public static class ProjectReorganization
{
    [Serializable] sealed class Move { public string source="",target=""; }
    [Serializable] sealed class Plan { public Move[] moves=Array.Empty<Move>(),references=Array.Empty<Move>(); }
    [Serializable] sealed class Moved { public string source,target,guid; }
    [Serializable] sealed class Report
    {
        public bool success,scenePreserved,dependenciesPreserved,rolledBack;
        public string error,scene;
        public List<Moved> moved=new List<Moved>();
        public List<string> checkedDependencies=new List<string>();
    }
    static ProjectReorganization(){EditorApplication.update+=Poll;}
    static string[] CriticalAssets={"Assets/Scenes/Title.unity","Assets/Scenes/1.unity","Assets/Scenes/2.unity","Assets/Scenes/3.unity","Assets/Resources/Campaign/CampaignRoot.prefab","Assets/Resources/Campaign/ChapterTable.asset","Assets/Resources/Campaign/Cinematics.asset","Assets/Resources/Campaign/GameFont.asset","Assets/UI/Campaign/StartScreen.prefab","Assets/Cinematics/TavernUI/Prefabs/TavernUI_AnimationStage.prefab"};
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ProjectReorganization.request"));
        if(!File.Exists(request))return;
        var lines=File.ReadAllLines(request);File.Delete(request);Apply(lines[0],lines[1]);
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static string Dependencies(string asset)=>string.Join("\n",AssetDatabase.GetDependencies(asset,true).Select(AssetDatabase.AssetPathToGUID).Where(g=>!string.IsNullOrEmpty(g)).OrderBy(g=>g));
    static void Apply(string output,string planPath)
    {
        var report=new Report();var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(planPath));var before=new Dictionary<string,string>();var originals=new Dictionary<string,string>();
        foreach(var asset in CriticalAssets)if(File.Exists(asset))before[asset]=Dependencies(asset);
        report.scene=scene.path;
        try
        {
            foreach(var move in plan.moves)
            {
                if(!File.Exists(move.source)&&!Directory.Exists(move.source))throw new FileNotFoundException(move.source);
                if(File.Exists(move.target)||Directory.Exists(move.target))throw new IOException("Destination already exists: "+move.target);
                if(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(move.source)))throw new Exception("Unimported asset: "+move.source);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(string script in Directory.GetFiles("Assets/Script","*.cs",SearchOption.AllDirectories))
                {
                    string text=File.ReadAllText(script),updated=text;
                    foreach(var replacement in plan.references.OrderByDescending(r=>r.source.Length))updated=updated.Replace(replacement.source,replacement.target);
                    if(updated!=text){originals[script.Replace('\\','/')]=text;File.WriteAllText(script,updated,new System.Text.UTF8Encoding(true));}
                }
                foreach(var move in plan.moves)
                {
                    Folder(Path.GetDirectoryName(move.target).Replace('\\','/'));
                    string guid=AssetDatabase.AssetPathToGUID(move.source),error=AssetDatabase.MoveAsset(move.source,move.target);
                    if(!string.IsNullOrEmpty(error))throw new Exception(error);
                    report.moved.Add(new Moved{source=move.source,target=move.target,guid=guid});
                }
            }
            finally{AssetDatabase.StopAssetEditing();}
            foreach(var moved in report.moved)if(AssetDatabase.AssetPathToGUID(moved.target)!=moved.guid)throw new Exception("GUID changed: "+moved.target);
            foreach(var pair in before)
            {
                if(Dependencies(pair.Key)!=pair.Value)throw new Exception("Dependency GUIDs changed: "+pair.Key);
                report.checkedDependencies.Add(pair.Key);
            }
            report.dependenciesPreserved=true;
            report.scenePreserved=SceneManager.GetActiveScene().path==scene.path&&SceneManager.GetActiveScene().isDirty==dirty;
            if(!report.scenePreserved)throw new Exception("Editor scene changed");
            report.success=true;
        }
        catch(Exception e)
        {
            report.error=e.ToString();
            foreach(var move in report.moved.AsEnumerable().Reverse()){Folder(Path.GetDirectoryName(move.source).Replace('\\','/'));AssetDatabase.MoveAsset(move.target,move.source);}
            foreach(var pair in originals)File.WriteAllText(pair.Key,pair.Value,new System.Text.UTF8Encoding(true));
            report.rolledBack=report.moved.All(m=>AssetDatabase.AssetPathToGUID(m.source)==m.guid);Debug.LogException(e);
        }
        File.WriteAllText(Path.Combine(output,"Unity资源移动与依赖检查.json"),JsonUtility.ToJson(report,true));
        AssetDatabase.Refresh();
    }
}
#endif

