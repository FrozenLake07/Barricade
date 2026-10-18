using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)]
public sealed class HeroCombatDriver : MonoBehaviour
{
    public HeroActor actor;
    public TapSec input;
    public SBullet shooter;
    SBullet subscribed;

    void OnEnable() => Subscribe();
    void Start()
    {
        if (actor == null) actor = GetComponent<HeroActor>();
        if (input == null) input = GetComponent<TapSec>();
        if (shooter == null && input != null) shooter = input.sbullet;
        Subscribe();
        if (actor == null) return;
        actor.SetFacingLeft(true);
        actor.SetCombat(true);
        actor.SetAimUp(TapSec.ShootStatu == "Up");
        actor.BeginAction(HeroMotion.RaiseGun);
    }

    void Subscribe()
    {
        if (shooter == null || subscribed == shooter) return;
        if (subscribed != null) subscribed.ShotFired -= OnShot;
        subscribed = shooter;
        subscribed.ShotFired += OnShot;
    }
    void Update()
    {
        Subscribe();
        if (actor == null || actor.IsStoryControlled) return;
        actor.SetAimUp(TapSec.ShootStatu == "Up");
        actor.SetReloadRemaining(shooter != null ? shooter.ReloadRemaining : 0f);
    }
    void OnShot(bool aimUp, bool perfect, int shotId)
    {
        if (actor == null) return;
        actor.SetReloadRemaining(shooter != null ? shooter.ReloadRemaining : 0f);
        actor.OnActualShot(aimUp);
    }
    void OnDisable()
    {
        if (subscribed != null) subscribed.ShotFired -= OnShot;
        subscribed = null;
    }
}
