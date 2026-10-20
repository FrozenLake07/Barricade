using UnityEngine;

public class BulletHit : MonoBehaviour
{
    bool spent;
    readonly System.Collections.Generic.HashSet<int> hitTargets = new System.Collections.Generic.HashSet<int>();
    public System.Collections.Generic.List<string> CaptureSaveTargets(System.Func<UnityEngine.Object,string> reference)
    {
        var result=new System.Collections.Generic.List<string>();
        foreach(var transform in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if(hitTargets.Contains(transform.GetInstanceID()))result.Add(reference(transform));
        return result;
    }
    public void RestoreSaveTargets(System.Collections.Generic.List<string> targets,System.Func<string,UnityEngine.Object> resolve)
    {
        hitTargets.Clear();if(targets==null)return;
        foreach(string key in targets){var transform=resolve(key) as Transform;if(transform!=null)hitTargets.Add(transform.GetInstanceID());}
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (spent || hitTargets.Contains(other.transform.GetInstanceID())) return;
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
        hitTargets.Add(other.transform.GetInstanceID());
        // 只有实际命中合法目标的 Perfect 弹才能取消它自己的换弹等待。
        if (def.perfectShot && def.playerShooter != null)
            def.playerShooter.OnPerfectHit(def.playerShotId);
        if (def.pierceRemaining > 0 && def.targetKind == ArrowDefinition.TargetKind.Enemy) { def.pierceRemaining--; return; }
        spent = true;
        Destroy(gameObject);
    }

    bool TryDamage(Collider2D other, ArrowDefinition def)
    {
        if (def.targetKind == ArrowDefinition.TargetKind.Enemy)
        {
            EnemyHP enemy = other.GetComponent<EnemyHP>();
            if (enemy == null || enemy.HP <= 0
                || Mathf.Abs(other.transform.position.y - def.trackY) > def.trackRange) return false;
            other.GetComponent<CampaignActor>()?.HitByPlayer();
            enemy.HP -= def.Damage;
            if (enemy.HP <= 0) Destroy(enemy.gameObject);
            SfxPlayer.Play("SFX-ENM-02", 0.45f);
            if (def.perfectShot) SfxPlayer.Play("SFX-QTE-01", 0.4f);
            return true;
        }
        CannonShell shell = other.GetComponent<CannonShell>();
        if (shell != null)
        {
            if (!shell.IsAlive || Mathf.Abs(shell.AttackTrackY - def.trackY) > def.trackRange) return false;
            shell.TakeDamage(def.Damage);
            if (def.perfectShot) SfxPlayer.Play("SFX-QTE-01", .4f);
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
