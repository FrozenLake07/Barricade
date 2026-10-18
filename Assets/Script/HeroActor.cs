using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class HeroActor : MonoBehaviour
{
    static readonly int MotionParameter = Animator.StringToHash("Motion");
    static readonly int ReloadSpeedParameter = Animator.StringToHash("ReloadSpeed");
    static readonly int LocomotionSpeedParameter = Animator.StringToHash("LocomotionSpeed");
    public HeroAnimationLibrary library;
    public Animator animator;
    public SpriteRenderer visual;
    public Transform groundMuzzle, upMuzzle;
    HeroStateMachine machine;
    int appliedRevision = -1;
    HeroMotion appliedMotion;
    bool externalControl, warned;
    Vector3 previousPosition;
    bool positionInitialized, hasLocomotionSpeed;
    RuntimeAnimatorController checkedController;
    int checkedVersion = -1;

    public HeroStateMachine State { get { EnsureReady(); return machine; } }
    public HeroMotion CurrentMotion => State != null ? State.Motion : HeroMotion.Idle;
    public bool IsBusy => State != null && State.IsBusy;
    public bool IsExternalControlled => externalControl;
    public bool IsStoryControlled => externalControl || (State != null && State.IsStoryControlled);
    public bool IsCarrying => State != null && State.Carrying;

    void Awake() => EnsureReady();
    void OnEnable() => EnsureReady();

    bool EnsureReady()
    {
        if (library == null) library = Resources.Load<HeroAnimationLibrary>(HeroAnimationLibrary.ResourcePath);
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (visual == null && animator != null) visual = animator.GetComponent<SpriteRenderer>();
        if (library == null || library.controller == null || animator == null || visual == null)
        {
            if (Application.isPlaying && !warned)
            {
                warned = true;
                Debug.LogWarning("[HeroActor] 主角动画未生成，请停止运行并执行“街垒/动画/生成或检查主角Animator”。", this);
            }
            return false;
        }
        if (animator.runtimeAnimatorController != library.controller)
            animator.runtimeAnimatorController = library.controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        if (!positionInitialized) { previousPosition = transform.position; positionInitialized = true; }
        if (checkedController != library.controller || checkedVersion != library.formatVersion)
        {
            checkedController = library.controller; checkedVersion = library.formatVersion;
            hasLocomotionSpeed = false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == LocomotionSpeedParameter) { hasLocomotionSpeed = true; break; }
        }
        if (machine == null) machine = new HeroStateMachine(library.Duration);
        return true;
    }

    void Update()
    {
        if (!EnsureReady()) return;
        float speed = Time.deltaTime > 0f ? Vector3.Distance(transform.position, previousPosition) / Time.deltaTime : 0f;
        previousPosition = transform.position;
        if (externalControl) return;
        machine.Tick(Time.deltaTime);
        if (hasLocomotionSpeed)
        {
            bool walking = machine.Motion == HeroMotion.Walk || machine.Motion == HeroMotion.CarryWalk;
            float distance = machine.Motion == HeroMotion.CarryWalk ? library.carryingCycleDistance : library.walkingCycleDistance;
            // Ignore teleports; a stationary story preview keeps the authored cadence.
            float rate = walking && speed < 8f ? HeroLocomotionTiming.PlaybackRate(speed, library.Duration(machine.Motion), distance) : 1f;
            animator.SetFloat(LocomotionSpeedParameter, rate);
        }
        Apply(false);
    }

    void Apply(bool immediate)
    {
        if (machine == null || animator == null || externalControl) return;
        // All art is left-facing except the preserved original Idle.
        if (visual != null) visual.flipX = machine.Motion != HeroMotion.Idle && !machine.FacingLeft;
        if (appliedRevision != machine.Revision)
        {
            HeroAnimationLibrary.Entry entry = library.Find(machine.Motion);
            if (entry == null) return;
            if (machine.Motion == HeroMotion.Reload)
            {
                // Gameplay already starts its one-second reload at the shot. Fit the complete
                // reload clip into the actual time left after recoil, rather than blocking input.
                float speed = entry.duration / Mathf.Max(.01f, machine.ReloadRemaining);
                animator.SetFloat(ReloadSpeedParameter, Mathf.Clamp(speed, .01f, 100f));
            }
            animator.SetInteger(MotionParameter, (int)machine.Motion);
            // Normal changes use the controller's transitions. Explicit commands/restarts
            // sample the first frame immediately, including consecutive Perfect shots.
            if (immediate || (appliedRevision >= 0 && appliedMotion == machine.Motion))
                animator.Play("Base Layer." + machine.Motion, 0, 0f);
            appliedMotion = machine.Motion;
            appliedRevision = machine.Revision;
            if (immediate && animator.isActiveAndEnabled) animator.Update(0f);
        }
    }

    public void SetCombat(bool value) { if (State != null) State.SetCombat(value); }
    public void SetAimUp(bool value) { if (State != null) State.SetAimUp(value); }
    public void SetMoving(bool value) { if (State != null) State.SetMoving(value); }
    public void SetCarrying(bool value) { if (State != null) State.SetCarrying(value); }
    public void SetFacingLeft(bool value) { if (State != null) State.SetFacingLeft(value); }
    public void SetResting(bool value) { if (State != null) State.SetResting(value); }
    public void SetReloadRemaining(float value) { if (State != null) State.SetReloadRemaining(value); }
    public void CancelAction() { if (State != null) State.CancelAction(); }
    public bool BeginAction(HeroMotion motion)
    {
        if (State == null || externalControl || !State.BeginAction(motion)) return false;
        Apply(true);
        return true;
    }
    public void OnActualShot(bool aimUp)
    {
        if (State == null || externalControl || !State.Fire(aimUp)) return;
        Apply(true);
    }

    public void SetExternalControl(bool value)
    {
        externalControl = value;
        previousPosition = transform.position;
        if (animator != null) animator.enabled = true;
        if (value && visual != null) visual.flipX = false;
        if (!value) ResumeGameplay();
    }
    public void ResumeGameplay()
    {
        externalControl = false;
        previousPosition = transform.position;
        if (State != null) State.ResumeGameplay();
        appliedRevision = -1;
        Apply(true);
    }
    public void PlayStory(HeroMotion motion)
    {
        externalControl = false;
        if (State == null) return;
        State.HoldStory(motion);
        Apply(true);
    }

    // Public parameterless methods can be connected directly to Timeline Signals or buttons.
    public void Idle() => PlayStory(HeroMotion.Idle);
    public void IdleLeft() => PlayStory(HeroMotion.IdleLeft);
    public void Walk() => PlayStory(HeroMotion.Walk);
    public void CarryIdle() => PlayStory(HeroMotion.CarryIdle);
    public void CarryWalk() => PlayStory(HeroMotion.CarryWalk);
    public void PickUp() => PlayStory(HeroMotion.PickUp);
    public void Place() => PlayStory(HeroMotion.Place);
    public void Rest() => PlayStory(HeroMotion.Rest);
    public void AimGround() => PlayStory(HeroMotion.AimGround);
    public void FireGround() => PlayStory(HeroMotion.FireGround);
    public void AimUp() => PlayStory(HeroMotion.AimUp);
    public void FireUp() => PlayStory(HeroMotion.FireUp);
    public void Reload() => PlayStory(HeroMotion.Reload);
    public void RaiseGun() => PlayStory(HeroMotion.RaiseGun);
    public void LowerGun() => PlayStory(HeroMotion.LowerGun);
    public void Remember() => PlayStory(HeroMotion.EndingRemember);
    public void Hope() => PlayStory(HeroMotion.EndingHope);
    public void PlayEndingSequence()
    {
        externalControl = false;
        if (State == null) return;
        State.PlayEnding();
        Apply(true);
    }
}
