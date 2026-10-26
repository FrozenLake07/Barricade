using UnityEngine;

// Sole runtime writer of BGM volume. Heartbeat, SFX, master volume and clip position are independent.
public sealed class CampaignMusicFader : MonoBehaviour
{
    readonly MusicVolumeEnvelope fade=new MusicVolumeEnvelope();
    StoryDirector owner;
    AudioSource source;
    bool voiceActive,animationActive;
    public void Initialize(StoryDirector director,AudioSource music)
    {owner=director;source=music;SnapToPhase();}
    float Target
    {
        get
        {
            float level=(owner.PreparationHandoff||animationActive||MusicVolumeEnvelope.QuietPhase(owner.Phase))?owner.gameplayMusicVolume:owner.cinematicMusicVolume;
            return Mathf.Clamp01(level)*(voiceActive?.5f:1f);
        }
    }
    public void SetVoiceActive(bool active){voiceActive=active;}
    public void SetAnimationActive(bool active){animationActive=active;}
    public void FadeToPhase()
    {
        if(owner==null||source==null)return;
        float target=Target;fade.Retarget(target,target<fade.Value?owner.gameplayMusicFadeSeconds:owner.musicRecoveryFadeSeconds);
    }
    public void SnapToPhase()
    {
        if(owner==null||source==null)return;
        fade.Snap(Target);source.volume=fade.Value*GamePresentationOptions.MusicVolume;
    }
    void LateUpdate()
    {
        if(owner==null||source==null)return;
        if(!CampaignSession.Suspended && !GameSettingsView.IsOpen){FadeToPhase();fade.Advance(Time.unscaledDeltaTime);}
        source.volume=fade.Value*GamePresentationOptions.MusicVolume;
    }
}
