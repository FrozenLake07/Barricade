using UnityEngine;
using UnityEngine.UI;

/// 心脏血条：彩色层做成"从下往上的填充"，灰色层常驻在后面。
/// 街垒血量越少，彩色部分越矮，露出来的灰色就从上往下推进。
///
/// 层级要求：
///   Heart           ← 挂 Heartbeat（缩放脉冲）。挂这里，缩放才会同时作用在两层上
///   ├─ GrayImage    ← 灰色心脏，满尺寸，不动
///   └─ ColorImage   ← 彩色心脏，Image Type = Filled，Fill Method = Vertical，Fill Origin = Bottom
public class HeartFill : MonoBehaviour
{
    [Tooltip("彩色那层 Image（Type 要设成 Filled / Vertical / Fill Origin = Bottom）")]
    public Image fill;

    [Tooltip("街垒满血值（血条的分母）。填 0 = 开局自动读 GameManager.WallHP（推荐）")]
    public int maxWallHP;

    [Tooltip("跟随快慢。调大 = 更快跟上，调小 = 更缓慢地褪下去")]
    public float followSpeed = 6f;

    int maxHP;
    float shown = 1f;      // 当前显示出来的比例，平滑追向目标

    void Start()
    {
        // 分母取"进场时的血量"——场景 1 攒到多少，场景 2 就以它为满格。
        // 所以攒到 300 进来，血条就是 100% 起步，后面挨打再往下掉。
        if (StoryDirector.Active) maxWallHP = GameManager.WallMaxHP;
        maxHP = maxWallHP > 0 ? maxWallHP : Mathf.Max(1, GameManager.WallHP);

        shown = Ratio();   // 开局直接就位，不要从满格慢慢滑下来
        Apply();
    }

    void Update()
    {
        // 平滑跟随：血量是一格一格掉的，不追一下会看见台阶
        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        shown = Mathf.Lerp(shown, Ratio(), k);
        Apply();
    }

    // 剩余比例：1 = 满血，0 = 空血
    float Ratio()
    {
        return Mathf.Clamp01((float)GameManager.WallHP / maxHP);
    }

    void Apply()
    {
        if (fill != null) fill.fillAmount = shown;
    }
}
