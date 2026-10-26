using UnityEngine;

// Explicit opt-in for narrative text; menus, prompts and combat HUD keep their sizes.
public sealed class SubtitleSizePreference : MonoBehaviour
{
    UnityEngine.UI.Text label;
    int size,min,max;
    float applied=-1;
    public static void Attach(UnityEngine.UI.Text text)
    {
        if(text==null||text.GetComponent<SubtitleSizePreference>()!=null)return;
        SetBase(text,text.fontSize,text.resizeTextMinSize,text.resizeTextMaxSize);
    }
    public static void SetBase(UnityEngine.UI.Text text,int size,int min,int max)
    {
        var view=text.GetComponent<SubtitleSizePreference>();if(view==null)view=text.gameObject.AddComponent<SubtitleSizePreference>();
        view.label=text;view.size=size;view.min=min;view.max=max;view.applied=-1;view.Apply();
    }
    void OnEnable(){applied=-1;Apply();}
    void LateUpdate()=>Apply();
    void Apply()
    {
        if(label==null)return;float scale=GamePresentationOptions.SubtitleScale;if(Mathf.Approximately(applied,scale))return;
        applied=scale;label.fontSize=Mathf.RoundToInt(size*scale);label.resizeTextMinSize=Mathf.RoundToInt(min*scale);label.resizeTextMaxSize=Mathf.RoundToInt(max*scale);
    }
}
