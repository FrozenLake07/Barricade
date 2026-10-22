using System;

// A finite smooth fade; changing targets continues from the current audible gain.
public sealed class MusicVolumeEnvelope
{
    public float Value {get;private set;}
    public float Target {get;private set;}
    float start,elapsed,duration;
    static float Gain(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(1,value));
    public void Snap(float value){Value=Target=start=Gain(value);elapsed=duration=0;}
    public void Retarget(float value,float seconds)
    {
        value=Gain(value);if(value==Target)return;
        start=Value;Target=value;elapsed=0;
        duration=float.IsNaN(seconds)||float.IsInfinity(seconds)?0:Math.Max(0,seconds);
        if(duration==0)Snap(value);
    }
    public float Advance(float seconds)
    {
        if(duration<=0||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0)return Value;
        elapsed=Math.Min(duration,elapsed+seconds);float t=elapsed/duration;
        Value=start+(Target-start)*(t*t*(3-2*t));
        if(elapsed>=duration){Value=Target;duration=0;}
        return Value;
    }
    public static bool QuietPhase(CampaignPhase phase)=>phase==CampaignPhase.Preparation||phase==CampaignPhase.Defense
        ||phase==CampaignPhase.Choice||phase==CampaignPhase.Epilogue||phase==CampaignPhase.LastChance||phase==CampaignPhase.EndCard;
}
