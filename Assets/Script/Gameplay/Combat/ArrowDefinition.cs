using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowDefinition : MonoBehaviour
{
    // Start is called before the first frame update
    public int Damage;
    public float Speed=10f;

    [Tooltip("是不是玩家的子弹。只有玩家的子弹会造成伤害（敌人的子弹不会打到自己人）")]
    public bool fromPlayer;

    [Tooltip("这颗子弹的攻击轨道 y。玩家子弹只会打到这个高度附近的目标")]
    public float trackY;
    [Tooltip("攻击轨道的 y 容差")]
    public float trackRange = 1.25f;

    /// 这颗子弹只打哪一类目标
    public enum TargetKind { Enemy, Bomb }

    [Tooltip("严格类型隔离：Down 瞄敌人的子弹伤不到炸弹，Up 瞄手榴弹的子弹伤不到敌人")]
    public TargetKind targetKind = TargetKind.Enemy;

    // 只在实例生成后注入，不序列化进敌我共用的预制体。
    [System.NonSerialized] public SBullet playerShooter;
    [System.NonSerialized] public int playerShotId;
    [System.NonSerialized] public bool perfectShot;
}
