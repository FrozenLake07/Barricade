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

    /// 各类撞墙者每次的扣血量：Enemy = 10，Bullet = 1，Bomb = 50
    static readonly int[] WallDamageByKind = { 10, 1, 50 };

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
        WallHP -= WallDamageByKind[(int)kind];
        if (WallHP < 0) WallHP = 0;
    }

    /// 给街垒回血（上限为 WallMaxHP）。场景 1 每次完美按键就调它
    public static void AddWallHP(int amount)
    {
        WallHP = Mathf.Min(WallMaxHP, WallHP + amount);
    }
}
