using UnityEngine;

/// 火炮独立部署后按自身计时开火，晚期刷出由关卡流程决定。
[DisallowMultipleComponent]
public sealed class Cannon : MonoBehaviour
{
    [Header("炮击（策划：每30秒，最大街垒血量25%）")]
    public CannonShell shellPrefab;
    public Collider2D targetWall;
    public Transform muzzle;
    public Animator visualAnimator;
    public bool autoFire = true;
    [Min(1f)] public float fireInterval = 30f;
    [Min(0f)] public float firstShotDelay = 30f;
    [Min(1f)] public float shellSpeed = 80f;
    [Range(0f, 1f)] public float wallDamageFraction = .25f;

    [Header("部署移动")]
    [Min(0f)] public float advanceDistance = 1.5f;
    [Min(.01f)] public float moveSpeed = .4f;
    [Min(0f)] public float wallStandOff = 1.5f;
    [Tooltip("炮车约八成进入画面后停止部署，不向场地中央行驶")]
    [Range(.5f, 1f)] public float deploymentVisibleFraction = .8f;
    [Header("开炮本体尺寸校正（以行走55x36像素为基准）")]
    public Vector2 fireBodyScale = new Vector2(55f / 41f, 36f / 28f);
    [Tooltip("开炮第3帧出现火光，实际出弹对齐该帧")]
    [Min(0f)] public float releaseDelay = 2f / 12f;

    bool retreating,finalBarrageShot;
    float nextChainSound;Vector3 audioPosition;bool audioPositionKnown;
    public bool IsRetreating=>retreating;
    public void BeginRetreat(){retreating=true;pending=firing=false;autoFire=false;}
    static readonly int Moving = Animator.StringToHash("Moving");
    float nextShot, releaseAt, animationEndsAt, remainingAdvance;
    bool pending, firing, warned;
    bool duplicate;
    int waveImpact=-1,waveLane=2;
    string waveTag;
    Vector3 restingScale;
    static readonly System.Collections.Generic.Dictionary<int, Cannon> owners =
        new System.Collections.Generic.Dictionary<int, Cannon>();
    public bool IsFiring => firing;
    public float ShotRemaining => Mathf.Max(0f, nextShot - Time.timeSinceLevelLoad);

