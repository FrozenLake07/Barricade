using UnityEngine;

// 与现有界面保持uGUI；黑幕、提示和HUD各自独立，不覆盖旧Canvas的配置。
public sealed class CampaignUI
{
    public UnityEngine.UI.Text Main, Detail, Hud, Caption;
    public UnityEngine.UI.Image Black, Night;
    StartScreenView view;
    GameObject root;
    public StartScreenView View => view;
    public CampaignUI(Transform parent,Font font,GameObject prefab=null)
    {
        if(AdoptTitleScene(parent))return;
        if(prefab!=null)
        {
            Bind(Object.Instantiate(prefab,parent,false),font);return;
        }
        var go=new GameObject("CampaignCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
        go.transform.SetParent(parent,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
        var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        Night=Panel("NightMask",go.transform,new Color(0,0,0,0));
        Black=Panel("BlackScreen",go.transform,Color.black);
        Main=Label("MainText",go.transform,font,42,new Vector2(.08f,.39f),new Vector2(.92f,.61f));
        Detail=Label("Detail",go.transform,font,25,new Vector2(.1f,.12f),new Vector2(.9f,.38f));
        Hud=Label("ChapterHUD",go.transform,font,23,new Vector2(.02f,.85f),new Vector2(.98f,.98f));
        Hud.alignment=TextAnchor.UpperLeft;
        Caption=Label("NarrativeCaption",go.transform,font,27,new Vector2(.1f,.02f),new Vector2(.9f,.14f));
        Bind(go,font);
    }
    // 首次进入和通关返回均接管 Title 场景中实际编辑的实例，旧画布同步移除。
    public bool AdoptTitleScene(Transform parent)
    {
        foreach(var candidate in Resources.FindObjectsOfTypeAll<StartScreenView>())
        {
            if(!candidate.gameObject.scene.IsValid()||candidate.gameObject.scene.name!="Title"
                ||!candidate.gameObject.activeInHierarchy)continue;
            var previous=root;
            candidate.transform.SetParent(parent,false);
            Bind(candidate.gameObject,GameFont.Silver);
            if(previous!=null&&previous!=root){previous.SetActive(false);Object.Destroy(previous);}
            return true;
        }
        return false;
    }
    void Bind(GameObject instance,Font font)
    {
        root=instance;
        view=instance.GetComponent<StartScreenView>();
        if(view==null)
        {
            view=instance.AddComponent<StartScreenView>();
            view.main=instance.transform.Find("MainText").GetComponent<UnityEngine.UI.Text>();
            view.detail=instance.transform.Find("Detail").GetComponent<UnityEngine.UI.Text>();
            view.hud=instance.transform.Find("ChapterHUD").GetComponent<UnityEngine.UI.Text>();
            view.caption=instance.transform.Find("NarrativeCaption").GetComponent<UnityEngine.UI.Text>();
            view.black=instance.transform.Find("BlackScreen").GetComponent<UnityEngine.UI.Image>();
            view.night=instance.transform.Find("NightMask").GetComponent<UnityEngine.UI.Image>();
        }
        if(view.titlePage==null)
        {
            // 配置缺失时才创建兼容界面；正式开始页由场景中的预制体提供。
            var page=new GameObject("TitlePage",typeof(RectTransform));
            page.transform.SetParent(instance.transform,false);
            var rect=page.GetComponent<RectTransform>();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            Panel("Background",page.transform,Color.black);
            view.titlePage=page;
            view.prompt=Label("Prompt",page.transform,font,42,new Vector2(.08f,.39f),new Vector2(.92f,.61f));
            view.prompt.text="按下任意按键开始游戏";
            view.subtitle=Label("Subtitle",page.transform,font,27,new Vector2(.1f,.12f),new Vector2(.9f,.38f));
        }
        AmmoHudView.Attach(instance.transform);
        Main=view.main;Detail=view.detail;Hud=view.hud;Caption=view.caption;
        Black=view.black;Night=view.night;
        var silver=GameFont.Silver;
        foreach(var text in instance.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            text.font=silver!=null?silver:font;
        ParisUITheme.Apply(view);
        view.Capture();
    }
    public void ShowTitle(){view.ShowTitle();}
    public void AnimateTitle(float elapsed){view.AnimateTitle(elapsed);}
    static UnityEngine.UI.Image Panel(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;
    }
    static UnityEngine.UI.Text Label(string name,Transform parent,Font font,int size,Vector2 min,Vector2 max)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var text=go.GetComponent<UnityEngine.UI.Text>();text.font=font;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;
        text.color=new Color(.89f,.88f,.83f);text.raycastTarget=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
    }
    public void Card(string main,string detail="")
    {view.HideTitle();Main.color=view.MainColor;Black.color=Color.black;Main.text=main;Detail.text=detail;Caption.text="";Hud.text="";}
    public void Gameplay(float darkness)
    {view.HideTitle();Main.color=view.MainColor;Black.color=Color.clear;Main.text=Detail.text="";Night.color=new Color(0,0,0,darkness);}
}
