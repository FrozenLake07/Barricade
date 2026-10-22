using UnityEngine;

[CreateAssetMenu(menuName="街垒/巴黎像素界面样式")]
public sealed class ParisUIStyle : ScriptableObject
{
    public Sprite frame,paper,leather,leadBall,titleBackground,titleMark,heart,heartEmpty;
    public Color ink=new Color(.18f,.16f,.14f),cream=new Color(.84f,.80f,.71f);
    public Color red=new Color(.46f,.19f,.17f),muted=new Color(.60f,.56f,.48f);
    public Color heartTint=new Color(.72f,.62f,.58f),heartEmptyTint=new Color(.28f,.31f,.34f);
    public Color surface=new Color(.055f,.065f,.075f,.88f);
    [Tooltip("资源以低分辨率采样、Point过滤导出。此值控制九宫格边缘的显示密度。")]
    public float borderDensity=1f;
}

// Shared by the authored prefab, fallback menu and scene-local rhythm widgets.
public static class ParisUI
{
    static ParisUIStyle style;
    public static ParisUIStyle Style
    {
        get
        {
            if(style==null)style=Resources.Load<ParisUIStyle>("UI/Paris1832/Style");
            if(style==null)throw new System.InvalidOperationException("缺少 UI/Paris1832/Style 界面样式资源。");
            return style;
        }
    }
    public static void Place(RectTransform rect,float x0,float y0,float x1,float y1)
    {
        rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);
        rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        rect.localScale=Vector3.one;
    }
    public static void Behind(Transform decoration,Transform target)
    {decoration.SetSiblingIndex(Mathf.Max(0,target.GetSiblingIndex()-(decoration.GetSiblingIndex()<target.GetSiblingIndex()?1:0)));}
    public static UnityEngine.UI.Image Panel(Transform parent,string name,Sprite sprite,float x0,float y0,float x1,float y1)
    {
        var node=parent.Find(name);
        if(node==null){var go=new GameObject(name,typeof(RectTransform),sprite==null?typeof(ParisBackdropGraphic):typeof(UnityEngine.UI.Image));node=go.transform;node.SetParent(parent,false);}
        var image=node.GetComponent<UnityEngine.UI.Image>();if(image==null)image=node.gameObject.AddComponent<UnityEngine.UI.Image>();
        Place((RectTransform)node,x0,y0,x1,y1);Paint(image,sprite);return image;
    }
    public static void Paint(UnityEngine.UI.Image image,Sprite sprite)
    {
        image.sprite=sprite;image.color=sprite!=null?Color.white:Style.surface;image.raycastTarget=false;
        image.type=sprite!=null?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier=Mathf.Max(.1f,Style.borderDensity);
    }
    public static void Text(UnityEngine.UI.Text text,int size,Color color,TextAnchor alignment=TextAnchor.MiddleCenter)
    {
        if(text==null)return;text.font=GameFont.Silver;text.fontSize=size;text.color=color;text.raycastTarget=false;
        text.alignment=alignment;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Max(18,size-8);text.resizeTextMaxSize=size;
        var shadow=text.GetComponent<UnityEngine.UI.Shadow>();if(shadow!=null)shadow.enabled=false;
    }
    public static UnityEngine.UI.Text Label(Transform parent,string name,string content,int size,Color color,float x0,float y0,float x1,float y1)
    {
        var node=parent.Find(name);if(node==null){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Text));node=go.transform;node.SetParent(parent,false);}
        var text=node.GetComponent<UnityEngine.UI.Text>();text.text=content;Text(text,size,color);Place(text.rectTransform,x0,y0,x1,y1);return text;
    }
    public static void Button(UnityEngine.UI.Button button,float x0,float y0,float x1,float y1)
    {
        if(button==null)return;Place((RectTransform)button.transform,x0,y0,x1,y1);
        StyleButton(button);
        foreach(var text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {Place(text.rectTransform,.06f,.08f,.94f,.92f);Text(text,44,Style.cream,TextAnchor.MiddleLeft);}
        var navigation=button.navigation;navigation.mode=UnityEngine.UI.Navigation.Mode.Vertical;button.navigation=navigation;
    }
    public static void StyleButton(UnityEngine.UI.Button button)
    {
        var image=button.GetComponent<UnityEngine.UI.Image>();Paint(image,null);image.color=Color.white;image.raycastTarget=true;button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=new Color(.08f,.09f,.10f,.015f);colors.highlightedColor=new Color(.30f,.10f,.09f,.18f);
        colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.40f,.12f,.10f,.34f);colors.disabledColor=new Color(.04f,.04f,.04f,0);colors.fadeDuration=.12f;button.colors=colors;
        var accent=button.GetComponent<ParisMenuAccent>();if(accent==null)accent=button.gameObject.AddComponent<ParisMenuAccent>();accent.RefreshStyle();
    }
}