    void Awake()
    {
        restingScale = transform.localScale;
        int scene = gameObject.scene.handle;
        if (owners.TryGetValue(scene, out Cannon existing) && existing != null && existing != this)
        {
            duplicate = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        owners[scene] = this;
        if (visualAnimator == null) visualAnimator = GetComponentInChildren<Animator>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOwners() => owners.Clear();

    void OnEnable()
    {
        if (duplicate) return;
        pending = firing = warned = false;
        remainingAdvance = Mathf.Max(0f, advanceDistance);
        nextShot = Time.timeSinceLevelLoad + Mathf.Max(0f, firstShotDelay);
        if (visualAnimator != null) visualAnimator.SetBool(Moving, false);
    }

    void Start()
    {
        if (CampaignSession.Restoring) return;
        if (duplicate) return;
        ResolveWall();
        var camera = Camera.main;
        var renderer = GetComponent<SpriteRenderer>();
        if (camera != null && camera.orthographic && renderer != null
            && camera.gameObject.scene == gameObject.scene)
        {
            float left = camera.transform.position.x - camera.orthographicSize * camera.aspect;
            float rightFromOrigin = renderer.bounds.max.x - transform.position.x;
            float stopX = left - rightFromOrigin + renderer.bounds.size.x * Mathf.Clamp01(deploymentVisibleFraction);
            remainingAdvance = Mathf.Min(remainingAdvance, Mathf.Max(0f, stopX - transform.position.x));
        }
    }

    public void ConfigureSpawn(Collider2D wall) { targetWall = wall; }
    public void ResumeWaveDeployment(Vector3 position)
    {transform.position=position;remainingAdvance=0;autoFire=false;if(visualAnimator!=null)visualAnimator.SetBool(Moving,false);}

    bool ResolveWall()
    {
        if (targetWall != null && targetWall.enabled && targetWall.gameObject.activeInHierarchy
            && GameManager.IsWall(targetWall)) return true;
        foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            if (collider.enabled && GameManager.IsWall(collider)) { targetWall = collider; return true; }
        if (!warned) { Debug.LogWarning("[Cannon] 未找到启用的Wall碰撞体，等待街垒后再炮击。", this); warned = true; }
        return false;
    }

    void Update()
    {
        if(CampaignSession.Suspended||DebugConsole.SimulationPaused)return;
        if(retreating)
        {
            if(CampaignSession.Suspended||DebugConsole.SimulationPaused)return;
            transform.position+=Vector3.left*Mathf.Max(.8f,moveSpeed)*Time.deltaTime;
            if(Time.timeSinceLevelLoad>=nextChainSound){nextChainSound=Time.timeSinceLevelLoad+.85f;SfxPlayer.Play("artillery_chain_detail",.5f);}
            if(visualAnimator!=null)visualAnimator.SetBool(Moving,true);
            var camera=Camera.main;var sprite=GetComponent<SpriteRenderer>();
            if(camera!=null&&sprite!=null&&sprite.bounds.max.x<camera.transform.position.x-camera.orthographicSize*camera.aspect-.25f)Destroy(gameObject);
            return;
        }
        if (StoryDirector.Active && StoryDirector.Current.EnemiesFrozen&&!StoryDirector.Current.FinalBarrageActive) return;
        if (duplicate) return;
        if (Time.deltaTime <= 0f) return;
        float now = Time.timeSinceLevelLoad;
        if (pending && now >= releaseAt) ReleaseShell();
        if (firing && now >= animationEndsAt) firing = false;
        bool moving = false;
        if (!firing && remainingAdvance > 0f)
        {
            float step = Mathf.Min(remainingAdvance, Mathf.Max(.01f, moveSpeed) * Time.deltaTime);
            if (targetWall != null)
                step = Mathf.Min(step, Mathf.Max(0f, targetWall.bounds.min.x - wallStandOff - transform.position.x));
            transform.position += Vector3.right * step;
            remainingAdvance -= step;
            if (step == 0f || remainingAdvance < .0001f) remainingAdvance = 0f;
            moving = step > 0f;
        }
        if(!audioPositionKnown){audioPosition=transform.position;audioPositionKnown=true;}
        if((transform.position-audioPosition).sqrMagnitude>.000001f&&now>=nextChainSound)
        {nextChainSound=now+.85f;SfxPlayer.Play("artillery_chain_detail",.5f);}
        audioPosition=transform.position;
        if (visualAnimator != null) visualAnimator.SetBool(Moving, moving);
        // 提前播放火光前的两帧，使首发出弹时刻仍是部署后第30秒。
        if (autoFire && !firing && remainingAdvance <= 0f && now >= nextShot - Mathf.Max(0f, releaseDelay))
            BeginFire(Mathf.Max(nextShot, now + Mathf.Max(0f, releaseDelay)));
    }

    void LateUpdate()
    {
        if (CampaignSession.Suspended || DebugConsole.SimulationPaused) return;
        if (duplicate) return;
        // Animator完成采样后按实际状态校正，避免Fire和Idle切换时大小错一帧。
        bool fire = visualAnimator != null && visualAnimator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Fire");
        transform.localScale = new Vector3(restingScale.x * (fire ? fireBodyScale.x : 1f),
            restingScale.y * (fire ? fireBodyScale.y : 1f), restingScale.z);
    }

    [ContextMenu("立即试射（运行中）")]
    public void FireNow()
    {
        if (Application.isPlaying && isActiveAndEnabled && !duplicate && !firing)
            BeginFire(Time.timeSinceLevelLoad + Mathf.Max(0f, releaseDelay));
    }

    public void FireWave(int impactBeat,int lane,string label)
    {
        if(!Application.isPlaying||duplicate||pending||retreating)return;
        firing=false;
        autoFire=false;waveImpact=impactBeat;waveLane=lane;waveTag=label;
        BeginFire(Time.timeSinceLevelLoad+Mathf.Max(0,releaseDelay));
    }

    public bool FireFinalBarrage()
    {
        if(!Application.isPlaying||duplicate||!isActiveAndEnabled||shellPrefab==null||!ResolveWall())return false;
        retreating=false;remainingAdvance=0;autoFire=false;pending=firing=false;waveImpact=-1;
        var camera=Camera.main;var sprite=GetComponent<SpriteRenderer>();
        if(camera!=null&&camera.orthographic&&sprite!=null)
        {
            float left=camera.transform.position.x-camera.orthographicSize*camera.aspect;
            if(sprite.bounds.max.x<left+sprite.bounds.size.x*.7f)
                transform.position+=Vector3.right*(left+sprite.bounds.size.x*.8f-sprite.bounds.max.x);
        }
        finalBarrageShot=true;BeginFire(Time.timeSinceLevelLoad+Mathf.Max(0,releaseDelay));
        return pending;
    }

    void BeginFire(float time)
    {
        if (shellPrefab == null)
        {
            if (!warned) { Debug.LogWarning("[Cannon] 缺少CannonShell预制体，无法开炮。", this); warned = true; }
            return;
        }
        if (!ResolveWall()) return;
        pending = firing = true;
        releaseAt = time;
        animationEndsAt = Time.timeSinceLevelLoad + 1f;
        if (visualAnimator != null)
        {
            visualAnimator.SetBool(Moving, false);
            visualAnimator.Play("Base Layer.Fire", 0, 0f);
        }
    }

    void ReleaseShell()
    {
        pending = false;
        float now = Time.timeSinceLevelLoad;
        nextShot = now + Mathf.Max(1f, fireInterval); // 卡帧不追补多发，避免瞬间清空街垒。
        if (shellPrefab == null || !ResolveWall()) return;
        Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
        Vector2 target = targetWall.ClosestPoint(origin);
        Vector2 direction = target - origin;
        if (direction.sqrMagnitude < .0001f) direction = Vector2.right;
        var shell = Instantiate(shellPrefab, origin, Quaternion.identity);
        if(finalBarrageShot)
        {
            shell.MarkFinalBarrage();shell.Launch(direction.normalized,Mathf.Max(40,shellSpeed),.25f,target.y);
            finalBarrageShot=false;CampaignTelemetry.Current?.Record("final_barrage_shot");
        }
        else if(waveImpact>=0&&WaveDirector.Current!=null)
        {
            var wave=WaveDirector.Current;
            float seconds=(float)WaveTiming.SecondsUntil(wave.Position,waveImpact,wave.Period);
            shell.lifetime=Mathf.Max(shell.lifetime,seconds+2);
            shell.Launch(direction.normalized,Mathf.Max(1,direction.magnitude/Mathf.Max(.1f,seconds)),wallDamageFraction,wave.LaneY(waveLane));
            shell.gameObject.AddComponent<WaveThreat>().Initialize(wave,WaveEventType.Shell,waveLane,waveImpact,waveTag);
            waveImpact=-1;
        }
        else shell.Launch(direction.normalized, shellSpeed, wallDamageFraction, transform.position.y);
        SfxPlayer.PlayVariants("SFX-ART-02", 2, .9f);
    }

    void OnDisable()
    {
        pending = firing = false;
        transform.localScale = restingScale;
        if (visualAnimator != null) visualAnimator.SetBool(Moving, false);
    }

    void OnDestroy()
    {
        int scene = gameObject.scene.handle;
        if (owners.TryGetValue(scene, out Cannon owner) && owner == this) owners.Remove(scene);
    }
}

