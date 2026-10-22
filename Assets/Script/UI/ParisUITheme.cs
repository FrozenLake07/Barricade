using UnityEngine;

// Retains existing functional objects and serialized references; only their presentation changes.
[DisallowMultipleComponent]
public sealed class ParisUITheme : MonoBehaviour
{
    public bool useAuthoredLayout=true;
    StartScreenView view;
    UnityEngine.UI.Image card,hudPanel,captionPanel;
    public static ParisUITheme Apply(StartScreenView screen)
    {
        var theme=screen.GetComponent<ParisUITheme>();
        if(theme==null){theme=screen.gameObject.AddComponent<ParisUITheme>();theme.useAuthoredLayout=false;}
        theme.Build(screen);return theme;
    }
    void Surface(Transform parent,string name,ref UnityEngine.UI.Image image,float x0,float y0,float x1,float y1)
    {
        var existing=parent.Find(name);
        if(existing!=null&&useAuthoredLayout){image=existing.GetComponent<UnityEngine.UI.Image>();ParisUI.Paint(image,null);image.color=new Color(.018f,.023f,.03f,.94f);}
        else {image=ParisUI.Panel(parent,name,null,x0,y0,x1,y1);image.color=new Color(.018f,.023f,.03f,.94f);}
        if(name=="CountPaper"||name=="ChapterLeather"||name=="CaptionLeather")image.color=Color.clear;
        var scrim=image as ParisBackdropGraphic;if(scrim!=null)scrim.fadeMode=name=="TitlePaper"||name=="ChapterLeather"?0:1;
    }
    void Label(UnityEngine.UI.Text label,int size,TextAnchor alignment=TextAnchor.MiddleCenter)
    {ParisUI.Text(label,useAuthoredLayout?label.fontSize:size,ParisUI.Style.cream,alignment);}
    void Build(StartScreenView screen)
    {
        view=screen;var s=ParisUI.Style;
        Surface(transform,"StoryPaper",ref card,0,0,1,1);ParisUI.Behind(card.transform,view.main.transform);
        Surface(transform,"ChapterLeather",ref hudPanel,.03f,.846f,.70f,.976f);ParisUI.Behind(hudPanel.transform,view.hud.transform);
        Surface(transform,"CaptionLeather",ref captionPanel,.14f,.025f,.86f,.12f);ParisUI.Behind(captionPanel.transform,view.caption.transform);
        if(!useAuthoredLayout)
        {
            ParisUI.Place(view.main.rectTransform,.18f,.66f,.82f,.83f);ParisUI.Place(view.detail.rectTransform,.22f,.22f,.78f,.59f);
            ParisUI.Place(view.hud.rectTransform,.032f,.885f,.39f,.972f);ParisUI.Place(view.caption.rectTransform,.20f,.045f,.80f,.11f);
        }
        Label(view.main,58);view.main.resizeTextForBestFit=false;Label(view.detail,38);Label(view.hud,30,TextAnchor.MiddleLeft);Label(view.caption,30);
        var page=view.titlePage.transform;var background=page.Find("Background").GetComponent<UnityEngine.UI.Image>();
        background.sprite=s.titleBackground;background.type=UnityEngine.UI.Image.Type.Simple;background.color=new Color(.78f,.78f,.76f);background.raycastTarget=false;
        UnityEngine.UI.Image titlePanel=null;Surface(page,"TitlePaper",ref titlePanel,0,0,.70f,1);
        var title=ParisUI.Label(page,"GameTitle","街垒",88,s.cream,.10f,.68f,.34f,.82f);title.enabled=s.titleMark==null;
        var mark=page.Find("GameTitleMark");
        if(mark==null)mark=ParisUI.Panel(page,"GameTitleMark",s.titleMark,.075f,.565f,.405f,.845f).transform;
        var logo=mark.GetComponent<UnityEngine.UI.Image>();logo.sprite=s.titleMark;logo.type=UnityEngine.UI.Image.Type.Simple;logo.preserveAspect=true;logo.color=Color.white;logo.raycastTarget=false;
        var year=page.Find("PlaceAndYear");if(year!=null){Label(year.GetComponent<UnityEngine.UI.Text>(),30,TextAnchor.MiddleLeft);year.GetComponent<UnityEngine.UI.Text>().color=ParisUI.Style.muted;}
        Label(view.prompt,36,TextAnchor.MiddleLeft);Label(view.subtitle,24,TextAnchor.MiddleLeft);
        if(!useAuthoredLayout)
        {ParisUI.Place(view.prompt.rectTransform,.10f,.42f,.34f,.58f);ParisUI.Place(view.subtitle.rectTransform,.10f,.14f,.34f,.20f);}
        StyleMenus(CampaignMenuView.Ensure(view));
        var gameplay=GetComponent<CampaignGameplayHud>();if(gameplay==null)gameplay=gameObject.AddComponent<CampaignGameplayHud>();gameplay.Bind(view);Refresh();
    }
    void StyleMenus(CampaignMenuView menu)
    {
        if(!useAuthoredLayout)
        {
            ParisUI.Button(menu.newGame,.095f,.365f,.365f,.435f);ParisUI.Button(menu.continueGame,.095f,.265f,.365f,.335f);ParisUI.Button(menu.titleQuit,.095f,.165f,.365f,.235f);
            ParisUI.Place(menu.titleStatus.rectTransform,.09f,.15f,.35f,.23f);
            ParisUI.Button(menu.resume,.38f,.64f,.62f,.72f);ParisUI.Button(menu.save,.38f,.53f,.62f,.61f);
            ParisUI.Button(menu.load,.38f,.42f,.62f,.50f);ParisUI.Button(menu.pauseQuit,.38f,.31f,.62f,.39f);
            ParisUI.Place(menu.pauseStatus.rectTransform,.33f,.15f,.67f,.25f);
        }
        foreach(var button in new[]{menu.newGame,menu.continueGame,menu.titleQuit,menu.resume,menu.save,menu.load,menu.pauseQuit})
        {
            ParisUI.StyleButton(button);
            foreach(var text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))Label(text,44,TextAnchor.MiddleLeft);
            button.GetComponent<ParisMenuAccent>().RefreshStyle();
        }
        Label(menu.titleStatus,24,TextAnchor.MiddleLeft);menu.titleStatus.color=ParisUI.Style.muted;Label(menu.pauseStatus,26);
        foreach(var image in menu.pausePage.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if(image.transform.parent==menu.pausePage.transform&&image.name!="PausePaper")
            {image.sprite=null;image.color=new Color(.012f,.016f,.022f,.78f);image.raycastTarget=true;}
        UnityEngine.UI.Image pausePanel=null;Surface(menu.pausePage.transform,"PausePaper",ref pausePanel,.10f,.08f,.90f,.92f);
        foreach(var text in menu.pausePage.GetComponentsInChildren<UnityEngine.UI.Text>(true))if(text.transform.parent==menu.pausePage.transform)Label(text,text.fontSize);
        foreach(var image in menu.countInPage.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if(image.transform.parent==menu.countInPage.transform&&image.name!="CountPaper")
            {image.sprite=null;image.color=new Color(.025f,.03f,.035f,.75f);image.raycastTarget=false;}
        UnityEngine.UI.Image countPanel=null;Surface(menu.countInPage.transform,"CountPaper",ref countPanel,.23f,.27f,.77f,.73f);
        Label(menu.countInText,120);
        var hint=menu.countInPage.transform.Find("CountHint")??menu.countInPage.transform.Find("Hint");if(hint!=null)Label(hint.GetComponent<UnityEngine.UI.Text>(),32);
    }
    void LateUpdate(){Refresh();}
    public void Refresh()
    {
        if(view==null||card==null)return;bool title=view.titlePage.activeSelf;
        card.gameObject.SetActive(!title&&view.black.color.a>.01f&&(!string.IsNullOrEmpty(view.main.text)||!string.IsNullOrEmpty(view.detail.text)));
        hudPanel.gameObject.SetActive(!title&&!string.IsNullOrEmpty(view.hud.text));captionPanel.gameObject.SetActive(!title&&!string.IsNullOrEmpty(view.caption.text));
        if(card.gameObject.activeSelf){var c=ParisUI.Style.cream;c.a=view.main.color.a;view.main.color=c;view.detail.color=ParisUI.Style.cream;}
    }
}
