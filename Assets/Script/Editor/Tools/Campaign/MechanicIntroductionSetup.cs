#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MechanicIntroductionSetup
{
    [Serializable] sealed class Difficulty {public int chapter;public float before,after,bullet;public int body,grenade,cannon;}
    [Serializable] sealed class Report {public bool success,scenePreserved;public string error;public int checks;public string[] passed;public float chordBefore,chordAfter;public Difficulty[] chapters;}
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/MechanicIntroductionSetup.request"));
    static MechanicIntroductionSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);string reportPath=Path.Combine(output,"轻度降难原生检查.json");
        if(File.Exists(reportPath)&&JsonUtility.FromJson<Report>(File.ReadAllText(reportPath)).success)return;
        var report=new Report();var passed=new List<string>();var editing=SceneManager.GetActiveScene();bool dirty=editing.isDirty;
        Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);passed.Add(text);};
        try
        {
            var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");
            var music=table.chapters.Select(c=>c.openingMusic).ToArray();var beats=table.chapters.Select(c=>c.TotalBeats).ToArray();var tempo=table.chapters.Select(c=>c.startingWait).ToArray();float window=table.perfectWindow;
            Undo.RecordObject(table,"小幅降低各操作章节难度");report.chordBefore=table.chordMaxGap;table.chordMaxGap=Mathf.Max(table.chordMaxGap,.14f);report.chordAfter=table.chordMaxGap;
            var changed=new List<Difficulty>();
            foreach(var c in table.chapters.Where(c=>c.defenseBeats>0))
            {
                float before=c.combatDamageMultiplier;c.combatDamageMultiplier=Mathf.Max(.1f,(float)Math.Round(before*.9f,4));
                changed.Add(new Difficulty{chapter=c.number,before=before,after=c.combatDamageMultiplier,bullet=GameManager.BulletBaseDamage*c.combatDamageMultiplier,body=CampaignRevisionRules.ScaleCombatDamage(10,c.combatDamageMultiplier),grenade=CampaignRevisionRules.ScaleCombatDamage(20,c.combatDamageMultiplier),cannon=CampaignRevisionRules.ScaleCombatDamage(CannonShell.CalculateWallDamage(table.constructionLimit,.25f),c.combatDamageMultiplier)});
            }
            table.Validate();EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);report.chapters=changed.ToArray();
            check(table.chordMaxGap>=.14f,"Construction chord gap relaxed to at least 140ms");
            check(changed.Count==7&&changed.All(c=>c.after<=c.before),"Combat chapters 3-9 receive ten-percent damage reduction");
            check(table.chapters.Select(c=>c.openingMusic).SequenceEqual(music),"Approved chapter music retained");
            check(table.chapters.Select(c=>c.TotalBeats).SequenceEqual(beats)&&table.chapters.Select(c=>c.startingWait).SequenceEqual(tempo)&&table.perfectWindow==window,"Chapter beats and heartbeat timings retained");
            foreach(int n in new[]{2,3,4,5,8,9})check(ChapterMechanicIntroduction.TryGet(n,table.chordMaxGap,out var title,out var body)&&!string.IsNullOrWhiteSpace(title)&&!string.IsNullOrWhiteSpace(body),"Mechanic content available: chapter "+n);
            foreach(int n in new[]{1,6,7,10})check(!ChapterMechanicIntroduction.TryGet(n,table.chordMaxGap,out _,out _),"No duplicate mechanic page: chapter "+n);
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally{report.scenePreserved=SceneManager.GetActiveScene()==editing&&editing.isDirty==dirty;report.success&=report.scenePreserved;report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));}
    }
}
#endif
