using UnityEngine;

// Player presentation preferences are local settings, independent of save slots.
public static class GamePresentationOptions
{
    public const string MusicKey="Barricade.MusicVolume", EffectsKey="Barricade.EffectsVolume", HeartbeatKey="Barricade.HeartbeatVolume", SubtitleKey="Barricade.SubtitleScale", MasterKey="Barricade.MasterVolume";
    static float Read(string key,float fallback,float min,float max)
    {
        float value=PlayerPrefs.GetFloat(key,fallback);
        return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max);
    }
    static void Write(string key,float value,float min,float max)
    {if(float.IsNaN(value)||float.IsInfinity(value))return;PlayerPrefs.SetFloat(key,Mathf.Clamp(value,min,max));PlayerPrefs.Save();}
    public static float MasterVolume{get=>Read(MasterKey,.8f,0,1);set{Write(MasterKey,value,0,1);AudioListener.volume=MasterVolume;}}
    public static float MusicVolume{get=>Read(MusicKey,1,0,1);set=>Write(MusicKey,value,0,1);}
    public static float EffectsVolume{get=>Read(EffectsKey,1,0,1);set=>Write(EffectsKey,value,0,1);}
    public static float HeartbeatVolume{get=>Read(HeartbeatKey,1,0,1);set=>Write(HeartbeatKey,value,0,1);}
    public static float SubtitleScale{get=>Read(SubtitleKey,1,1,1.5f);set=>Write(SubtitleKey,value,1,1.5f);}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Restore()=>AudioListener.volume=MasterVolume;
}
