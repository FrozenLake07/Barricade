using System;
using System.Collections.Generic;
using UnityEngine;

public static class SfxMixLevels
{
    [Serializable] public sealed class ClipGain {public string path;public float gain=1;}
    [Serializable] sealed class Settings {public float master=.85f;public ClipGain[] clips=Array.Empty<ClipGain>();}
    static readonly Dictionary<string,float> gains=new Dictionary<string,float>();
    static string cached;static float master=.85f;
    public static float Gain(string path)
    {
        if(string.IsNullOrEmpty(path))return 0;
        // Preserve the rhythmic reference independently of the effects balance.
        if(path.StartsWith("SFX/HeartbeatPulse/",StringComparison.Ordinal))return 1;
        var asset=Resources.Load<TextAsset>("SFX/Selected/MixLevels");
        if(asset!=null&&cached!=asset.text)
        {
            cached=asset.text;gains.Clear();master=.85f;
            try
            {
                var data=JsonUtility.FromJson<Settings>(cached);if(data==null)throw new FormatException("Empty mix");
                master=Mathf.Clamp01(data.master);
                foreach(var clip in data.clips??Array.Empty<ClipGain>())if(clip!=null&&!string.IsNullOrEmpty(clip.path))gains[clip.path]=Mathf.Clamp(clip.gain,0,2);
            }
            catch(Exception e){Debug.LogWarning("[SfxMix] 使用默认音效平衡："+e.Message);}
        }
        return master*(gains.TryGetValue(path,out float gain)?gain:1);
    }
}
