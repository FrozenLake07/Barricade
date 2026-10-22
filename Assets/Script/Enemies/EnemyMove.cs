using System.Collections.Generic;
using UnityEngine;

public class EnemyMove : MonoBehaviour
{
    public enum Role { Soldier, Leader }
    public Role role = Role.Soldier;
    [Header("移动")]
    public float speed = 0.5f;
    [HideInInspector] public bool waveControlled;
    public float settleTime = 2.5f;
    public float bulletSpeed = 20f;
    [Header("Leader 投弹")]
    public float bombSpeed = 4f;
    [Tooltip("至少先推进这段距离；射程不足时继续推进，避免慢弹够不到街垒")]
    public float leaderAdvanceDistance = 1.5f;
    public float leaderWindup = 1f;
    public float leaderRoundInterval = 3.5f;
    public float leaderRecovery = 1f;
    public int leaderRoundCount = 3;
    public Transform target;
    [HideInInspector] public int groupId;

    Rigidbody2D rb;
    GameObject bulletPrefab, bombPrefab;
    float birthTime;
    Vector3 birthPosition;
    int actedCycle = -1;
    LeaderSquad squad;
    EnemyVisualAnimation visuals;
    bool pendingFire;
    float pendingFireAt;
    static readonly Dictionary<int, LeaderSquad> squads = new Dictionary<int, LeaderSquad>();

    public static bool LeaderBusy
    {
        get
        {
            foreach (LeaderSquad group in squads.Values)
                if (group.Busy) return true;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRun() => squads.Clear();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletPrefab = Resources.Load<GameObject>("arrow/Bullet");
        bombPrefab = Resources.Load<GameObject>("arrow/Bomb");
        birthTime = Time.timeSinceLevelLoad;
        birthPosition = transform.position;
        visuals = GetComponent<EnemyVisualAnimation>();
        if (visuals == null) visuals = gameObject.AddComponent<EnemyVisualAnimation>();
    }

    public void ConfigureSpawn(int id, Transform wall)
    {
        groupId = id;
        target = wall;
        if (role != Role.Leader || waveControlled) return;
        if (!squads.TryGetValue(id, out squad))
        {
            squad = new LeaderSquad();
            squads.Add(id, squad);
        }
        squad.members.Add(this);
    }

    void Start()
    {
        if (rb == null)
        {
            Debug.LogWarning("[EnemyMove] 缺少 Rigidbody2D。");
            enabled = false;
            return;
        }
        if (role == Role.Leader && !waveControlled && squad == null)
            ConfigureSpawn(Spawn.NewGroupId(), target);
    }

    bool Alive
    {
        get
        {
            EnemyHP hp = GetComponent<EnemyHP>();
            return isActiveAndEnabled && rb != null && (hp == null || hp.HP > 0);
        }
    }

    void Update()
    {
        if (CampaignSession.Suspended || DebugConsole.SimulationPaused) return;
        if (StoryDirector.Active && StoryDirector.Current.EnemiesFrozen)
        { if (rb != null) rb.velocity = Vector2.zero; return; }
        if (!Alive) { pendingFire = false; return; }
        if (waveControlled)
        {
            if(role==Role.Soldier)rb.velocity=Vector2.right*speed;
            return; // 小队投弹由WaveSquad控制，单兵不随机开枪。
        }
        // 发射时间由战斗脚本负责，卡帧或节拍窗口结束都不会漏发、重发。
        if (pendingFire && Time.timeSinceLevelLoad >= pendingFireAt)
        {
            pendingFire = false;
            Fire();
        }
        if (role == Role.Leader && squad != null)
        {
            squad.Tick();
            if (squad.Busy || squad.JustResumed) return;
        }
        UpdateMarchAndFire();
    }

    void UpdateMarchAndFire()
    {
        if (Time.timeSinceLevelLoad - birthTime < settleTime)
        {
            rb.velocity = Vector2.right * speed;
            return;
        }
        int cycle = Beat.CycleIndex(groupId);
        if (actedCycle < 0) actedCycle = cycle;
        bool fire = Beat.SoldierFires(groupId);
        if (fire && Beat.InActionWindow(groupId))
        {
            rb.velocity = Vector2.zero;
            if (actedCycle != cycle) { actedCycle = cycle; BeginFire(); }
        }
        else rb.velocity = Vector2.right * speed;
    }

    void BeginFire()
    {
        if (bulletPrefab == null) return;
        if (visuals == null || !visuals.CanPlayFire) { Fire(); return; }
        float now = Time.timeSinceLevelLoad;
        visuals.BeginFire(now);
        pendingFire = true;
        pendingFireAt = now + EnemyVisualAnimation.FireReleaseDelay;
    }

    void Fire()
    {
        if (bulletPrefab == null) return;
        SfxPlayer.PlayVariants("SFX-ENM-01", 3, 0.35f, 0.04f);
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            Vector2 direction = Vector2.right;
            body.velocity = direction * bulletSpeed;
        }
    }

