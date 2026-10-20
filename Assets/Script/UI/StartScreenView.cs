using UnityEngine;

// 直接编辑子物体 Text / RectTransform / Image；运行时保存并恢复编辑好的开始页。
[DisallowMultipleComponent]
public sealed class StartScreenView : MonoBehaviour
{
    [Header("界面引用（编辑下方子物体的内容和布局）")]
    public UnityEngine.UI.Text main, detail, hud, caption;
    public UnityEngine.UI.Image black, night;
    [Header("开始页（独立于章节提示与 HUD）")]
    public GameObject titlePage;
    public UnityEngine.UI.Text prompt, subtitle;
    [Header("开始提示呼吸效果")]
    public bool pulsePrompt = true;
    [Min(0f), Tooltip("每秒完整的明暗变化次数，0 为静止。")]
    public float pulseCyclesPerSecond = .2387324f;
    [Range(0f,1f), Tooltip("相对于 Text 原始透明度的最低比例。")]
    public float minimumOpacity = .4f;

    bool captured;
    Color mainColor, promptColor;
    public Color MainColor {get {Capture();return mainColor;}}

    public void Capture()
    {
        if(captured)return;
        mainColor=main.color;promptColor=prompt.color;captured=true;
    }
    public void ShowTitle()
    {
        Capture();main.text=detail.text=hud.text=caption.text="";
        black.color=night.color=Color.clear;
        prompt.color=promptColor;titlePage.SetActive(true);
    }
    public void HideTitle(){titlePage.SetActive(false);}
    public void AnimateTitle(float elapsed)
    {
        var color=promptColor;
        if(pulsePrompt&&pulseCyclesPerSecond>0f)
        {
            float wave=.5f+.5f*Mathf.Sin(elapsed*pulseCyclesPerSecond*Mathf.PI*2f);
            color.a*=Mathf.Lerp(minimumOpacity,1f,wave);
        }
        prompt.color=color;
    }
}
