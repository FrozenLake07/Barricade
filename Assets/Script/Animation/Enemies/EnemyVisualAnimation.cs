using UnityEngine;

// 战斗脚本只发送动作指令；帧采样和动作结束后的切换由 Animator 负责。
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class EnemyVisualAnimation : MonoBehaviour
{
    public const float FireReleaseDelay = 3f / 12f;
    public const float ThrowReleaseDelay = 4f / 10f;
    static readonly int Moving = Animator.StringToHash("Moving");
    static readonly int FireTrigger = Animator.StringToHash("Fire");
    static readonly int ThrowTrigger = Animator.StringToHash("Throw");
    static EnemyAnimationLibrary library;
    static bool warnedMissing;
    EnemyMove enemy;
    Rigidbody2D body;
    SpriteRenderer renderer2D;
    GrenadierOriginalRig rig;
    Animator animator;
    Sprite originalSprite;
    bool initialized, ready, originalEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() { library = null; warnedMissing = false; }

    public bool CanPlayFire
    {
        get { EnsureInitialized(); return isActiveAndEnabled && enemy != null && enemy.enabled && ready; }
    }

    void Awake() => EnsureInitialized();
    void OnEnable() => EnsureInitialized();

    void EnsureInitialized()
    {
        if (initialized || !Application.isPlaying || !isActiveAndEnabled) return;
        initialized = true;
        enemy = GetComponent<EnemyMove>();
        body = GetComponent<Rigidbody2D>();
        renderer2D = GetComponent<SpriteRenderer>();
        if (enemy == null || renderer2D == null) return;
        originalSprite = renderer2D.sprite;
        originalEnabled = renderer2D.enabled;
        if (library == null) library = Resources.Load<EnemyAnimationLibrary>(EnemyAnimationLibrary.ResourcePath);
        bool grenadier = enemy.role == EnemyMove.Role.Leader;
        RuntimeAnimatorController controller = library != null
            ? (grenadier ? library.grenadierController : library.soldierController) : null;
        if (controller == null)
        {
            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("[EnemyVisualAnimation] Animator资源未生成，请停止运行并执行“街垒/动画/生成或检查敌人Animator”。");
            }
            return;
        }
        animator = GetComponent<Animator>();
        if (animator == null) animator = gameObject.AddComponent<Animator>();
        animator.enabled = false;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        // 敌人离开摄像机画面时，动作时钟仍与实际攻击保持一致。
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 1f;
        if (grenadier)
        {
            rig = GetComponent<GrenadierOriginalRig>();
            if (rig == null) rig = gameObject.AddComponent<GrenadierOriginalRig>();
            rig.Initialize(Resources.Load<Texture2D>("EnemyAnimations/Grenadier_Original"),
                Resources.Load<Texture2D>("EnemyAnimations/Grenade_Original"),
                Resources.Load<TextAsset>("EnemyAnimations/GrenadierRig"), EnemyAnimationLibrary.GrenadierOffset);
            if (!rig.IsReady) return;
        }
        // 掷弹兵的部件创建完成后再重新绑定，避免 Animator 缓存不存在的子物体。
        animator.enabled = true;
        animator.Rebind();
        animator.SetBool(Moving, body != null && body.velocity.sqrMagnitude > .0001f);
        animator.Update(0f);
        ready = true;
    }

    void Update()
    {
        EnsureInitialized();
        if (!ready || animator == null) return;
        if (enemy == null || !enemy.enabled) { animator.enabled = false; return; }
        animator.enabled = true;
        animator.SetBool(Moving, body != null && body.velocity.sqrMagnitude > .0001f);
    }

    public void BeginFire(float startTime)
    {
        if (CanPlayFire) BeginAction(FireTrigger, "Base Layer.Fire", startTime, 8f / 12f);
    }

    public void BeginThrow(float startTime)
    {
        EnsureInitialized();
        if (isActiveAndEnabled && ready && enemy != null && enemy.role == EnemyMove.Role.Leader)
            BeginAction(ThrowTrigger, "Base Layer.Throw", startTime, 8f / 10f);
    }

    void BeginAction(int trigger, string state, float startTime, float duration)
    {
        animator.ResetTrigger(FireTrigger);
        if (enemy.role == EnemyMove.Role.Leader) animator.ResetTrigger(ThrowTrigger);
        float elapsed = Mathf.Max(0f, Time.timeSinceLevelLoad - startTime);
        // 严重卡帧后的投弹可能已经离手；只让外观追上战斗时间，不重放或再次发射。
        if (elapsed >= duration) return;
        if (elapsed > Mathf.Max(Time.deltaTime, .025f) + .001f)
        {
            animator.Play(state, 0, elapsed / duration);
            animator.Update(0f);
        }
        else animator.SetTrigger(trigger);
    }

    void OnDisable()
    {
        ready = false;
        if (animator != null) animator.enabled = false;
        if (rig != null) rig.enabled = false;
        if (renderer2D != null && initialized)
        {
            renderer2D.sprite = originalSprite;
            renderer2D.enabled = originalEnabled;
        }
        initialized = false;
    }
}
