using UnityEngine;

public sealed class CampaignMenuView : MonoBehaviour
{
    [Header("开始页菜单")]
    public GameObject titleMenu;
    public UnityEngine.UI.Button newGame,continueGame,titleQuit;
    public UnityEngine.UI.Text titleStatus;
    [Header("暂停页面")]
    public GameObject pausePage;
    public UnityEngine.UI.Button resume,pauseQuit,save,load;
    public UnityEngine.UI.Text pauseStatus;
    [Header("恢复前的三拍倒计时")]
    public GameObject countInPage;
    public UnityEngine.UI.Text countInText;

    // Used only when the assigned UI prefab is missing. The shipped prefab has editable nodes.
    public static CampaignMenuView Ensure(StartScreenView view)
    {
        var menu=view.GetComponent<CampaignMenuView>();
        if(menu==null)menu=view.gameObject.AddComponent<CampaignMenuView>();
        if(menu.titleMenu!=null)return menu;
        menu.titleMenu=Root("TitleMenu",view.titlePage.transform);
        menu.newGame=Button("NewGame","开始游戏",menu.titleMenu.transform,.63f);
        menu.continueGame=Button("ContinueGame","继续游戏",menu.titleMenu.transform,.51f);
        menu.titleQuit=Button("Quit","退出",menu.titleMenu.transform,.39f);
        menu.titleStatus=Label("SaveStatus","",menu.titleMenu.transform,.24f,25);
        menu.pausePage=Root("PausePage",view.transform);Background(menu.pausePage.transform,new Color(0,0,0,.94f));
        Label("Heading","游戏暂停",menu.pausePage.transform,.83f,50);
        menu.resume=Button("Resume","继续",menu.pausePage.transform,.69f);
        menu.save=Button("Save","存档",menu.pausePage.transform,.57f);
        menu.load=Button("Load","读档",menu.pausePage.transform,.45f);
        menu.pauseQuit=Button("Quit","退出",menu.pausePage.transform,.33f);
        menu.pauseStatus=Label("Status","",menu.pausePage.transform,.16f,25);
        menu.countInPage=Root("CountInPage",view.transform);Background(menu.countInPage.transform,new Color(0,0,0,.55f));
        menu.countInText=Label("Count","3",menu.countInPage.transform,.53f,112);
        Label("Hint","跟随心跳，准备继续",menu.countInPage.transform,.36f,30);
        menu.titleMenu.SetActive(false);menu.pausePage.SetActive(false);menu.countInPage.SetActive(false);
        if(view.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null)view.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return menu;
    }
    static GameObject Root(string name,Transform parent)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return go;
    }
    static UnityEngine.UI.Text Label(string name,string content,Transform parent,float y,int size)
    {
        var go=Root(name,parent);var rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.08f,y-.07f);rect.anchorMax=new Vector2(.92f,y+.07f);
        var text=go.AddComponent<UnityEngine.UI.Text>();text.font=GameFont.Silver;text.text=content;text.fontSize=size;text.color=new Color(.89f,.88f,.83f);
        text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
    }
    static UnityEngine.UI.Button Button(string name,string content,Transform parent,float y)
    {
        var go=Root(name,parent);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,y);rect.sizeDelta=new Vector2(440,78);
        var image=go.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.15f,.16f,.18f,.98f);
        var button=go.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        var text=Label("Label",content,go.transform,.5f,40);var textRect=text.rectTransform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;
        return button;
    }
    static void Background(Transform parent,Color color)
    {var go=Root("Background",parent);var image=go.AddComponent<UnityEngine.UI.Image>();image.color=color;}
}
