using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)]
public sealed class HeroConstructionDriver : MonoBehaviour
{
    public HeroActor actor;
    public Tap qte;
    public ConstructionStageFlow flow;
    public Transform homePoint, buildPoint;
    public SpriteRenderer homeCrate, placedCrate, droppedCrate;
    [Min(.01f)] public float moveSpeed = 1.05f;
    [Range(.1f, 1f)] public float carryingSpeedMultiplier = .8f;
    public Vector3 crateGroundOffset = new Vector3(-.44f, -.45f, 0f);
    enum Phase { Idle, PickingUp, Carrying, Placing, Returning }
    Phase phase;
    Phase phaseBeforeRest;
    bool pendingWork, wasResting, resumeAfterRest, cutscene;
    Tap subscribedTap;
    ConstructionStageFlow subscribedFlow;
    float stepTimer;

    void OnEnable() => Subscribe();
    void Start()
    {
        Subscribe();
        if (actor == null || homePoint == null || buildPoint == null)
        {
            Debug.LogWarning("[HeroConstructionDriver] 缺少主角或搬运路径引用。", this);
            enabled = false;
            return;
        }
        actor.transform.position = homePoint.position;
        actor.SetCombat(false);
        actor.SetFacingLeft(true);
        actor.SetMoving(false);
        actor.SetCarrying(false);
        Show(homeCrate, true);
        Show(placedCrate, false);
        Show(droppedCrate, false);
    }

    void Subscribe()
    {
        if (qte != subscribedTap)
        {
            if (subscribedTap != null) subscribedTap.InputAccepted -= OnAccepted;
            subscribedTap = qte;
            if (subscribedTap != null) subscribedTap.InputAccepted += OnAccepted;
        }
        if (flow != subscribedFlow)
        {
            if (subscribedFlow != null) subscribedFlow.CutsceneStarting -= OnCutscene;
            subscribedFlow = flow;
            if (subscribedFlow != null) subscribedFlow.CutsceneStarting += OnCutscene;
        }
    }

    void OnAccepted(bool perfect)
    {
        // A visual work cycle can span several QTEs. Do not delay, queue or rescore inputs.
        if (!cutscene && (flow == null || flow.IsBuilding)) pendingWork = true;
    }

    void Update()
    {
        if (actor == null || cutscene || actor.IsStoryControlled) return;
        bool resting = Tap.IsResting;
        if (resting && !wasResting)
        {
            phaseBeforeRest = phase;
            resumeAfterRest = phase != Phase.Idle;
            if (phase == Phase.PickingUp || phase == Phase.Carrying || phase == Phase.Placing || actor.IsCarrying)
            {
                if (droppedCrate != null) droppedCrate.transform.position = actor.transform.position + crateGroundOffset;
                Show(droppedCrate, true);
                SfxPlayer.Play("SFX-BLD-07", 0.4f);
            }
            actor.SetMoving(false);
            actor.SetCarrying(false);
            actor.SetResting(true);
            phase = Phase.Idle;
        }
        if (!resting && wasResting)
        {
            actor.SetResting(false);
            if (resumeAfterRest && phaseBeforeRest == Phase.Returning)
            {
                phase = Phase.Returning;
                actor.SetFacingLeft(homePoint.position.x <= actor.transform.position.x);
                actor.SetMoving(true);
            }
            else if (resumeAfterRest) pendingWork = true;
            resumeAfterRest = false;
        }
        wasResting = resting;
        if (resting) return;

        switch (phase)
        {
            case Phase.Idle:
                if (!pendingWork) break;
                pendingWork = false;
                actor.SetFacingLeft(true);
                actor.SetCarrying(false);
                Show(homeCrate, false);
                Show(droppedCrate, false);
                if (actor.BeginAction(HeroMotion.PickUp)) phase = Phase.PickingUp;
                break;
            case Phase.PickingUp:
                if (actor.IsBusy) break;
                actor.SetCarrying(true);
                actor.SetFacingLeft(buildPoint.position.x <= actor.transform.position.x);
                actor.SetMoving(true);
                SfxPlayer.PlayVariants("SFX-BLD-01", 3, 0.4f, 0.04f);
                phase = Phase.Carrying;
                break;
            case Phase.Carrying:
                if (!MoveTo(buildPoint.position)) break;
                actor.SetMoving(false);
                Show(placedCrate, false);
                if (actor.BeginAction(HeroMotion.Place)) phase = Phase.Placing;
                break;
            case Phase.Placing:
                if (actor.IsBusy) break;
                Show(placedCrate, true);
                actor.SetCarrying(false);
                actor.SetFacingLeft(homePoint.position.x <= actor.transform.position.x);
                actor.SetMoving(true);
                SfxPlayer.PlayVariants("SFX-BLD-02", 3, 0.45f, 0.04f);
                phase = Phase.Returning;
                break;
            case Phase.Returning:
                if (!MoveTo(homePoint.position)) break;
                actor.SetMoving(false);
                actor.SetFacingLeft(true);
                Show(homeCrate, true);
                phase = Phase.Idle;
                break;
        }
    }

    bool MoveTo(Vector3 target)
    {
        actor.transform.position = Vector3.MoveTowards(actor.transform.position, target,
            Mathf.Max(.01f, moveSpeed) * (phase == Phase.Carrying ? carryingSpeedMultiplier : 1f) * Time.deltaTime);
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            stepTimer = 0.32f;
            SfxPlayer.PlayVariants("SFX-BLD-06", 4, 0.35f, 0.05f);
        }
        return (actor.transform.position - target).sqrMagnitude < .000001f;
    }

    void OnCutscene()
    {
        cutscene = true;
        pendingWork = false;
        actor.SetResting(false);
        actor.SetMoving(false);
        actor.SetCarrying(false);
        actor.CancelAction();
        actor.SetFacingLeft(true);
        Show(droppedCrate, false);
        actor.SetExternalControl(true);
    }

    static void Show(SpriteRenderer renderer2D, bool value)
    {
        if (renderer2D != null) renderer2D.enabled = value;
    }
    void OnDisable()
    {
        if (subscribedTap != null) subscribedTap.InputAccepted -= OnAccepted;
        if (subscribedFlow != null) subscribedFlow.CutsceneStarting -= OnCutscene;
        subscribedTap = null;
        subscribedFlow = null;
    }
}
