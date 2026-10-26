#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameplayPolishSetup
{
    [Serializable] sealed class Cue {public string asset="";public int[] chapters=Array.Empty<int>();public int duration=0;}
    [Serializable] sealed class Cues {public Cue[] clips=Array.Empty<Cue>();}
    [Serializable] sealed class Damage {public int chapter,body,bullet,grenade,cannonAtMaximum;public float multiplier;public string music;}
    [Serializable] sealed class Report {public bool success,scenePreserved;public int checks,wallMaximum,soldierHP,grenadierHP,grenadeHP;public float cannonFraction;public string error;public string[] passed;public Damage[] damage;}
    static readonly List<string> passed=new List<string>();
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/GameplayPolishSetup.request"));
    static GameplayPolishSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);passed.Add(text);}
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    static GameObject Node(string name,Scene scene){var node=new GameObject(name);SceneManager.MoveGameObjectToScene(node,scene);return node;}
    static BombHP Bomb(Scene preview,int group)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("arrow/Bomb"),preview);var hp=go.GetComponent<BombHP>();hp.Configure(group,0);return hp;
    }
    static void BlurCheck(Scene preview,string output)
    {
        var shader=Resources.Load<Shader>("UI/EnemyWallImpactBlur");Check(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Enemy-only sprite blur shader compiles");
        var camera=Node("Retired camera effect check",preview).AddComponent<Camera>();camera.enabled=false;
        var retired=camera.gameObject.AddComponent<WallImpactBlur>();retired.Pulse(1);
        Check(!retired.enabled&&retired.Strength==0,"Retired whole-camera blur cannot re-enable or pulse");UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }
    static void Run(string output)
    {
        Directory.CreateDirectory(output);passed.Clear();var report=new Report();var editing=SceneManager.GetActiveScene();bool dirty=editing.isDirty;var roots=editing.GetRootGameObjects();
        Scene preview=default;var previousStory=StoryDirector.Current;int hp=GameManager.WallHP,maximum=GameManager.WallMaxHP;var debug=CampaignDebug.Value;bool god=debug.god;
        try
        {
            Check(previousStory==null,"No active campaign is changed by isolated verification");debug.god=false;
            var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");Check(table!=null,"Chapter table resolves");
            var unchanged=table.chapters.Select(c=>c.openingMusic).ToArray();
            var cues=JsonUtility.FromJson<Cues>("{\"clips\":"+File.ReadAllText(Path.Combine(output,"章节高潮配置.json"))+"}");
            foreach(var cue in cues.clips)
            {
                AssetDatabase.ImportAsset(cue.asset,ImportAssetOptions.ForceSynchronousImport);var importer=(AudioImporter)AssetImporter.GetAtPath(cue.asset);var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;settings.preloadAudioData=false;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(cue.asset);Check(clip!=null&&Math.Abs(clip.length-cue.duration)<.1&&clip.frequency==44100,"Core clip imports: "+Path.GetFileName(cue.asset));
                foreach(int chapter in cue.chapters)table.chapters[chapter-1].openingMusic=clip;
            }
            foreach(int chapter in new[]{1,2,9,10})Check(table.chapters[chapter-1].openingMusic==unchanged[chapter-1],"Unrequested chapter music unchanged: "+chapter);
            foreach(int chapter in Enumerable.Range(3,6))Check(table.chapters[chapter-1].openingMusic!=null&&AssetDatabase.GetAssetPath(table.chapters[chapter-1].openingMusic).StartsWith("Assets/Audio/ChapterHighlights/",StringComparison.Ordinal),"Chapter uses core clip: "+chapter);
            table.Validate();EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);
            preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(editing);GameManager.WallMaxHP=1000;GameManager.WallHP=1000;
            var a=Bomb(preview,Spawn.NewGroupId());var b=Bomb(preview,a.groupId);var c=Bomb(preview,a.groupId);var unrelated=Bomb(preview,Spawn.NewGroupId());
            a.TakeDamage(10000);Check(!a.IsAlive&&!b.IsAlive&&!c.IsAlive&&unrelated.IsAlive,"Shooting one grenade explodes the entire matching volley only");
            Check(GameManager.WallHP==1000,"Intercepted chain does not damage the barricade");
            Check(new[]{a,b,c}.All(x=>x.GetComponents<Collider2D>().All(col=>!col.enabled)),"Every chained grenade disables its collision immediately");
            var d=Bomb(preview,Spawn.NewGroupId());var e=Bomb(preview,d.groupId);var f=Bomb(preview,d.groupId);d.Explode(true);e.Explode(true);f.Explode(true);
            Check(GameManager.WallHP==980,"A whole volley striking the wall resolves its fixed damage once");
            var ordinary=Bomb(preview,Spawn.NewGroupId());var bullet=Node("Test ordinary bullet",preview);var definition=bullet.AddComponent<ArrowDefinition>();definition.targetKind=ArrowDefinition.TargetKind.Bomb;definition.perfectShot=false;definition.Damage=9999;definition.trackY=0;
            var hit=bullet.AddComponent<BulletHit>();var method=typeof(BulletHit).GetMethod("TryDamage",BindingFlags.NonPublic|BindingFlags.Instance);
            int before=ordinary.HP;Check((bool)method.Invoke(hit,new object[]{ordinary.GetComponent<Collider2D>(),definition})&&ordinary.HP==before&&ordinary.IsAlive,"Ordinary hit still cannot chip grenade health");
            Check(SfxPlayer.LastEventId=="SFX-BMB-03","Ordinary grenade hit retains its distinct non-explosion feedback");
            definition.perfectShot=true;Check((bool)method.Invoke(hit,new object[]{ordinary.GetComponent<Collider2D>(),definition})&&!ordinary.IsAlive,"Perfect hit still explodes a grenade");
            Check(SfxPlayer.MixGainFor("HeartbeatPulse/Pulse-V1")==1,"Heartbeat reference volume is unchanged");
            Check(SfxPlayer.MixGainFor("SFX-BMB-04")<SfxPlayer.MixGainFor("SFX-PLY-01"),"Explosion mix sits below player fire");
            Check(SfxPlayer.MixGainFor("SFX-ENM-01")<SfxPlayer.MixGainFor("SFX-PLY-01"),"Enemy fire mix sits below player fire");
            Check(SfxPlayer.MixGainFor("SFX-BMB-02")<.3,"Grenade fuse layers are restrained");
            var view=Node("Last picture policy test",preview).AddComponent<CampaignCinematicView>();var page=Node("Test picture page",preview);Set(view,"page",page);Set(view,"activeDurations",new double[]{1,1});Set(view,"shotStartedFrame",Time.frameCount-10);Set(view,"shotStartedAt",Time.unscaledTime-1);Set(view,"shownFrame",0);
            Check(!view.CanSkipFinalPicture,"Any-key early finish is not available before the last picture");Set(view,"shownFrame",1);Check(view.CanSkipFinalPicture,"Any-key early finish is available on the last picture");Set(view,"shotStartedAt",Time.unscaledTime);Check(!view.CanSkipFinalPicture,"Entering last picture has a short accidental-key guard");Set(view,"page",null);UnityEngine.Object.DestroyImmediate(view.gameObject);UnityEngine.Object.DestroyImmediate(page);
            BlurCheck(preview,output);
            report.wallMaximum=table.constructionLimit;report.soldierHP=Resources.Load<GameObject>("enemy/Soldier").GetComponent<EnemyHP>().HP;report.grenadierHP=Resources.Load<GameObject>("enemy/Leader").GetComponent<EnemyHP>().HP;report.grenadeHP=Resources.Load<GameObject>("arrow/Bomb").GetComponent<BombHP>().HP;report.cannonFraction=Resources.Load<GameObject>("enemy/Cannon").GetComponent<Cannon>().wallDamageFraction;
            report.damage=table.chapters.Where(ch=>ch.number>=3&&ch.number<=9).Select(ch=>new Damage{chapter=ch.number,multiplier=ch.combatDamageMultiplier,body=CampaignRevisionRules.ScaleCombatDamage(10,ch.combatDamageMultiplier),bullet=CampaignRevisionRules.ScaleCombatDamage(1,ch.combatDamageMultiplier),grenade=CampaignRevisionRules.ScaleCombatDamage(20,ch.combatDamageMultiplier),cannonAtMaximum=CampaignRevisionRules.ScaleCombatDamage(CannonShell.CalculateWallDamage(table.constructionLimit,report.cannonFraction),ch.combatDamageMultiplier),music=ch.openingMusic!=null?ch.openingMusic.name:"静音"}).ToArray();
            report.scenePreserved=SceneManager.GetActiveScene()==editing&&editing.isDirty==dirty&&roots.All(g=>g!=null&&g.scene==editing);Check(report.scenePreserved,"Editing scene and unsaved user work retained");report.success=true;
        }
        catch(Exception error){report.error=error.ToString();Debug.LogException(error);}
        finally
        {
            if(preview.IsValid())EditorSceneManager.CloseScene(preview,true);GameManager.WallHP=hp;GameManager.WallMaxHP=maximum;debug.god=god;
            report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"原生检查.json"),JsonUtility.ToJson(report,true));
        }
    }
}
#endif
