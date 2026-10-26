using System;
using UnityEngine;

[Serializable] public sealed class MusicLyricCue
{
    public float start,end;
    public string speaker,en,zh;
}
[Serializable] public sealed class MusicLyricTrack
{
    public string id,clip;
    public MusicLyricCue[] cues=new MusicLyricCue[0];
    public MusicLyricCue At(double seconds)
    {
        int low=0,high=cues.Length-1,found=-1;
        while(low<=high){int mid=(low+high)/2;if(cues[mid].start<=seconds){found=mid;low=mid+1;}else high=mid-1;}
        return found>=0&&seconds<cues[found].end?cues[found]:null;
    }
}
[Serializable] public sealed class MusicLyricsCatalog
{
    public int version;
    public MusicLyricTrack[] tracks=new MusicLyricTrack[0];
    public MusicLyricTrack Find(string clip)
    {
        foreach(var track in tracks)if(track.clip==clip)return track;
        return null;
    }
    public static MusicLyricsCatalog Load()
    {
        var asset=Resources.Load<TextAsset>("Campaign/MusicLyrics");
        return asset!=null?JsonUtility.FromJson<MusicLyricsCatalog>(asset.text):new MusicLyricsCatalog();
    }
    public static string SpeakerFor(MusicLyricCue cue,int chapter)
    {return cue==null||(chapter>4&&cue.speaker=="Gavroche")?"":cue.speaker??"";}
}
