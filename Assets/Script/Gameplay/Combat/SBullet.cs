using UnityEngine;

public class SBullet : MonoBehaviour
{
    public event System.Action<bool, bool, int> ShotFired;
    [Header("主角动画枪口（由自动配置填写）")]
    public Transform groundMuzzle, upMuzzle;
    bool activeAimUp;
    Vector2 ShotOrigin => activeAimUp
        ? (upMuzzle != null ? (Vector2)upMuzzle.position : (Vector2)transform.position)
        : (groundMuzzle != null ? (Vector2)groundMuzzle.position : (Vector2)transform.position);

    private GameObject player;
    GameObject bulletPrefab;

    [Header("开火")]
    [Tooltip("子弹的直线速度")]
    public float bulletSpeed = 20f;
    [Tooltip("判定目标在攻击轨道上的 y 容差")]
    public float sameTrackRange = 1.25f;

    [Min(0f)]
    [Tooltip("每次真正发射后的换弹秒数；该发 Perfect 弹实际命中后立即取消")]
    public float reloadTime = 1f;
    float reloadReadyTime;
    int latestShotId;
    bool wasReloading;
    public bool IsReloading => Time.timeSinceLevelLoad < reloadReadyTime;
    public float ReloadRemaining => Mathf.Max(0f, reloadReadyTime - Time.timeSinceLevelLoad);
    public bool CanFire => bulletPrefab != null && !CapsuleTrack.IsSwitching && !IsReloading;

    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        bulletPrefab = Resources.Load<GameObject>("arrow/Bullet");
        reloadReadyTime = 0f;
        latestShotId = 0;
        if (bulletPrefab == null)
            Debug.LogWarning("[SBullet] 缺少 arrow/Bullet 预制体，无法开枪。");
    }

    // 由 TapSec 调用：dir = "Up" / "Down"，perf = 是否 Perfect
    //    Up   —— 只瞄攻击轨道上的手榴弹
    //    Down —— 只瞄攻击轨道上离玩家最近的敌人
    // 两种情况都是在玩家处开火，子弹带着预判朝目标飞
    public void Fire(string dir, bool perf)
    {
        if (!CanFire) return;
        activeAimUp = dir == "Up";
        int dam = perf ? 100 : 50;   // 和 TapSec 一致的 perfecttime 奖励

        // ————— Up：只瞄手榴弹 —————
        if (dir == "Up")
        {
            BombHP bomb = PickBomb();
            if (bomb == null) { Penalty(); return; }

            Shoot(AimAtBomb(bomb), dam,
                  ArrowDefinition.TargetKind.Bomb, perf);
            return;
        }

        // ————— Down（以及其他状态）：只瞄敌人 —————
        EnemyHP enemy = PickEnemy();
        if (enemy == null) { Penalty(); return; }

        Shoot(AimAt(enemy.transform.position, VelocityOf(enemy.gameObject)), dam,
              ArrowDefinition.TargetKind.Enemy, perf);
    }

    // 当前会锁定谁 —— 给准星（TargetMarker）用。
    // 这里的分支必须和 Fire 里一模一样，否则准星指的和实际打的会对不上。
    public Transform CurrentTarget()
    {
        if (TapSec.ShootStatu == "Up")
        {
            BombHP bomb = PickBomb();
            return bomb != null ? bomb.transform : null;
        }

        EnemyHP enemy = PickEnemy();
        return enemy != null ? enemy.transform : null;
    }

    // ————— 目标选择 —————

    // 攻击轨道上离玩家最近的敌人
    EnemyHP PickEnemy()
    {
        EnemyHP best = null;
        float bestDist = float.MaxValue;

        foreach (EnemyHP e in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))
        {
            if (e == null || e.HP <= 0) continue;
            if (!OnAttackTrack(e.transform.position.y)) continue;

            float dist = Vector2.Distance(e.transform.position, transform.position);
            if (dist < bestDist) { best = e; bestDist = dist; }
        }
        return best;
    }

    // 攻击轨道上离玩家最近的手榴弹
    BombHP PickBomb()
    {
        BombHP best = null;
        float bestDist = float.MaxValue;

        foreach (BombHP b in FindObjectsByType<BombHP>(FindObjectsSortMode.None))
        {
            if (b == null || !b.IsAlive) continue;
            if (!OnAttackTrack(b.AttackTrackY)) continue;

            float dist = Vector2.Distance(b.transform.position, transform.position);
            if (dist < bestDist) { best = b; bestDist = dist; }
        }
        return best;
    }

    bool OnAttackTrack(float y)
    {
        return Mathf.Abs(y - CapsuleTrack.AttackY) <= sameTrackRange;
    }

    // ————— 预判 —————

    // 能预判就朝预判点打；预判不出来（追不上）就直接打它当前的位置
    Vector2 AimAt(Vector2 targetPos, Vector2 targetVel)
    {
        return TryLead(targetPos, targetVel, out Vector2 lead)
               ? lead
               : targetPos - ShotOrigin;
    }

    // 慢速手榴弹的飞行时间更长，瞄准必须把重力加速度一起算进去。
    Vector2 AimAtBomb(BombHP bomb)
    {
        Rigidbody2D body = bomb.GetComponent<Rigidbody2D>();
        Vector2 velocity = body != null ? body.velocity : Vector2.zero;
        Vector2 acceleration = body != null ? Physics2D.gravity * body.gravityScale : Vector2.zero;
        Vector2 distance = (Vector2)bomb.transform.position - ShotOrigin;
        float flightTime = distance.magnitude / Mathf.Max(0.01f, bulletSpeed);
        Vector2 aim = distance;
        for (int i = 0; i < 12; i++)
        {
            aim = distance + velocity * flightTime + 0.5f * acceleration * flightTime * flightTime;
            flightTime = aim.magnitude / Mathf.Max(0.01f, bulletSpeed);
        }
        return aim;
    }
    static Vector2 VelocityOf(GameObject go)
    {
        if (go == null) return Vector2.zero;
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        return rb != null ? rb.velocity : Vector2.zero;
    }

    // 预判拦截：我们的子弹重力为 0，是直线匀速；目标按当前速度外推，
    // 解 |d + v·t| = bulletSpeed·t 取最小正根 t，返回瞄准那一刻目标的方向。
    // 目标静止时 v = 0，会自然退化成"直接打当前位置"
    bool TryLead(Vector2 targetPos, Vector2 targetVel, out Vector2 aimDir)
    {
        aimDir = Vector2.zero;

        Vector2 p0 = ShotOrigin;
        Vector2 d = targetPos - p0;
        Vector2 v = targetVel;

        float s = bulletSpeed;
        float a = v.sqrMagnitude - s * s;
        float b = 2f * Vector2.Dot(d, v);
        float c = d.sqrMagnitude;

        float t;
        if (Mathf.Abs(a) < 1e-4f)              // 退化成一次方程
        {
            if (Mathf.Abs(b) < 1e-4f) return false;
            t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;       // 判别式小于 0，追不上

            float sq = Mathf.Sqrt(disc);
            float t1 = (-b - sq) / (2f * a);
            float t2 = (-b + sq) / (2f * a);
            t = (t1 > 0f && t2 > 0f) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
        }

        if (t <= 0f) return false;

        Vector2 hit = targetPos + v * t;
        aimDir = hit - p0;
        return aimDir.sqrMagnitude > 1e-4f;
    }

    // 攻击轨道上没有可打的目标时的惩罚：这一拍已经被 TapSec 在按下时消耗过了，
    // 这里再压一档节拍作为额外惩罚
    void Penalty()
    {
        if (!RhythmManager.IsFollowingMusic)
            TapSec.Setbeattime = Mathf.Max(0.5f, TapSec.Setbeattime - 0.05f);
        SfxPlayer.Play("SFX-PLY-02", 0.4f);
        Debug.Log("[SBullet] 攻击轨道上没有可打的目标，本次无法射击；音乐模式不改变拍点。");
    }

    // 生成子弹：在玩家处射出，方向朝向目标 —— 子弹的 rotation 也就跟着指向目标。
    // kind 决定这颗子弹只伤哪一类目标（严格类型隔离）
    void Shoot(Vector2 dir, int dam, ArrowDefinition.TargetKind kind, bool perf)
    {
        if (bulletPrefab == null) return;

        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.left;
        dir = dir.normalized;

        var t = Instantiate(bulletPrefab, (Vector3)ShotOrigin,
                            Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
        latestShotId++;
        reloadReadyTime = Time.timeSinceLevelLoad + Mathf.Max(0f, reloadTime);

        var rb = t.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = dir * bulletSpeed;

        var ad = t.GetComponent<ArrowDefinition>();
        if (ad != null)
        {
            ad.Damage = dam;
            ad.fromPlayer = true;                    // 标记成玩家的子弹，BulletHit 才会结算伤害
            ad.trackY = CapsuleTrack.AttackY;        // 锁定攻击轨道：只打这条轨道上的目标
            ad.trackRange = sameTrackRange;
            ad.targetKind = kind;                    // 锁定目标类型：只伤这一类
            ad.playerShooter = this;
            ad.playerShotId = latestShotId;
            ad.perfectShot = perf;
        }
        SfxPlayer.PlayVariants("SFX-PLY-01", 3, 0.5f, 0.03f);
        SfxPlayer.Play("SFX-PLY-03", 0.25f);
        ShotFired?.Invoke(kind == ArrowDefinition.TargetKind.Bomb, perf, latestShotId);
    }

    public void OnPerfectHit(int shotId)
    {
        // 远处旧子弹可能在下一发射出后才命中，不能替新的一发免换弹。
        if (shotId == latestShotId)
            reloadReadyTime = Time.timeSinceLevelLoad;
    }

    void Update()
    {
        if (player == null) return;
        transform.position = player.transform.position;   // 炮口跟着玩家
        if (wasReloading && !IsReloading) SfxPlayer.Play("SFX-PLY-04", 0.35f);
        wasReloading = IsReloading;
    }
}
