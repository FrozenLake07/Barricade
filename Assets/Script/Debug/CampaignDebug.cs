using System;
using UnityEngine;

// Whitelisted, run-only overrides. Never write into ChapterTable or WaveScript assets.
public static class CampaignDebug
{
    [Serializable] public sealed class Settings
    {
        public bool nonDesign,god,freeze,crit,hideHud,muteHeartbeat;
        public int infinite=-1,cap=-1;
        public float tempo=-1,window=-1,density=1;
    }
    public static Settings Value=new Settings();
    public static bool Available=>Application.isEditor||Debug.isDebugBuild||Array.Exists(Environment.GetCommandLineArgs(),s=>s=="-console");
    public static bool Infinite(bool normal)=>Value.infinite<0?normal:Value.infinite==1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset(){Value=new Settings();}
    public static void Mark(string command)
    {
        Value.nonDesign=true;CampaignTelemetry.Current?.MarkDebug(command);
    }
    public static void Restore(string json){Value=string.IsNullOrEmpty(json)?new Settings():JsonUtility.FromJson<Settings>(json)??new Settings();}
}