    bool HasBombSetup => target != null && bombPrefab != null
        && bombPrefab.GetComponent<Rigidbody2D>() != null && bombSpeed > 0f;

    bool TryBombVelocity(out Vector2 velocity)
    {
        velocity = Vector2.zero;
        if (!HasBombSetup) return false;
        Vector2 origin = transform.position;
        Collider2D wall = target.GetComponent<Collider2D>();
        // 街垒是倾斜的，落点取当前轨道附近的实体表面，不再瞄玩家。
        Vector2 point = wall != null
            ? wall.ClosestPoint(new Vector2(wall.bounds.center.x, birthPosition.y))
            : new Vector2(target.position.x, birthPosition.y);
        float gravity = -Physics2D.gravity.y * bombPrefab.GetComponent<Rigidbody2D>().gravityScale;
        return SolveBombVelocity(point - origin, bombSpeed, gravity, out velocity);
    }

    public static bool SolveBombVelocity(Vector2 displacement, float launchSpeed, float gravity,
                                         out Vector2 velocity)
    {
        velocity = Vector2.zero;
        if (launchSpeed <= 0f || displacement.sqrMagnitude < 0.0001f) return false;
        if (gravity <= 0.0001f)
        {
            velocity = displacement.normalized * launchSpeed;
            return true;
        }
        float distance = Mathf.Abs(displacement.x);
        if (distance < 0.0001f) return false;
        float speedSquared = launchSpeed * launchSpeed;
        float discriminant = speedSquared * speedSquared
            - gravity * (gravity * distance * distance + 2f * displacement.y * speedSquared);
        if (discriminant < 0f) return false;
        // 配合较小重力用低抛解，保持远距离射程，也避免手榴弹飞出画面。
        float angle = Mathf.Atan((speedSquared - Mathf.Sqrt(discriminant)) / (gravity * distance));
        velocity = new Vector2(Mathf.Sign(displacement.x) * launchSpeed * Mathf.Cos(angle),
                               launchSpeed * Mathf.Sin(angle));
        return true;
    }

    public void BeginWaveThrow(){if(visuals!=null)visuals.BeginThrow(Time.timeSinceLevelLoad);}
    public void ThrowWaveBomb(WaveDirector owner,int lane,int impactBeat,string label)
    {
        if(bombPrefab==null)return;
        var bomb=Instantiate(bombPrefab,transform.position,Quaternion.identity);
        bomb.GetComponent<BombHP>().Configure(Spawn.NewGroupId(),owner.LaneY(lane));
        bomb.AddComponent<WaveThreat>().Initialize(owner,WaveEventType.Squad,lane,impactBeat,label);
        SfxPlayer.Play("SFX-BMB-01",.45f);
        if(RunState.Chapter==6)SfxPlayer.Play("SFX-BMB-02",.6f);
    }

