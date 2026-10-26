using UnityEngine;

[DefaultExecutionOrder(300),DisallowMultipleComponent]
public sealed class GavrocheVisual : MonoBehaviour
{
    float animationTime,pickTime;bool wasPicking;
    public bool IsPicking {get;private set;}
    public int Frame {get;private set;}
    void LateUpdate()
    {
        if(CampaignSession.Suspended||DebugConsole.SimulationPaused)return;
        var art=CampaignActorArt.Current;var actor=GetComponent<CampaignActor>();if(art==null||actor==null||actor.IsResolved)return;
        animationTime+=Time.deltaTime;float progress=actor.JourneyProgress;IsPicking=progress>=.42f&&progress<.58f;
        if(IsPicking){if(!wasPicking){pickTime=0;if(!CampaignSession.Restoring)SfxPlayer.Play("character_pickup_ammunition",.6f);}pickTime+=Time.deltaTime;}
        var frames=IsPicking?art.gavrochePick:art.gavrocheRun;if(frames==null||frames.Length==0)return;
        Frame=IsPicking?Mathf.Min(frames.Length-1,Mathf.FloorToInt(pickTime*art.childFPS)):Mathf.FloorToInt(animationTime*art.childFPS)%frames.Length;
        var renderer=GetComponent<SpriteRenderer>();renderer.sprite=frames[Frame];renderer.color=new Color(1,1,1,actor.VisualOpacity);renderer.flipX=progress<.58f;
        wasPicking=IsPicking;
    }
}
