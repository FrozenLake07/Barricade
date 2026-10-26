using System;
using System.Collections.Generic;
using UnityEngine;

// User-selected fixed clips, including explicit silence for rejected events.
public static class SfxPlayer
{
    public const string Folder="SFX/";
    public static float MasterVolume=1f;
    [Serializable] public sealed class Binding {public string id,path;}
    [Serializable] sealed class Bank {public Binding[] bindings=new Binding[0];}
    static readonly Dictionary<string,string> paths=new Dictionary<string,string>();
    static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    static readonly Dictionary<string,float> lastAt=new Dictionary<string,float>();
    static AudioSource[] voices;
    static bool loaded;static int cursor;
    public static string LastEventId {get;private set;}
    public static int EmittedCount {get;private set;}
    public static event Action<string> Played;
    public static bool Suppressed=>false; // Chapter-nine silence applies to background music only.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetState(){voices=null;cursor=0;loaded=false;paths.Clear();clips.Clear();lastAt.Clear();LastEventId=null;EmittedCount=0;Played=null;}
    static void Load()
    {
        if(loaded)return;loaded=true;
        var json=Resources.Load<TextAsset>(Folder+"Selected/Bindings");
        if(json==null){Debug.LogError("[SfxPlayer] Missing selected sound bindings.");return;}
        var bank=JsonUtility.FromJson<Bank>(json.text);
        if(bank?.bindings!=null)foreach(var b in bank.bindings)paths[b.id]=b.path??"";
    }
    public static string ResourceFor(string id)
    {
        if(string.IsNullOrEmpty(id))return null;
        if(id.StartsWith("HeartbeatPulse/",StringComparison.Ordinal))return Folder+id;
        Load();return paths.TryGetValue(id,out string path)&&path.Length>0?Folder+path:null;
    }
    public static AudioClip ClipFor(string id)
    {
        string path=ResourceFor(id);if(path==null)return null;
        if(!clips.TryGetValue(path,out var clip))clips[path]=clip=Resources.Load<AudioClip>(path);
        return clip;
    }
    public static bool CanPlay(string id)
    {
        if(Suppressed)return false;
        if(id!=null&&id.StartsWith("ui_",StringComparison.Ordinal)&&StoryDirector.Active)
            return StoryDirector.Current.Phase!=CampaignPhase.Title&&StoryDirector.Current.Phase!=CampaignPhase.Calibration&&RunState.Chapter!=10;
        return true;
    }
    public static float MixGainFor(string id)=>SfxMixLevels.Gain(ResourceFor(id));
    public static void Play(string id,float volume=1f,float pitch=1f)
    {
        if(!CanPlay(id))return;
        var clip=ClipFor(id);if(clip==null)return;
        bool ui=id.StartsWith("ui_",StringComparison.Ordinal);
        float gap=ui?.08f:id=="SFX-PLY-06"?.12f:.015f;
        if(Application.isPlaying&&lastAt.TryGetValue(id,out float at)&&Time.unscaledTime-at<gap)return;
        lastAt[id]=Time.unscaledTime;LastEventId=id;EmittedCount++;Played?.Invoke(id);
        if(!Application.isPlaying)return;
        var source=NextSource();if(source==null)return;
        source.ignoreListenerPause=ui;
        source.priority=ui||id.StartsWith("SFX-QTE",StringComparison.Ordinal)||id.StartsWith("SFX-PLY",StringComparison.Ordinal)?60:110;
        source.pitch=1f;EffectsVolumeGain.Set(source,volume*MasterVolume*MixGainFor(id));source.clip=clip;source.Play();
    }
    // No random draws, variant selection or pitch changes.
    public static void PlayVariants(string id,int variants,float volume=1f,float pitchJitter=0f)=>Play(id,volume);
    public static void StopAll()
    {if(voices!=null)foreach(var s in voices)if(s!=null)s.Stop();SelectedSoundLoop.StopAll();lastAt.Clear();}
    static AudioSource NextSource()
    {
        if(voices==null||voices[0]==null)
        {
            var host=new GameObject("[Selected SFX]");UnityEngine.Object.DontDestroyOnLoad(host);voices=new AudioSource[20];
            for(int i=0;i<voices.Length;i++){var s=host.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=false;s.spatialBlend=0;voices[i]=s;}
        }
        for(int i=0;i<voices.Length;i++){int n=(cursor+i)%voices.Length;if(!voices[n].isPlaying){cursor=(n+1)%voices.Length;return voices[n];}}
        for(int i=0;i<voices.Length;i++)if(voices[i].priority>=100){voices[i].Stop();return voices[i];}
        return null;
    }
}
