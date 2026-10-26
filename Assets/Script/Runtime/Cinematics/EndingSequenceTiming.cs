using System;

// Stretch only images after the musical cue; reserve the final music segment for credits.
public static class EndingSequenceTiming
{
    public static double[] Durations(double[] authored,int cue,double musicSeconds,double creditsSeconds)
    {
        var result=(double[])authored.Clone();
        if(cue<0||cue>=result.Length||musicSeconds<=0)return result;
        double original=0;for(int i=cue;i<result.Length;i++)original+=result[i];
        double scale=Math.Max(1,(musicSeconds-Math.Max(0,creditsSeconds))/Math.Max(.1,original));
        for(int i=cue;i<result.Length;i++)result[i]*=scale;
        return result;
    }
    // Align both musical sections to a continuous score, including time already spent on the chapter card.
    public static double[] HopeDurations(double[] authored,int cue,double cueSeconds,double musicSeconds,double creditsSeconds,double elapsed)
    {
        var result=(double[])authored.Clone();
        if(cue<=0||cue>=result.Length||musicSeconds<=0||cueSeconds<=elapsed||cueSeconds>=musicSeconds)return result;
        Fit(result,0,cue,cueSeconds-Math.Max(0,elapsed));
        Fit(result,cue,result.Length,Math.Max(.1*(result.Length-cue),musicSeconds-cueSeconds-Math.Max(0,creditsSeconds)));
        return result;
    }
    static void Fit(double[] values,int start,int end,double seconds)
    {
        double sum=0;for(int i=start;i<end;i++)sum+=values[i];
        if(sum<=0)return;
        for(int i=start;i<end;i++)values[i]*=seconds/sum;
    }
}
