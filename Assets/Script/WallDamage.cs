using UnityEngine;

public class WallDamage : MonoBehaviour
{
    bool resolved;
    void OnTriggerEnter2D(Collider2D other)
    {
        if (resolved || !GameManager.IsWall(other)) return;
        resolved = true;
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
