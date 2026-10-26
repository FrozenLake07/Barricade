using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Ending-only menu on the existing campaign canvas; the final picture stays behind it.
public sealed class CampaignEndingView : MonoBehaviour
{
    public UnityEngine.UI.Button restartChapter,returnTitle,quit;
    public UnityEngine.UI.Image background;
    GameObject page,menu;
    CanvasGroup fade;
    Material blur;
    public bool Visible=>page!=null&&page.activeSelf;
    public static CampaignEndingView Attach(StartScreenView view)
    {
        var result=view.GetComponent<CampaignEndingView>();if(result==null)result=view.gameObject.AddComponent<CampaignEndingView>();
        if(result.page==null)result.Build(view);return result;
    }
    static GameObject Node(string name,Transform parent,float x0=0,float y0=0,float x1=1,float y1=1)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);ParisUI.Place((RectTransform)go.transform,x0,y0,x1,y1);return go;
    }
    UnityEngine.UI.Button Button(string name,string text,float y,UnityEngine.Events.UnityAction action)
    {
        var go=Node(name,menu.transform,.32f,y,.68f,y+.095f);go.AddComponent<UnityEngine.UI.Image>();
        var button=go.AddComponent<UnityEngine.UI.Button>();ParisUI.StyleButton(button);
        ParisUI.Label(go.transform,"Label",text,38,ParisUI.Style.cream,.06f,.04f,.94f,.96f);
        button.onClick.AddListener(action);return button;
    }
    void Build(StartScreenView view)
    {
        page=Node("EndingMenu",view.transform);page.AddComponent<UnityEngine.UI.Image>().color=Color.black;
        background=Node("FinalPicture",page.transform).AddComponent<UnityEngine.UI.Image>();background.preserveAspect=true;background.raycastTarget=false;
        var shader=Resources.Load<Shader>("UI/EndingBlur");if(shader!=null){blur=new Material(shader);background.material=blur;}
        var shade=Node("Veil",page.transform).AddComponent<UnityEngine.UI.Image>();shade.color=new Color(.012f,.016f,.022f,.5f);shade.raycastTarget=false;
        menu=Node("Choices",page.transform);fade=menu.AddComponent<CanvasGroup>();
        page.SetActive(false);
    }
    public void Show(Sprite picture,UnityEngine.Events.UnityAction restart,UnityEngine.Events.UnityAction title,UnityEngine.Events.UnityAction exit)
    {
        background.sprite=picture;page.SetActive(true);page.transform.SetAsLastSibling();
        if(restartChapter==null)
        {
            restartChapter=Button("RestartChapter","重新开始本章",.52f,restart);
            returnTitle=Button("ReturnTitle","返回标题",.38f,title);
            quit=Button("Quit","结束游戏",.24f,exit);
        }
        else
        {
            restartChapter.onClick.RemoveAllListeners();restartChapter.onClick.AddListener(restart);
            returnTitle.onClick.RemoveAllListeners();returnTitle.onClick.AddListener(title);
            quit.onClick.RemoveAllListeners();quit.onClick.AddListener(exit);
        }
        fade.alpha=0;fade.interactable=fade.blocksRaycasts=false;blur?.SetFloat("_BlurPixels",0);
    }
    public IEnumerator Reveal()
    {
        float age=0;
        while(age<.8f)
        {
            if(CampaignSession.InputBlocked||DebugConsole.SimulationPaused){yield return null;continue;}
            age+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,age/.8f);fade.alpha=t;blur?.SetFloat("_BlurPixels",t*8);yield return null;
        }
        fade.alpha=1;fade.interactable=fade.blocksRaycasts=true;
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(restartChapter.gameObject);
    }
    public void Hide(){if(page!=null)page.SetActive(false);}
    void OnDestroy(){if(blur!=null)Destroy(blur);}
}
