using UnityEngine;

/// 全局数值。全部是 static，任何脚本直接 GameManager.xxx 读写，
/// 不需要在 Inspector 里拖引用，也不必把 GameManager 挂到场景里。
public class GameManager : MonoBehaviour
{
    /// 撞墙者的类别，决定扣墙多少血
    public enum Kind { Enemy, Bullet, Bomb }

    /// 墙的血量
    public static int WallHP = 1000;

    /// 墙能有的最大血量。也是血条按比例显示时的分母
    public static int WallMaxHP = 1000;

    /// 各类撞墙者每次的基础扣血量：Enemy = 10，Bullet = 0.5，Bomb = 20。
    /// 手榴弹仍使用章节倍率；同组连锁仅结算一次，火炮使用独立伤害规则。
    public const float BulletBaseDamage = .5f;
    static readonly float[] WallDamageByKind = { 10, BulletBaseDamage, 20 };
    static float bulletDamageRemainder;
    static int bulletDamageHealth = WallHP;
    public static float PendingBulletDamage => bulletDamageRemainder;

    public static void ResetBulletDamage()
    { bulletDamageRemainder=0;bulletDamageHealth=WallHP; }

    static void SyncBulletDamageHealth()
    { if(bulletDamageHealth!=WallHP)ResetBulletDamage(); }

    /// 墙的 Tag。需要先在 Project Settings → Tags and Layers 里建好同名 Tag
    public const string WallTag = "Wall";

    /// 这个碰撞体是不是墙
    public static bool IsWall(Collider2D other)
    {
        return other != null && other.CompareTag(WallTag);
    }

    /// 撞墙扣血（下限 0）
    public static void DamageWall(Kind kind)
    {
        if(kind!=Kind.Bullet){DamageWallAmount((int)WallDamageByKind[(int)kind],kind==Kind.Bomb);return;}
        if(CampaignDebug.Value.god||(StoryDirector.Active&&!StoryDirector.CombatRunning))return;
        SyncBulletDamageHealth();
        float multiplier=StoryDirector.Active?StoryDirector.Current.Chapter.combatDamageMultiplier:1;
        float accumulated=bulletDamageRemainder+BulletBaseDamage*Mathf.Max(0,multiplier);
        int whole=Mathf.FloorToInt(accumulated+0.000001f);
        bulletDamageRemainder=Mathf.Max(0,accumulated-whole);
        ApplyWallDamage(whole);
    }

    public static void DamageWallAmount(int amount, bool explosion = false,bool finalBarrage=false)
    {
        bool scripted=finalBarrage&&StoryDirector.Active&&RunState.Chapter==9&&StoryDirector.Current.FinalBarrageActive;
        if ((CampaignDebug.Value.god&&!scripted) || (StoryDirector.Active && !StoryDirector.CombatRunning)) return;
        if(StoryDirector.Active)amount=CampaignRevisionRules.ScaleCombatDamage(amount,StoryDirector.Current.Chapter.combatDamageMultiplier);
        SyncBulletDamageHealth();
        ApplyWallDamage(amount);
    }

    static void ApplyWallDamage(int amount)
    {
        int before=WallHP;
        WallHP = Mathf.Max(0, WallHP - Mathf.Max(0, amount));
        bulletDamageHealth=WallHP;
        if(WallHP==0)ResetBulletDamage();
        CampaignTelemetry.Current?.HealthChanged(before,WallHP,0);
        CampaignSoundscape.WallDamage(before,WallHP,WallMaxHP);
    }

    /// 给街垒回血（上限为 WallMaxHP）。场景 1 每次完美按键就调它
    public static void AddWallHP(int amount)
    {
        SyncBulletDamageHealth();
        int before=WallHP;
        WallHP = Mathf.Min(WallMaxHP, WallHP + amount);
        bulletDamageHealth=WallHP;
        if(WallHP>=WallMaxHP)ResetBulletDamage();
        CampaignTelemetry.Current?.HealthChanged(before,WallHP,Mathf.Max(0,amount));
    }
}
