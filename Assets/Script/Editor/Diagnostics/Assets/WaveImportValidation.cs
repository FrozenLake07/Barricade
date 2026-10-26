#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WaveImportValidation
{
    [Serializable] public sealed class Entry
    {public string asset;public string[] errors,warnings;}
    [Serializable] public sealed class Report
    {public bool success;public string utc;public Entry[] entries;public int errors,warnings;}
    [MenuItem("街垒/战役/审计波次末端与护送约束")]
    public static Report ValidateAll()
    {
        var entries=new List<Entry>();int errors=0,warnings=0;
        foreach(string guid in AssetDatabase.FindAssets("t:WaveScript",new[]{"Assets/Resources/Campaign/Waves"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var script=AssetDatabase.LoadAssetAtPath<WaveScript>(path);
            var entry=new Entry{asset=path,errors=new string[0],warnings=WaveAudit.Warnings(script)};
            try{script.Validate();}catch(Exception e){entry.errors=new[]{e.Message};}
            errors+=entry.errors.Length;warnings+=entry.warnings.Length;entries.Add(entry);
        }
        var report=new Report{success=errors==0&&entries.Count==16,utc=DateTime.UtcNow.ToString("O"),entries=entries.ToArray(),errors=errors,warnings=warnings};
        File.WriteAllText("Library/WaveValidation.json",JsonUtility.ToJson(report,true));return report;
    }
}

public sealed class WaveAssetPostprocessor:AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] from)
    {
        if(imported.Concat(moved).Any(p=>p.StartsWith("Assets/Resources/Campaign/Waves/")&&p.EndsWith(".asset")))
            EditorApplication.delayCall+=()=>WaveImportValidation.ValidateAll();
    }
}

public sealed class WaveSaveGuard:AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        if(!paths.Any(p=>p.StartsWith("Assets/Resources/Campaign/Waves/")&&p.EndsWith(".asset")))return paths;
        var report=WaveImportValidation.ValidateAll();
        var failed=new HashSet<string>(report.entries.Where(e=>e.errors.Length>0).Select(e=>e.asset));
        foreach(var e in report.entries.Where(e=>e.errors.Length>0))Debug.LogError("[WaveSaveGuard] 阻止保存："+e.asset+"\n"+string.Join("\n",e.errors));
        return paths.Where(p=>!failed.Contains(p)).ToArray();
    }
}
#endif
