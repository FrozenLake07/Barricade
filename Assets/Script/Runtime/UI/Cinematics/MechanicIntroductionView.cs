using System.Collections;
using UnityEngine;

// Uses the existing campaign Canvas, palette and Silver game font.
public sealed class MechanicIntroductionView:MonoBehaviour
{
    GameObject page;
    UnityEngine.UI.Text eyebrow,heading,body;
    UnityEngine.UI.Button confirm;
    bool requested;
    float shownAt;
    int shownFrame;
    public bool Visible=>page!=null&&page.activeInHierarchy;
    public bool CanContinue=>Visible&&Time.frameCount>shownFrame+1&&Time.unscaledTime-shownAt>=.35f&&!CampaignSession.InputBlocked&&!DebugConsole.SimulationPaused&&!DebugConsole.CapturingInput;
    public string Heading=>heading!=null?heading.text:"";
    public string Body=>body!=null?body.text:"";
    public void Initialize(StartScreenView screen)
    {
        if(page!=null&&page.transform.parent==screen.transform)return;
        if(page!=null)Destroy(page);
        var canvas=screen.GetComponent<Canvas>();if(canvas!=null&&canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null)canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        page=Node("MechanicIntroduction",screen.transform,0,0,1,1);
        var background=page.AddComponent<UnityEngine.UI.Image>();background.color=new Color(.035f,.043f,.05f,1);background.raycastTarget=true;ParisPrintGraphic.Attach(background,3);
        eyebrow=Text("Eyebrow",30,ParisUI.Style.muted,.17f,.83f,.83f,.89f);
        heading=Text("Heading",64,ParisUI.Style.cream,.17f,.68f,.83f,.81f);
        var rule=Node("Rule",page.transform,.17f,.655f,.83f,.658f).AddComponent<UnityEngine.UI.Image>();rule.color=ParisUI.Style.red;rule.raycastTarget=false;
        body=Text("Rules",40,ParisUI.Style.cream,.17f,.25f,.83f,.62f);body.alignment=TextAnchor.UpperLeft;body.lineSpacing=1.18f;body.resizeTextMinSize=32;
        var button=Node("Continue",page.transform,.36f,.115f,.64f,.205f);var surface=button.AddComponent<UnityEngine.UI.Image>();surface.color=ParisUI.Style.surface;
        confirm=button.AddComponent<UnityEngine.UI.Button>();confirm.targetGraphic=surface;surface.color=Color.white;
        var colors=confirm.colors;colors.normalColor=new Color(.13f,.14f,.15f,1);colors.highlightedColor=new Color(.23f,.20f,.18f,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.30f,.22f,.19f,1);colors.disabledColor=new Color(.075f,.08f,.085f,1);colors.fadeDuration=.12f;confirm.colors=colors;
        var label=ParisUI.Label(button.transform,"Label","明白了，继续",36,ParisUI.Style.cream,.02f,0,.98f,1);label.supportRichText=false;
        confirm.onClick.AddListener(()=>RequestContinue());
        Text("KeyboardHint",26,ParisUI.Style.muted,.17f,.05f,.83f,.10f).text="空格 / Enter 继续";
        var pause=screen.transform.Find("PausePage");if(pause!=null)page.transform.SetSiblingIndex(pause.GetSiblingIndex());
        page.SetActive(false);
    }
    UnityEngine.UI.Text Text(string name,int size,Color color,float x0,float y0,float x1,float y1)
    {
        var label=ParisUI.Label(page.transform,name,"",size,color,x0,y0,x1,y1);label.supportRichText=false;return label;
    }
    static GameObject Node(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);ParisUI.Place((RectTransform)go.transform,x0,y0,x1,y1);return go;
    }
    public bool RequestContinue(bool debug=false)
    {if(!Visible||(!debug&&!CanContinue))return false;requested=true;return true;}
    public IEnumerator Show(int chapter,string chapterName,string title,string rules)
    {
        requested=false;shownAt=Time.unscaledTime;shownFrame=Time.frameCount;
        eyebrow.text="第 "+chapter+" 章 · "+chapterName+"  /  新机制";heading.text=title;body.text=rules;
        confirm.interactable=false;page.SetActive(true);
        try
        {
            while(Visible&&!requested)
            {
                confirm.interactable=CanContinue;
                if(CanContinue&&(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)))RequestContinue();
                yield return null;
            }
        }
        finally{Hide();CampaignSession.BlockCurrentInput();}
    }
    public void Hide(){if(page!=null)page.SetActive(false);}
    void OnDestroy(){if(page!=null)Destroy(page);}
}
