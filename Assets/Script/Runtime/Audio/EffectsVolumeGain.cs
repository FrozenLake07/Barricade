using UnityEngine;

// Keeps active effects and narration responsive to the player's effects volume.
public sealed class EffectsVolumeGain : MonoBehaviour
{
    AudioSource source;
    float baseVolume=1;
    public static void Set(AudioSource source,float level)
    {
        if(source==null)return;
        EffectsVolumeGain found=null;
        foreach(var item in source.GetComponents<EffectsVolumeGain>())if(item.source==source){found=item;break;}
        if(found==null){found=source.gameObject.AddComponent<EffectsVolumeGain>();found.source=source;}
        found.baseVolume=Mathf.Clamp01(level);found.Apply();
    }
    void Apply(){if(source!=null)source.volume=baseVolume*GamePresentationOptions.EffectsVolume;}
    void LateUpdate()=>Apply();
}
