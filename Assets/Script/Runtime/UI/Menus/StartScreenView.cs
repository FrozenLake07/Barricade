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
    public const string FirstLaunchCoverSeenKey="Barricade.Title.FirstLaunchCoverSeen.v1";
    public const int FirstLaunchCoverIndex=-2;
    int selectedTitleCover=-1;
    static int previousTitleCover=-1;
    static readonly System.Random coverRandom=new System.Random();
    public int TitleCoverIndex=>selectedTitleCover;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCoverHistory(){previousTitleCover=-1;}
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
        ApplyTitleCover(true);
        prompt.color=promptColor;titlePage.SetActive(true);
    }
    public void ApplyTitleCover(bool choose=false)
    {
        if(titlePage==null)return;
        var style=ParisUI.Style;int count=style.titleCovers?.Length??0;
        if(count==0&&style.firstLaunchCover==null)return;
        if(choose&&style.firstLaunchCover!=null&&PlayerPrefs.GetInt(FirstLaunchCoverSeenKey,0)==0)
        {
            selectedTitleCover=FirstLaunchCoverIndex;
            PlayerPrefs.SetInt(FirstLaunchCoverSeenKey,1);PlayerPrefs.Save();
        }
        else if(choose&&count>0)
        {
            bool exclude=previousTitleCover>=0&&previousTitleCover<count&&count>1;
            selectedTitleCover=coverRandom.Next(count-(exclude?1:0));
            if(exclude&&selectedTitleCover>=previousTitleCover)selectedTitleCover++;
            previousTitleCover=selectedTitleCover;
        }
        else if(selectedTitleCover!=FirstLaunchCoverIndex||style.firstLaunchCover==null)selectedTitleCover=count>0?Mathf.Clamp(selectedTitleCover,0,count-1):-1;
        bool first=selectedTitleCover==FirstLaunchCoverIndex&&style.firstLaunchCover!=null;
        var page=titlePage.transform;var background=page.Find("Background").GetComponent<UnityEngine.UI.Image>();
        background.sprite=first?style.firstLaunchCover:selectedTitleCover>=0?style.titleCovers[selectedTitleCover]??style.titleBackground:style.titleBackground;background.color=Color.white;
        background.preserveAspect=true;
        var fit=background.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        if(fit==null)fit=background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        fit.aspectRatio=16f/9f;fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
        bool right=first||selectedTitleCover==1;float x0=right?.55f:.07f,x1=right?.94f:.46f;
        var title=page.Find("GameTitle").GetComponent<UnityEngine.UI.Text>();
        ParisUI.Place(title.rectTransform,x0,.72f,x1,.90f);title.alignment=TextAnchor.MiddleLeft;
        var mark=page.Find("GameTitleMark");if(mark!=null)ParisUI.Place((RectTransform)mark,x0,.66f,x1,.96f);
        var year=page.Find("PlaceAndYear");if(year!=null)ParisUI.Place((RectTransform)year,x0,.65f,x1,.71f);
        var veil=page.Find("TitlePaper");
        if(veil!=null)
        {
            ParisUI.Place((RectTransform)veil,right?.4f:0,0,right?1:.60f,1);
            var shade=veil.GetComponent<ParisBackdropGraphic>();if(shade!=null){shade.fadeMode=right?5:0;shade.color=new Color(.018f,.023f,.03f,.70f);shade.SetVerticesDirty();}
        }
        var menu=CampaignMenuView.Ensure(this);ParisUI.Place((RectTransform)menu.titleMenu.transform,0,0,1,1);
        var settings=GetComponent<GameSettingsView>();
        if(settings!=null)settings.LayoutTitle(menu,x0,x1);
        else
        {
            ParisUI.Button(menu.newGame,x0,.46f,x1,.54f);
            ParisUI.Button(menu.continueGame,x0,.35f,x1,.43f);
            ParisUI.Button(menu.titleQuit,x0,.24f,x1,.32f);
            ParisUI.Place(menu.titleStatus.rectTransform,x0,.08f,x1,.20f);
        }
        ParisUI.Place(prompt.rectTransform,x0,.38f,x1,.52f);prompt.alignment=TextAnchor.MiddleLeft;
        ParisUI.Place(subtitle.rectTransform,x0,.025f,x1,.075f);
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