    void ThrowBomb()
    {
        if (!TryBombVelocity(out Vector2 velocity)) return;
        SfxPlayer.Play("SFX-BMB-01", 0.45f);
        if (StoryDirector.Active && RunState.Chapter == 6) SfxPlayer.Play("SFX-BMB-02", .6f);
        GameObject bomb = Instantiate(bombPrefab, transform.position,
            Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg));
        bomb.GetComponent<Rigidbody2D>().velocity = velocity;
        BombHP hp = bomb.GetComponent<BombHP>();
        if (hp != null) hp.Configure(groupId, birthPosition.y);
    }

    void OnDisable() => pendingFire = false;

    void OnDestroy()
    {
        pendingFire = false;
        if (squad == null) return;
        squad.members.Remove(this);
        if (squad.members.Count == 0) squads.Remove(groupId);
    }

    sealed class LeaderSquad
    {
        public readonly List<EnemyMove> members = new List<EnemyMove>();
        enum Phase { Advancing, Throwing, Recovery, Assault }
        Phase phase;
        int lastFrame = -1, resumeFrame = -1, rounds, animatedRound = -1;
        float nextAction;
        public bool JustResumed => resumeFrame == Time.frameCount;
        public bool Busy
        {
            get
            {
                if (phase == Phase.Assault) return false;
                foreach (EnemyMove enemy in members)
                    if (enemy != null && enemy.Alive) return true;
                return false;
            }
        }

        void MoveAll(bool moving)
        {
            foreach (EnemyMove enemy in members)
                if (enemy != null && enemy.Alive)
                    enemy.rb.velocity = moving ? Vector2.right * enemy.speed : Vector2.zero;
        }

        void ResumeMarch()
        {
            phase = Phase.Assault;
            resumeFrame = Time.frameCount;
            MoveAll(true);
            foreach (EnemyMove enemy in members)
                if (enemy != null) enemy.actedCycle = Beat.CycleIndex(enemy.groupId);
        }

        public void Tick()
        {
            // 三个 Update 只推进一次队伍状态，避免同帧重复投弹或提前解锁。
            if (lastFrame == Time.frameCount || !Busy) return;
            lastFrame = Time.frameCount;
            EnemyMove first = null;
            foreach (EnemyMove enemy in members)
                if (enemy != null && enemy.Alive) { first = enemy; break; }
            if (first == null) return;

            if (phase == Phase.Advancing)
            {
                bool ready = true;
                foreach (EnemyMove enemy in members)
                {
                    if (enemy == null || !enemy.Alive) continue;
                    if (!enemy.HasBombSetup)
                    {
                        Debug.LogWarning("[EnemyMove] Leader 投弹配置不完整，整组继续行进。");
                        ResumeMarch();
                        return;
                    }
                    if (enemy.transform.position.x - enemy.birthPosition.x < enemy.leaderAdvanceDistance
                        || !enemy.TryBombVelocity(out _)) ready = false;
                }
                MoveAll(!ready);
                if (ready)
                {
                    phase = Phase.Throwing;
                    nextAction = Time.timeSinceLevelLoad + Mathf.Max(0f, first.leaderWindup);
                }
                return;
            }

            MoveAll(false);
            // 第四帧才离手；动画提前开始，保留原有三轮投弹的发射时刻。
            float animationStart = nextAction - EnemyVisualAnimation.ThrowReleaseDelay;
            if (phase == Phase.Throwing && animatedRound != rounds && Time.timeSinceLevelLoad >= animationStart)
            {
                animatedRound = rounds;
                foreach (EnemyMove enemy in members)
                    if (enemy != null && enemy.Alive && enemy.visuals != null)
                        enemy.visuals.BeginThrow(animationStart);
            }
            if (Time.timeSinceLevelLoad < nextAction) return;
            if (phase == Phase.Recovery) { ResumeMarch(); return; }
            foreach (EnemyMove enemy in members)
                if (enemy != null && enemy.Alive) enemy.ThrowBomb();
            rounds++;
            if (rounds >= Mathf.Max(1, first.leaderRoundCount))
            {
                phase = Phase.Recovery;
                nextAction = Time.timeSinceLevelLoad + Mathf.Max(0f, first.leaderRecovery);
            }
            else nextAction = Time.timeSinceLevelLoad + Mathf.Max(0.1f, first.leaderRoundInterval);
        }
    }
}
