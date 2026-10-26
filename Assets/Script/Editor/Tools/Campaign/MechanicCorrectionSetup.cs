#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MechanicCorrectionSetup
{
    [Serializable] sealed class OldChapter {public int chapter=0;public float before=0,after=0;}
    [Serializable] sealed class Previous {public OldChapter[] chapters=Array.Empty<OldChapter>();}
    [Serializable] sealed class Report {public bool success,scenePreserved;public string error;public int checks;public string[] passed;}
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/MechanicCorrectionSetup.request"));
    static MechanicCorrectionSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);var report=new Report();var passed=new List<string>();var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        Action<bool,string> check=(ok,text)=>{if(!ok)throw new Exception(text);passed.Add(text);};GameObject probe=null;
        try
        {
            var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");var music=table.chapters.Select(c=>c.openingMusic).ToArray();
            var previous=JsonUtility.FromJson<Previous>(File.ReadAllText(Path.Combine(output,"../机制介绍与轻度降难_20261024/轻度降难原生检查.json")));
            Undo.RecordObject(table,"撤销伤害降难，改为降低士兵密度");
            foreach(var ch in previous.chapters)
            {
                var c=table.chapters[ch.chapter-1];check(Math.Abs(c.combatDamageMultiplier-ch.after)<.0001||Math.Abs(c.combatDamageMultiplier-ch.before)<.0001,"Prior damage adjustment identified: chapter "+ch.chapter);c.combatDamageMultiplier=ch.before;
            }
            table.Validate();EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);
            foreach(var ch in previous.chapters)check(table.chapters[ch.chapter-1].combatDamageMultiplier==ch.before,"Damage restored: chapter "+ch.chapter);
            check(GameManager.BulletBaseDamage==.5f,"Bullet base damage remains one half");check(table.chapters.Select(c=>c.openingMusic).SequenceEqual(music),"Approved music retained");
            AssetDatabase.ImportAsset("Assets/UI/Fonts/Silver-BarricadeUI.ttf",ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/Silver-BarricadeUI.ttf");font.RequestCharactersInTexture("拦截",40,FontStyle.Normal);
            check(font==GameFont.Silver&&font.HasCharacter('拦'),"Current game font contains the added lan glyph");
            check(font.GetCharacterInfo('拦',out var glyph,40,FontStyle.Normal)&&glyph.advance>0&&glyph.glyphWidth>0,"Added glyph produces a visible Unity character");
            ChapterMechanicIntroduction.TryGet(5,table.chordMaxGap,out var title,out var body);
            check(title=="弹药管理"&&!body.Contains("沙威")&&!body.Contains("辨认")&&!body.Contains("辨别"),"Removed target-identification mechanic has no introduction");
            probe=new GameObject("DensityVerification");var wave=probe.AddComponent<WaveDirector>();wave.enabled=false;
            var method=typeof(WaveDirector).GetMethod("ReducedSoldier",BindingFlags.NonPublic|BindingFlags.Instance);int removed=0;
            for(int i=0;i<20;i++)check(!(bool)method.Invoke(wave,new object[]{"opening"}),"Opening soldier retained "+i);
            for(int i=0;i<100;i++)if((bool)method.Invoke(wave,new object[]{"ordinary"}))removed++;
            check(removed==100-WaveDirector.SoldierDensityPercent,"Ordinary soldier density matches configured percentage");
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally{if(probe!=null)UnityEngine.Object.DestroyImmediate(probe);report.scenePreserved=SceneManager.GetActiveScene()==scene&&scene.isDirty==dirty;report.success&=report.scenePreserved;report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"原生修正检查.json"),JsonUtility.ToJson(report,true));}
    }
}
#endif

