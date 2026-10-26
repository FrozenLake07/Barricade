#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HopeEndingMusicSetup
{
    const string AudioPath="Assets/Audio/CampaignRecordings/Hope_MourningAndHope.mp3";
    static HopeEndingMusicSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        string creditsRequest=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/HopeCreditsText.request"));
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&File.Exists(creditsRequest))
        {
            var lines=File.ReadAllLines(creditsRequest);File.Delete(creditsRequest);string creditsOutput=lines[0];
            try
            {
                var config=AssetDatabase.LoadAssetAtPath<CampaignCinematics>("Assets/Resources/Campaign/Cinematics.asset");
                string text=string.Join("\n",lines.Skip(1));
                string missing=new string(text.Where(c=>c>=0x4e00&&c<=0x9fff&&!GameFont.Silver.HasCharacter(c)).Distinct().ToArray());
                if(missing.Length>0)throw new Exception("Font missing characters: "+missing);
                Undo.RecordObject(config,"制作人员表改编说明");config.creditsText=text;EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);
                File.WriteAllText(Path.Combine(creditsOutput,"制作人员表原文.txt"),config.creditsText);
                File.WriteAllText(Path.Combine(creditsOutput,"文案保存检查.txt"),"success; game font contains all Chinese characters");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(creditsOutput,"文案保存检查.txt"),e.ToString());}
            return;
        }
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/HopeEndingMusicSetup.request"));
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        string output=File.ReadAllText(request).Trim();File.Delete(request);GameObject root=null;
        try
        {
            AssetDatabase.ImportAsset(AudioPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(AudioPath);var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.85f;
            importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
            if(clip==null||clip.length<130||clip.length>135)throw new Exception("Unexpected score length");
            string prefab="Assets/Resources/Campaign/CampaignRoot.prefab";root=PrefabUtility.LoadPrefabContents(prefab);
            var story=root.GetComponent<StoryDirector>();var mourning=story.mourningMusic;
            story.finaleMusic=clip;story.hopeFinaleCueSeconds=64.9f;
            PrefabUtility.SaveAsPrefabAsset(root,prefab);
            if(story.mourningMusic!=mourning)throw new Exception("Mourning ending must be unchanged");
            var config=AssetDatabase.LoadAssetAtPath<CampaignCinematics>("Assets/Resources/Campaign/Cinematics.asset");
            var shots=config.routes.Where(r=>r.chapter==10&&r.ending==CampaignEnding.Hope).SelectMany(r=>r.shots).ToArray();
            if(shots.Single(s=>s.triggerHopeFinale).image.name!="CH10_D")throw new Exception("Expected door picture cue");
            var durations=EndingSequenceTiming.HopeDurations(shots.Select(s=>(double)s.Duration).ToArray(),3,story.hopeFinaleCueSeconds,clip.length,config.creditsSeconds,config.chapterAnnouncementSeconds);
            File.WriteAllText(Path.Combine(output,"原生配乐配置.txt"),"success\nclip="+clip.name+"; length="+clip.length+"; cue="+story.hopeFinaleCueSeconds+"; credits="+config.creditsSeconds+"\nshots="+string.Join(",",durations.Select(v=>v.ToString("F3"))));
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"原生配乐配置.txt"),e.ToString());Debug.LogException(e);}
        finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif

