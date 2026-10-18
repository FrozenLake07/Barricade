using UnityEngine;

public class BulletHit : MonoBehaviour
{
    bool spent;
    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;
        ArrowDefinition def = GetComponent<ArrowDefinition>();
        if (def == null) return;
        if (GameManager.IsWall(other))
        {
            if (def.fromPlayer) return;
            spent = true;
            SfxPlayer.Play("SFX-WAL-01", 0.35f);
            GameManager.DamageWall(GameManager.Kind.Bullet);
            Destroy(gameObject);
            return;
        }
        // 敌弹从自己的碰撞体内生成，只结算街垒，避免被自己吃掉。
        if (!def.fromPlayer || !TryDamage(other, def)) return;
        spent = true;
        // 只有实际命中合法目标的 Perfect 弹才能取消它自己的换弹等待。
        if (def.perfectShot && def.playerShooter != null)
            def.playerShooter.OnPerfectHit(def.playerShotId);
        Destroy(gameObject);
    }

    bool TryDamage(Collider2D other, ArrowDefinition def)
    {
        if (def.targetKind == ArrowDefinition.TargetKind.Enemy)
        {
            EnemyHP enemy = other.GetComponent<EnemyHP>();
            if (enemy == null || enemy.HP <= 0
                || Mathf.Abs(other.transform.position.y - def.trackY) > def.trackRange) return false;
            enemy.HP -= def.Damage;
            if (enemy.HP <= 0) Destroy(enemy.gameObject);
            SfxPlayer.Play("SFX-ENM-02", 0.45f);
            if (def.perfectShot) SfxPlayer.Play("SFX-QTE-01", 0.4f);
            return true;
        }
        BombHP bomb = other.GetComponent<BombHP>();
        if (bomb == null || !bomb.IsAlive
            || Mathf.Abs(bomb.AttackTrackY - def.trackY) > def.trackRange) return false;
        bomb.TakeDamage(def.Damage);
        if (bomb.IsAlive) SfxPlayer.Play("SFX-BMB-03", 0.5f);
        return true;
    }
}
