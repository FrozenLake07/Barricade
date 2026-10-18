using UnityEngine;
using UnityEngine.UI;

/// 挂在 Key 那个 Text 上：文字一变就弹一下缩放，再平滑回到原大小。
/// 不需要和 Tap 连线 —— 它自己盯着自己的 Text，发现被改了就开始脉冲。
public class KeyPulse : MonoBehaviour
{
    [Header("缩放脉冲")]
    [Tooltip("一次脉冲持续多久（秒）")]
    public float duration = 0.25f;
    [Tooltip("脉冲瞬间放大到几倍")]
    public float peakScale = 1.4f;

    Text label;
    Vector3 baseScale;
    string lastText;
    float timer;          // > 0 表示正在脉冲

    void Awake()
    {
        label = GetComponent<Text>();
        baseScale = transform.localScale;
        if (label != null) lastText = label.text;
    }

    void Update()
    {
        // 文字被换掉了（Tap.Randoml 改了它）—— 开始一次脉冲
        if (label != null && label.text != lastText)
        {
            lastText = label.text;
            timer = duration;
        }

        if (timer <= 0f) return;      // 没在脉冲就完全不碰缩放

        timer -= Time.deltaTime;

        // t：1 → 0。刚换字时直接顶到 peakScale，然后平滑落回原大小
        float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
        float k = Mathf.Sin(t * Mathf.PI * 0.5f);      // 1 → 0，先快后慢
        transform.localScale = baseScale * Mathf.Lerp(1f, peakScale, k);

        if (timer <= 0f) transform.localScale = baseScale;   // 收尾，精确回到原大小
    }
}
