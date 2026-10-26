using System.Collections.Generic;
using UnityEngine;
[DisallowMultipleComponent]
public sealed class SelectedSoundLoop : MonoBehaviour
{
    static readonly HashSet<SelectedSoundLoop> instances=new HashSet<SelectedSoundLoop>();
    AudioSource source;string eventId;
    public bool Active=>source!=null&&source.isPlaying;
    void OnEnable()=>instances.Add(this);
    public void Set(string id,bool active,float volume)
    {
        if(!Application.isPlaying)return;
        active=active&&SfxPlayer.CanPlay(id);
        if(!active){if(source!=null)source.Stop();return;}
        var clip=SfxPlayer.ClipFor(id);if(clip==null)return;
        if(source==null){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.priority=180;}
        EffectsVolumeGain.Set(source,volume*SfxPlayer.MasterVolume*SfxPlayer.MixGainFor(id));source.pitch=1;
        if(source.clip!=clip||eventId!=id){source.Stop();source.clip=clip;eventId=id;}
        if(!AudioListener.pause&&!source.isPlaying)source.Play();
    }
    public static void StopAll(){foreach(var item in instances)if(item!=null&&item.source!=null)item.source.Stop();}
    void OnDisable(){instances.Remove(this);if(source!=null)source.Stop();}
}
