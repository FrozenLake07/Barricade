using UnityEngine;

// Nonlethal hits stop movement until this reaction finishes; repeated hits restart the reaction.
[DefaultExecutionOrder(300),DisallowMultipleComponent]
public sealed class CampaignReactionVisual : MonoBehaviour
{
    public enum Action { Hit,Death,Windup }
    Action mode;float elapsed;bool playing;bool landed;
    SpriteRenderer overlay;SpriteRenderer[] baseRenderers;bool[] baseEnabled;
    public bool IsPlaying=>playing;
    public bool IsHitPlaying=>isActiveAndEnabled&&playing&&mode==Action.Hit;
    public bool IsDying=>playing&&mode==Action.Death;
    void Awake()=>Ensure();
    void Ensure()
    {
        if(overlay!=null)return;
        var root=GetComponent<SpriteRenderer>();var go=new GameObject("ReactionVisual");go.transform.SetParent(transform,false);
        overlay=go.AddComponent<SpriteRenderer>();overlay.sortingLayerID=root.sortingLayerID;overlay.sortingOrder=root.sortingOrder+1;overlay.enabled=false;
    }
    Sprite[] Frames(CampaignActorArt art)
    {
        bool grenadier=GetComponent<EnemyMove>().role==EnemyMove.Role.Leader;
        return mode==Action.Windup?art.grenadierWindup:mode==Action.Death?(grenadier?art.grenadierDeath:art.soldierDeath):(grenadier?art.grenadierHit:art.soldierHit);
    }
    float FPS(CampaignActorArt art)=>GetComponent<EnemyMove>().role==EnemyMove.Role.Leader?art.grenadierFPS:art.soldierFPS;
    public bool Play(Action action)
    {
        if(IsDying||action==Action.Windup&&IsHitPlaying)return true;var art=CampaignActorArt.Current;if(art==null)return false;
        mode=action;var frames=Frames(art);if(frames==null||frames.Length==0)return false;
        Ensure();if(!playing)
        {
            baseRenderers=GetComponentsInChildren<SpriteRenderer>(true);baseEnabled=new bool[baseRenderers.Length];
            for(int i=0;i<baseRenderers.Length;i++)baseEnabled[i]=baseRenderers[i].enabled;
        }
        elapsed=0;playing=true;landed=false;
        if(action==Action.Hit)StopMovement();
        if(action==Action.Death)
        {
            var move=GetComponent<EnemyMove>();if(move!=null)move.enabled=false;
            foreach(var body in GetComponentsInChildren<Rigidbody2D>()){body.velocity=Vector2.zero;body.simulated=false;}
            foreach(var c in GetComponentsInChildren<Collider2D>())c.enabled=false;
            var animator=GetComponent<Animator>();if(animator!=null)animator.enabled=false;
        }
        Sample(art);return true;
    }
    void StopMovement()
    {
        var body=GetComponent<Rigidbody2D>();if(body!=null)body.velocity=Vector2.zero;
    }
    void FixedUpdate(){if(IsHitPlaying)StopMovement();}
    void LateUpdate()
    {
        if(!playing||CampaignSession.Suspended||DebugConsole.SimulationPaused)return;
        var art=CampaignActorArt.Current;if(art==null)return;Ensure();elapsed+=Time.deltaTime;
        Sample(art);var frames=Frames(art);float duration=frames.Length/FPS(art);
        if(mode==Action.Death)
        {
            if(!landed&&elapsed>=duration-.1f){landed=true;SfxPlayer.Play("combat_body_fall",.55f);}
            if(elapsed>=duration+art.deathHold+art.deathFade)Destroy(gameObject);
        }
        else if(elapsed>=duration)Finish();
    }
    void Sample(CampaignActorArt art)
    {
        var frames=Frames(art);if(frames==null||frames.Length==0)return;
        foreach(var renderer in GetComponentsInChildren<SpriteRenderer>(true))if(renderer!=overlay)renderer.enabled=false;
        overlay.enabled=true;overlay.sprite=frames[Mathf.Min(frames.Length-1,Mathf.FloorToInt(elapsed*FPS(art)))];
        float duration=frames.Length/FPS(art);float fade=mode==Action.Death?Mathf.Clamp01((elapsed-duration-art.deathHold)/Mathf.Max(.01f,art.deathFade)):0;
        overlay.color=new Color(1,1,1,1-fade);
    }
    public void FinishWindup(){if(playing&&mode==Action.Windup)Finish();}
    void Finish()
    {
        playing=false;overlay.enabled=false;
        if(baseRenderers!=null){for(int i=0;i<baseRenderers.Length;i++)if(baseRenderers[i]!=null&&baseRenderers[i]!=overlay)baseRenderers[i].enabled=baseEnabled[i];}
        // A restored hit reaction may have no pre-save renderer cache.
        else {var driver=GetComponent<EnemyVisualAnimation>();if(driver!=null){driver.enabled=false;driver.enabled=true;}}
    }
}


