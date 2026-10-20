using UnityEngine;

// 给战役敌弹提供可拦截标记，仍由原BulletHit处理撞墙的一点伤害。
public sealed class CampaignProjectile : MonoBehaviour
{
    public int HP=100;
    public float trackY;
    public bool IsAlive=>HP>0;
    public void TakeDamage(int damage){if(!IsAlive)return;HP-=Mathf.Max(0,damage);if(HP<=0)Destroy(gameObject);}
}
