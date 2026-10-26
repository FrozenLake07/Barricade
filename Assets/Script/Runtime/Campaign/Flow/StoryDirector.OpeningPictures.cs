using System;
using System.Collections;

// Picture-only timing. The music cursor and authored minimum shot durations are unchanged.
public static class ChapterOpeningPictureTiming
{
    public static double[] Extend(double[] authored,double musicRemaining)
    {
        if(authored==null||authored.Length==0)throw new ArgumentException("Opening pictures need durations.");
        double total=0;foreach(double seconds in authored){if(seconds<=0||double.IsNaN(seconds)||double.IsInfinity(seconds))throw new ArgumentException("Invalid picture duration.");total+=seconds;}
        var result=(double[])authored.Clone();
        if(double.IsNaN(musicRemaining)||double.IsInfinity(musicRemaining)||musicRemaining<=total)return result;
        double extra=musicRemaining-total,allocated=0;
        for(int i=0;i<result.Length;i++)
        {
            double add=i==result.Length-1?extra-allocated:extra*authored[i]/total;
            result[i]+=add;allocated+=add;
        }
        return result;
    }
}

public sealed partial class StoryDirector
{
    double[] OpeningPictureDurations(CinematicRoute route,string key)
    {
        if(key!="chapter-entry"||route.chapter<3||route.chapter>8||route.shots==null||route.shots.Length==0)return null;
        if(audioSource==null||audioSource.clip==null||!musicPlaybackActive||audioSource.clip!=ChapterMusicClip(route.chapter))return null;
        var authored=new double[route.shots.Length];for(int i=0;i<authored.Length;i++)authored[i]=route.shots[i].Duration;
        return ChapterOpeningPictureTiming.Extend(authored,Math.Max(0,audioSource.clip.length-audioSource.time));
    }
}
