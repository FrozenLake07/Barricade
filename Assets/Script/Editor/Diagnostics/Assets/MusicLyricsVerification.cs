#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MusicLyricsVerification
{
    [Serializable] sealed class Report {public bool success;public int tracks,cues;public string error,missingCharacters,fontPath;public int checkedCharacters;public List<string> clips=new List<string>();}
    static MusicLyricsVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/MusicLyricsCheck.request"));
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        string output=File.ReadAllText(request).Trim();File.Delete(request);var report=new Report();
        try
        {
            AssetDatabase.ImportAsset("Assets/Resources/Campaign/MusicLyrics.json",ImportAssetOptions.ForceSynchronousImport);
            var data=MusicLyricsCatalog.Load();report.tracks=data.tracks.Length;
            var story=Resources.Load<GameObject>("Campaign/CampaignRoot").GetComponent<StoryDirector>();
            var configured=story.table.chapters.Select(c=>c.openingMusic).Concat(new[]{story.mourningMusic,story.finaleMusic}).Where(c=>c!=null).Distinct().ToArray();
            foreach(var track in data.tracks)
            {
                var audio=configured.Single(c=>c.name==track.clip);
                var path=AssetDatabase.GetAssetPath(audio);float end=0;
                foreach(var cue in track.cues)
                {
                    if(cue.start<end-.001||cue.end<=cue.start||cue.end>audio.length+.05)throw new Exception(track.clip+" invalid cue "+cue.start+"–"+cue.end+" / clip "+audio.length);
                    if(string.IsNullOrWhiteSpace(cue.zh)||string.IsNullOrWhiteSpace(cue.en))throw new Exception("Missing bilingual line");
                    if(cue.speaker.Any(c=>c>127))throw new Exception("Speaker must use English name");
                    if(track.id!="chapter-4"&&cue.speaker=="Gavroche")throw new Exception("Gavroche label outside chapter four");
                    end=cue.end;report.cues++;
                }
                report.clips.Add(track.id+": "+path+"; "+audio.length+" sec");
            }
            string all=string.Concat(data.tracks.SelectMany(t=>t.cues).Select(c=>c.zh+c.en+c.speaker));
            report.fontPath=AssetDatabase.GetAssetPath(GameFont.Silver);
            var mapped=FontCodepoints(report.fontPath);report.checkedCharacters=all.Where(c=>!char.IsWhiteSpace(c)).Distinct().Count();
            report.missingCharacters=new string(all.Where(c=>!char.IsWhiteSpace(c)&&(!mapped.Contains(c)||!GameFont.Silver.HasCharacter(c))).Distinct().ToArray());
            if(report.missingCharacters.Length>0)throw new Exception("Game font missing characters: "+report.missingCharacters);
            for(int i=0;i<7;i++)if(data.Find(story.table.chapters[i].openingMusic.name)==null)throw new Exception("Chapter not covered: "+(i+1));
            if(data.Find(story.table.chapters[7].openingMusic.name)!=null)throw new Exception("Instrumental chapter eight must not have lyrics");
            if(data.Find(story.mourningMusic.name)==null||data.Find(story.finaleMusic.name)==null)throw new Exception("Ending not covered");
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();}
        File.WriteAllText(Path.Combine(output,"原生字幕资源检查.json"),JsonUtility.ToJson(report,true));
    }
    // Dynamic Font.HasCharacter alone can accept fallback characters that the shipped TTF does not contain.
    static HashSet<int> FontCodepoints(string path)
    {
        byte[] bytes=File.ReadAllBytes(path);
        Func<int,int> u16=o=>(bytes[o]<<8)|bytes[o+1];
        Func<int,int> u32=o=>checked((int)(((uint)bytes[o]<<24)|((uint)bytes[o+1]<<16)|((uint)bytes[o+2]<<8)|bytes[o+3]));
        int cmap=-1;
        for(int i=0;i<u16(4);i++){int row=12+i*16;if(bytes[row]=='c'&&bytes[row+1]=='m'&&bytes[row+2]=='a'&&bytes[row+3]=='p'){cmap=u32(row+8);break;}}
        if(cmap<0)throw new Exception("Font has no cmap");
        for(int i=0;i<u16(cmap+2);i++)
        {
            int sub=cmap+u32(cmap+8+i*8);if(u16(sub)!=12)continue;
            var points=new HashSet<int>();
            for(int group=0;group<u32(sub+12);group++)
            {
                int entry=sub+16+group*12,start=u32(entry),end=u32(entry+4),glyph=u32(entry+8);
                for(int cp=start;cp<=end;cp++)if(glyph+cp-start!=0)points.Add(cp);
            }
            return points;
        }
        throw new Exception("Expected format-12 Unicode cmap in the bundled Silver font");
    }

}
#endif
