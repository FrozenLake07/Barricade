#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// One-off chapter configuration edit through Unity serialization; runs only on explicit request.
[InitializeOnLoad]
public static class ConstructionBeatRedistribution
{
    static ConstructionBeatRedistribution(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ConstructionBeatRedistribution.request"));
        if(!File.Exists(request))return;
        string output=File.ReadAllText(request).Trim();File.Delete(request);
        try
        {
            var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");
            int before=table.chapters.Sum(c=>c.TotalBeats);
            int[] numbers={7,8,9},values={41,48,48},expected={22,58,55};
            for(int i=0;i<numbers.Length;i++)
            {
                int current=table.chapters[numbers[i]-1].preparationBeats;
                if(current!=expected[i]&&current!=values[i])throw new InvalidOperationException("Unexpected chapter preparation beats: "+numbers[i]+" = "+current);
            }
            Undo.RecordObject(table,"Redistribute construction beats to chapter seven");
            for(int i=0;i<numbers.Length;i++)table.chapters[numbers[i]-1].preparationBeats=values[i];
            table.targetBeatBudget=CampaignRevisionRules.ChapterBeatBudget;
            table.Validate();
            if(table.chapters.Sum(c=>c.TotalBeats)!=CampaignRevisionRules.ChapterBeatBudget)throw new InvalidOperationException("Total beat budget changed");
            EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);
            foreach(var c in table.chapters)if(c.preparationBeats!=CampaignRevisionRules.PreparationBeats(c.number))throw new InvalidOperationException("Default/config mismatch: "+c.number);
            File.WriteAllText(output,"SUCCESS\nBefore total="+before+"; after total="+table.chapters.Sum(c=>c.TotalBeats)+"\n"+string.Join("\n",table.chapters.Select(c=>"Chapter "+c.number+": construction="+c.preparationBeats+", defense="+c.defenseBeats+", total="+c.TotalBeats)));
        }
        catch(Exception e){File.WriteAllText(output,"FAILED\n"+e);Debug.LogException(e);}
    }
}
#endif
