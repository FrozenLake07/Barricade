#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CampaignArtSetup
{
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/CampaignArtSetup.request"));
    static CampaignArtSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);
        try{Setup();File.WriteAllText(Path.Combine(output,"原生资源配置.txt"),"success");}
        catch(Exception e){File.WriteAllText(Path.Combine(output,"原生资源配置.txt"),e.ToString());Debug.LogException(e);}
    }
    static Sprite Import(string path,float ppu)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=ppu;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Sprite[] Frames(string name,float ppu)=>Enumerable.Range(1,12).Select(i=>Import("Assets/Resources/CampaignActors/Frames/"+name+"/"+i.ToString("00")+".png",ppu)).ToArray();
    static void Clip(string name,Sprite[] sprites,float fps,bool loop)
    {
        string folder="Assets/Resources/CampaignActors/Clips";Directory.CreateDirectory(folder);string path=folder+"/"+name+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.frameRate=fps;
        var keys=sprites.Select((s,i)=>new ObjectReferenceKeyframe{time=i/fps,value=s}).Concat(new[]{new ObjectReferenceKeyframe{time=sprites.Length/fps,value=sprites[sprites.Length-1]}}).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
    }
    static void Setup()
    {
        string path="Assets/Resources/CampaignActors/ActorArt.asset";var art=AssetDatabase.LoadAssetAtPath<CampaignActorArt>(path);
        if(art==null){art=ScriptableObject.CreateInstance<CampaignActorArt>();AssetDatabase.CreateAsset(art,path);}
        art.gavrocheIdle=Import("Assets/Resources/CampaignActors/Frames/GavrocheIdle.png",48);
        art.gavrocheRun=Frames("GavrocheRun",48);art.gavrochePick=Frames("GavrochePick",48);
        art.soldierHit=Frames("SoldierHit",44.444444f);art.soldierDeath=Frames("SoldierDeath",44.444444f);
        art.grenadierHit=Frames("GrenadierHit",44.444444f);art.grenadierDeath=Frames("GrenadierDeath",44.444444f);art.grenadierWindup=Frames("GrenadierWindup",44.444444f);
        Clip("GavrocheRun",art.gavrocheRun,12,true);Clip("GavrochePick",art.gavrochePick,12,false);
        Clip("SoldierHit",art.soldierHit,10,false);Clip("SoldierDeath",art.soldierDeath,10,false);
        Clip("GrenadierHit",art.grenadierHit,12,false);Clip("GrenadierDeath",art.grenadierDeath,12,false);Clip("GrenadierWindup",art.grenadierWindup,12,false);EditorUtility.SetDirty(art);
        var child=new GameObject("Gavroche");
        try
        {
            var sprite=child.AddComponent<SpriteRenderer>();sprite.sprite=art.gavrocheIdle;sprite.sortingOrder=7;
            var body=child.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;
            var box=child.AddComponent<BoxCollider2D>();box.isTrigger=true;box.size=new Vector2(.35f,.7f);box.offset=new Vector2(0,-.1f);
            child.AddComponent<CampaignActor>().escort=true;child.AddComponent<GavrocheVisual>();
            PrefabUtility.SaveAsPrefabAsset(child,"Assets/Resources/CampaignActors/Gavroche.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(child);}
        foreach(string name in new[]{"Soldier","Leader"})
        {
            string prefab="Assets/Resources/enemy/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(prefab);
            try{if(root.GetComponent<CampaignReactionVisual>()==null)root.AddComponent<CampaignReactionVisual>();PrefabUtility.SaveAsPrefabAsset(root,prefab);}finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");Undo.RecordObject(table,"章节机制与动画");
        foreach(var chapter in table.chapters)chapter.ammoEnabled=chapter.number>=5;
        foreach(var wave in new[]{table.chapters[4].waveA,table.chapters[4].waveB})
        {Undo.RecordObject(wave,"第五章沙威已被俘");wave.events=wave.events.Where(e=>e.type!=WaveEventType.Decoy).ToArray();wave.Validate();EditorUtility.SetDirty(wave);}
        foreach(var wave in new[]{table.chapters[7].waveA,table.chapters[7].waveB})
        {Undo.RecordObject(wave,"第八章减少炮击后撤离");wave.requiredShellInterceptions=0;wave.cannonRetreatBeat=110;wave.events=wave.events.Where(e=>e.type!=WaveEventType.Shell||e.beat<=100).ToArray();wave.Validate();EditorUtility.SetDirty(wave);}
        table.chapters[4].waves=WaveDefaults.Legacy(table.chapters[4].waveA);table.chapters[7].waves=WaveDefaults.Legacy(table.chapters[7].waveA);
        table.Validate();EditorUtility.SetDirty(table);AssetDatabase.SaveAssets();CampaignVerification.Check();WaveVerification.Check();
    }
}
#endif
