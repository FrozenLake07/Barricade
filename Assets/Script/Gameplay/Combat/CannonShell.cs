using UnityEngine;

/// 炮弹不属于手榴弹组，不触发其连锁；只在实际撞墙时结算百分比伤害。
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
[DisallowMultipleComponent]
public sealed class CannonShell : MonoBehaviour
{
    public int HP = 100;
    [Range(0f, 1f)] public float wallDamageFraction = .25f;
    [Min(.1f)] public float lifetime = 5f;
    Rigidbody2D body;
    CircleCollider2D hitbox;
    float trackY, expiresAt;
    bool resolved, launched;
    public bool IsAlive => launched && !resolved && HP > 0;
    public float AttackTrackY => trackY;

    void Awake() { body = GetComponent<Rigidbody2D>(); hitbox = GetComponent<CircleCollider2D>(); }

    public void Launch(Vector2 direction, float speed, float fraction, float laneY)
    {
        trackY = laneY;
        wallDamageFraction = Mathf.Clamp01(fraction);
        expiresAt = Time.timeSinceLevelLoad + Mathf.Max(.1f, lifetime);
        launched = true;
        body.gravityScale = 0f;
        body.velocity = direction.normalized * Mathf.Max(1f, speed);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        SfxPlayer.PlayVariants("SFX-ART-03", 2, .6f);
    }

    public static int CalculateWallDamage(int maximumHP, float fraction)
    {
        // 与GameManager最大值一致；不按剩余血量递减，不借用Bomb固定50点伤害。
        return Mathf.CeilToInt(Mathf.Max(0, maximumHP) * Mathf.Clamp01(fraction));
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive || damage <= 0) return;
        HP = Mathf.Max(0, HP - damage);
        if (HP == 0){WaveDirector.Current?.RecordShellInterception();Finish(false);}
    }

    void FixedUpdate()
    {
        if (!IsAlive) return;
        if (Time.timeSinceLevelLoad >= expiresAt) { Finish(false); return; }
        Vector2 distance = body.velocity * Time.fixedDeltaTime;
        float radius = hitbox.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        // 高速Trigger只靠离散回调可能穿墙，额外扫过本物理步的完整路程。
        foreach (var hit in Physics2D.CircleCastAll(body.position, radius, distance.normalized, distance.magnitude))
        {
            if (hit.collider == null || !GameManager.IsWall(hit.collider)) continue;
            body.position = hit.centroid;
            Finish(true);
            break;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsAlive && GameManager.IsWall(other)) Finish(true);
    }

    public void ResolveFinaleIntercept(){if(IsAlive)Finish(false);}
    public void ResolveWaveImpact(){if(IsAlive)Finish(true);}

    void Finish(bool hitWall)
    {
        if (resolved) return;
        resolved = true;
        body.velocity = Vector2.zero;
        hitbox.enabled = false;
        if (hitWall)
        {
            GetComponent<WaveThreat>()?.RecordImpact();
            int damage = CalculateWallDamage(GameManager.WallMaxHP, wallDamageFraction);
            GameManager.DamageWallAmount(damage, true);
            SfxPlayer.PlayVariants("SFX-ART-04", 2, 1f);
        }
        Destroy(gameObject);
    }
}
