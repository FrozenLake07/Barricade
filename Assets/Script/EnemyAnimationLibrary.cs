using UnityEngine;

// 由编辑器生成；保存真正的 AnimatorController 和对齐后的行走精灵。
public sealed class EnemyAnimationLibrary : ScriptableObject
{
    public const string ResourcePath = "EnemyAnimations/Animator/EnemyAnimationLibrary";
    public static readonly Vector3 GrenadierOffset = new Vector3(.0625f, 0f, 0f);
    public int formatVersion;
    public Sprite[] alignedSoldierWalk;
    public RuntimeAnimatorController soldierController, grenadierController;
}
