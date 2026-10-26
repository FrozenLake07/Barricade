using UnityEngine;

public sealed class CampaignActorArt : ScriptableObject
{
    public Sprite gavrocheIdle;
    public Sprite[] gavrocheRun,gavrochePick,soldierHit,soldierDeath,grenadierHit,grenadierDeath,grenadierWindup;
    public float childFPS=12,soldierFPS=10,grenadierFPS=12,deathHold=.6f,deathFade=.3f;
    static CampaignActorArt cached;
    public static CampaignActorArt Current=>cached!=null?cached:cached=Resources.Load<CampaignActorArt>("CampaignActors/ActorArt");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>cached=null;
}
