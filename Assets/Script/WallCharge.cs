using UnityEngine;

/// 场景 1 的"攒血"：进场把街垒血量清成 startHP（默认 0），
/// 之后每次完美按键就加 gainPerPerfect，上限是 GameManager.WallMaxHP。
///
/// WallHP 是 static，所以攒到的血量会**带进场景 2**，成为开局时街垒的生命值 ——
/// 场景 2 的血条就是按"攒到多少 / 满值 1000"来显示剩余比例的。
///
/// 挂在和 Tap 同一个物体上（Tap 会用 GetComponent 找它）。
public class WallCharge : MonoBehaviour
{
    [Header("攒血")]
    [Tooltip("进入这个场景时街垒的初始生命")]
    public int startHP = 0;

    [Tooltip("每次完美按键增加多少")]
    public int gainPerPerfect = 10;

    void Start()
    {
        GameManager.WallHP = Mathf.Clamp(startHP, 0, GameManager.WallMaxHP);
    }

    // 由 Tap 在完美判定成立时调用
    public void OnPerfect()
    {
        GameManager.AddWallHP(gainPerPerfect);
    }
}
