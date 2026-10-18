using System;

// This state machine selects actions. Unity Animator owns all sprite-frame playback.
public enum HeroMotion
{
    Idle, IdleLeft, Walk, CarryIdle, CarryWalk, PickUp, Place, Rest,
    AimGround, FireGround, AimUp, FireUp, Reload, RaiseGun, LowerGun,
    EndingRemember, EndingHope
}

public sealed class HeroStateMachine
{
    readonly Func<HeroMotion, float> duration;
    float remaining;
    bool timedAction, storyHold, endingSequence;
    bool combat, up, moving, carrying, resting, left = true;
    float reloadRemaining;

    public HeroMotion Motion { get; private set; } = HeroMotion.IdleLeft;
    public int Revision { get; private set; }
    public bool IsBusy => timedAction;
    public bool IsStoryControlled => storyHold || endingSequence;
    public bool Carrying => carrying;
    public bool FacingLeft => left;
    public float ReloadRemaining => reloadRemaining;
    public float ActionRemaining => remaining;

    public HeroStateMachine(Func<HeroMotion, float> clipDuration)
    {
        duration = clipDuration ?? throw new ArgumentNullException(nameof(clipDuration));
    }

    public void SetCombat(bool value) { combat = value; Resolve(); }
    public void SetAimUp(bool value) { up = value; Resolve(); }
    public void SetMoving(bool value) { moving = value; Resolve(); }
    public void SetCarrying(bool value) { carrying = value; Resolve(); }
    public void SetFacingLeft(bool value) { left = value; Resolve(); }
    public void SetReloadRemaining(float value)
    {
        reloadRemaining = Valid(value);
        Resolve();
    }

    public void SetResting(bool value)
    {
        resting = value;
        if (value && !IsStoryControlled) { timedAction = false; remaining = 0f; }
        Resolve();
    }

    public bool BeginAction(HeroMotion motion)
    {
        if (IsStoryControlled || resting) return false;
        switch (motion)
        {
            case HeroMotion.PickUp: case HeroMotion.Place:
            case HeroMotion.RaiseGun: case HeroMotion.LowerGun:
                timedAction = true;
                remaining = ClipDuration(motion);
                Change(motion, true);
                return true;
            default: return false;
        }
    }

    public bool Fire(bool aimUp)
    {
        if (IsStoryControlled || resting) return false;
        combat = true;
        // A new legal shot can interrupt the preceding recoil, including another Perfect shot.
        HeroMotion shot = aimUp ? HeroMotion.FireUp : HeroMotion.FireGround;
        timedAction = true;
        remaining = ClipDuration(shot);
        Change(shot, true);
        return true;
    }

    public void CancelAction()
    {
        timedAction = false;
        remaining = 0f;
        Resolve();
    }

    public void HoldStory(HeroMotion motion)
    {
        timedAction = false;
        endingSequence = false;
        storyHold = true;
        remaining = 0f;
        Change(motion, true);
    }

    public void PlayEnding()
    {
        timedAction = false;
        storyHold = true;
        endingSequence = true;
        remaining = ClipDuration(HeroMotion.EndingRemember);
        Change(HeroMotion.EndingRemember, true);
    }

    public void ResumeGameplay()
    {
        storyHold = endingSequence = timedAction = false;
        remaining = 0f;
        Resolve();
    }

    public void Tick(float deltaTime)
    {
        float delta = Valid(deltaTime);
        if (endingSequence)
        {
            remaining -= delta;
            if (remaining <= 0f && Motion == HeroMotion.EndingRemember)
            {
                remaining += ClipDuration(HeroMotion.EndingHope);
                Change(HeroMotion.EndingHope, true);
            }
            if (remaining <= 0f && Motion == HeroMotion.EndingHope) endingSequence = false;
            return;
        }
        if (storyHold) return;
        if (timedAction)
        {
            remaining -= delta;
            if (remaining > 0f) return;
            timedAction = false;
            remaining = 0f;
            switch (Motion)
            {
                case HeroMotion.PickUp: carrying = true; break;
                case HeroMotion.Place: carrying = false; break;
                case HeroMotion.RaiseGun: combat = true; break;
                case HeroMotion.LowerGun: combat = false; break;
            }
        }
        Resolve();
    }

    void Resolve()
    {
        if (IsStoryControlled || timedAction) return;
        HeroMotion next = resting ? HeroMotion.Rest
            : combat ? (reloadRemaining > 0f ? HeroMotion.Reload : up ? HeroMotion.AimUp : HeroMotion.AimGround)
            : carrying ? (moving ? HeroMotion.CarryWalk : HeroMotion.CarryIdle)
            : moving ? HeroMotion.Walk : left ? HeroMotion.IdleLeft : HeroMotion.Idle;
        Change(next, false);
    }

    float ClipDuration(HeroMotion motion) => Math.Max(.001f, Valid(duration(motion)));
    static float Valid(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    void Change(HeroMotion motion, bool restart)
    {
        if (!restart && Motion == motion) return;
        Motion = motion;
        Revision++;
    }
}
