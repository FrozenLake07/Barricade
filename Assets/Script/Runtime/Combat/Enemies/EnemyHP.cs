using UnityEngine;

public class EnemyHP : MonoBehaviour
{
    public int HP = 100;
    public void TakeDamage(int amount)
    {
        if(HP<=0||amount<=0)return;HP=Mathf.Max(0,HP-amount);
        var reaction=GetComponent<CampaignReactionVisual>();
        if(reaction!=null)reaction.Play(HP<=0?CampaignReactionVisual.Action.Death:CampaignReactionVisual.Action.Hit);
        else if(HP<=0)Destroy(gameObject);
    }
}
