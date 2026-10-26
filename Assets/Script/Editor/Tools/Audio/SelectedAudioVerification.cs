#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SelectedAudioVerification
{
    [Serializable] sealed class Bank {public SfxPlayer.Binding[] bindings=new SfxPlayer.Binding[0];}
    [Serializable] sealed class Report
    {
        public bool success,editingScenePreserved,configurationUnchanged;
        public int imported,checks;public string error;public string[] tested;
        public string coverage="Native importer and actual event callbacks in an isolated Editor preview. No Play listening; dirty user scene preserved.";
    }
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/SelectedAudioSetup.request"));
    static SelectedAudioVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Run(string output)
    {
        var r=new Report();var tests=new List<string>();
        var editing=SceneManager.GetActiveScene();bool dirty=editing.isDirty;
        var previousStory=StoryDirector.Current;int oldChapter=RunState.Chapter;GameObject root=null;
        Scene preview=default;
        var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");
        string fingerprint=CampaignTelemetry.Fingerprint(table);
        const BindingFlags instance=BindingFlags.Instance|BindingFlags.NonPublic;
        Action<bool,string> check=(ok,text)=>{r.checks++;tests.Add(text);if(!ok)throw new Exception(text);};
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string dir="Assets/Resources/SFX/Selected";
            var paths=Directory.GetFiles(dir,"*.wav").OrderBy(p=>p).ToArray();
            check(paths.Length==41,"40 user-selected clips plus one authorized ordinary grenade impact clip");
            foreach(string path in paths)
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);bool longClip=clip.length>5;
                var settings=importer.defaultSampleSettings;
                settings.loadType=longClip?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=longClip?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;settings.quality=.85f;
                settings.preloadAudioData=!longClip;importer.defaultSampleSettings=settings;
                importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();r.imported++;
                clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                check(clip!=null&&clip.length>.04f&&clip.frequency==48000,"Native import valid: "+clip.name);
            }
            SfxPlayer.ResetState();var bank=JsonUtility.FromJson<Bank>(Resources.Load<TextAsset>("SFX/Selected/Bindings").text);
            foreach(var b in bank.bindings)
                check(string.IsNullOrEmpty(b.path)?SfxPlayer.ClipFor(b.id)==null:SfxPlayer.ClipFor(b.id)!=null,"Binding resolves: "+b.id);
            check(SfxPlayer.ClipFor("SFX-BMB-03")!=null&&SfxPlayer.ClipFor("SFX-BMB-03")!=SfxPlayer.ClipFor("SFX-BMB-04"),"Unexploded impact has a distinct fixed clip");
            check(SfxPlayer.ClipFor("SFX-ENM-02")==null,"Rejected body hit remains silent");
            var rng=UnityEngine.Random.state;SfxPlayer.PlayVariants("SFX-PLY-01",3,.5f,.1f);
            check(JsonUtility.ToJson(rng)==JsonUtility.ToJson(UnityEngine.Random.state),"Fixed playback does not consume gameplay RNG");

            root=PrefabUtility.LoadPrefabContents("Assets/Resources/Campaign/CampaignRoot.prefab");
            var story=root.GetComponent<StoryDirector>();
            typeof(StoryDirector).GetProperty("Current").SetValue(null,story);
            typeof(StoryDirector).GetField("phase",instance).SetValue(story,CampaignPhase.Defense);RunState.Chapter=3;
            preview=EditorSceneManager.NewPreviewScene();
            Func<string,GameObject> node=name=>{var go=new GameObject(name);go.hideFlags=HideFlags.HideAndDontSave;SceneManager.MoveGameObjectToScene(go,preview);return go;};
            var bombNode=node("Audio test grenade");var collider=bombNode.AddComponent<CircleCollider2D>();
            var bomb=bombNode.AddComponent<BombHP>();bomb.HP=100;
            var bulletNode=node("Audio test bullet");var definition=bulletNode.AddComponent<ArrowDefinition>();
            definition.fromPlayer=true;definition.targetKind=ArrowDefinition.TargetKind.Bomb;definition.Damage=50;definition.trackY=0;definition.trackRange=1;
            var bullet=bulletNode.AddComponent<BulletHit>();var hit=typeof(BulletHit).GetMethod("TryDamage",instance);
            int count=SfxPlayer.EmittedCount;
            check((bool)hit.Invoke(bullet,new object[]{collider,definition})&&bomb.HP==100&&bomb.IsAlive,"Ordinary hit leaves grenade HP unchanged at 100");
            check(SfxPlayer.EmittedCount==count+1&&SfxPlayer.LastEventId=="SFX-BMB-03","Ordinary hit emits exactly one unexploded impact sound");
            for(int i=0;i<3;i++)check((bool)hit.Invoke(bullet,new object[]{collider,definition})&&bomb.HP==100&&bomb.IsAlive,"Repeated ordinary hits cannot accumulate damage: "+i);
            definition.Damage=10000;
            check((bool)hit.Invoke(bullet,new object[]{collider,definition})&&bomb.HP==100,"Ordinary flag prevents grenade damage even with a large damage value");
            definition.trackY=5;count=SfxPlayer.EmittedCount;
            check(!(bool)hit.Invoke(bullet,new object[]{collider,definition})&&bomb.HP==100&&SfxPlayer.EmittedCount==count,"Wrong-lane contact has neither damage nor feedback");
            definition.trackY=0;definition.perfectShot=true;definition.Damage=100;
            count=SfxPlayer.EmittedCount;
            check((bool)hit.Invoke(bullet,new object[]{collider,definition})&&!bomb.IsAlive,"Perfect hit still explodes a full-health grenade");
            check(SfxPlayer.EmittedCount==count+1&&SfxPlayer.LastEventId=="SFX-BMB-04","Lethal hit does not double-play explosion sound");
            count=SfxPlayer.EmittedCount;
            check(!(bool)hit.Invoke(bullet,new object[]{collider,definition})&&SfxPlayer.EmittedCount==count,"Already exploded grenade cannot replay feedback");
            var wall=node("Test wall").AddComponent<BoxCollider2D>();wall.size=new Vector2(2,2);
            CampaignSoundscape.BulletImpact(wall,new Vector2(0,-.9f));check(SfxPlayer.LastEventId=="structure_bullet_hit_stone","Low foundation impact uses fixed stone clip");
            CampaignSoundscape.BulletImpact(wall,new Vector2(0,.8f));check(SfxPlayer.LastEventId=="structure_bullet_hit_wood","Upper impact uses fixed wood clip");
            CampaignSoundscape.WallDamage(80,60,100);check(SfxPlayer.LastEventId=="structure_crack_light","First damage threshold uses light crack");
            count=SfxPlayer.EmittedCount;CampaignSoundscape.WallDamage(60,59,100);check(SfxPlayer.EmittedCount==count,"Damage inside same tier does not repeat crack");
            CampaignSoundscape.WallDamage(40,30,100);check(SfxPlayer.LastEventId=="structure_crack_heavy","Heavy threshold uses selected heavy crack");
            CampaignSoundscape.WallDamage(10,0,100);check(SfxPlayer.LastEventId=="structure_collapse","Zero health emits collapse");
            RunState.Chapter=9;count=SfxPlayer.EmittedCount;
            foreach(var b in bank.bindings)SfxPlayer.Play(b.id);
            check(SfxPlayer.EmittedCount==count&&!SfxPlayer.CanPlay("threat_grenade_fuse_loop"),"Chapter nine suppresses all selected events and loops");
            RunState.Chapter=1;typeof(StoryDirector).GetField("phase",instance).SetValue(story,CampaignPhase.Title);
            check(!SfxPlayer.CanPlay("ui_confirm"),"Title buttons preserve silence");
            RunState.Chapter=10;typeof(StoryDirector).GetField("phase",instance).SetValue(story,CampaignPhase.EndCard);
            check(!SfxPlayer.CanPlay("ui_confirm"),"Ending menu preserves music space");
            // Rebase newly introduced audio timer in actual existing save codec.
            var timer=typeof(CampaignStateCodec).GetMethod("Rebase",BindingFlags.Static|BindingFlags.NonPublic);
            check(Math.Abs(Convert.ToSingle(timer.Invoke(null,new object[]{typeof(SBullet),"reloadMechanismAt",10f,-3f,0f,0f}))-7)<.001f,"Reload audio delay rebases on load");
            check(Math.Abs(Convert.ToSingle(timer.Invoke(null,new object[]{typeof(Cannon),"nextChainSound",10f,-3f,0f,0f}))-7)<.001f,"Cannon movement sound timer rebases on load");
            r.success=true;
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally
        {
            typeof(StoryDirector).GetProperty("Current").SetValue(null,previousStory);RunState.Chapter=oldChapter;
            if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
            if(root!=null)PrefabUtility.UnloadPrefabContents(root);
            SfxPlayer.ResetState();
            r.editingScenePreserved=SceneManager.GetActiveScene()==editing&&editing.isDirty==dirty;
            r.configurationUnchanged=CampaignTelemetry.Fingerprint(table)==fingerprint;
            r.success=r.success&&r.editingScenePreserved&&r.configurationUnchanged;r.tested=tests.ToArray();
            Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"音效原生验证.json"),JsonUtility.ToJson(r,true));
        }
    }
}
#endif
