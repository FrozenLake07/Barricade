using UnityEngine;

public class WallDamage : MonoBehaviour
{
    bool resolved;
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!GameManager.IsWall(other)) return;
        ResolveWallHit();
    }
    public void ResolveWallHit()
    {
        if(resolved)return;
        var hp=GetComponent<EnemyHP>();if(hp!=null&&hp.HP<=0)return;
        resolved = true;
        GetComponent<WaveThreat>()?.RecordImpact();
        BombHP bomb = GetComponent<BombHP>();
        if (bomb != null)
        {
            bomb.Explode(true);
            return;
        }
        SfxPlayer.Play("SFX-WAL-02", 0.6f);
        GameManager.DamageWall(GameManager.Kind.Enemy);
        Destroy(gameObject);
    }
}
