using UnityEngine;

// Presentation only: mirror the scene's existing key/pulse and accepted inputs; never query or reveal its Perfect window.
[DefaultExecutionOrder(220)]
public sealed class ConstructionCueView : MonoBehaviour
{
    public UnityEngine.UI.Text keyLabel,actionLabel;
    public CanvasGroup visibility;
    Tap source;
    string accepted;
    int displayFontSize;
    float acceptedUntil;
    public void Bind(Tap tap)
    {
        if(source!=null)source.InputAccepted-=Accepted;
        source=tap;source.InputAccepted+=Accepted;acceptedUntil=0;
        if(displayFontSize==0)displayFontSize=keyLabel.fontSize;
        keyLabel.font=actionLabel.font=GameFont.Silver;
        source.WordUI.enabled=false;
        visibility.blocksRaycasts=false;visibility.interactable=false;
        Refresh();
    }
    void Accepted(bool perfect)
    {
        bool repair=StoryDirector.Active&&RunState.Chapter!=2;
        accepted=perfect?"合  拍":repair?"继续修补":"继续建造";acceptedUntil=Time.unscaledTime+.65f;
    }
    void OnDestroy(){if(source!=null)source.InputAccepted-=Accepted;}
    void LateUpdate(){Refresh();}
    void Refresh()
    {
        bool show=!CampaignDebug.Value.hideHud&&source!=null&&source.WordUI!=null&&(!StoryDirector.Active||StoryDirector.Current.Phase==CampaignPhase.Preparation);
        visibility.alpha=show?1:0;if(!show)return;
        source.WordUI.enabled=false;
        keyLabel.text=source.WordUI.text;keyLabel.fontSize=keyLabel.text.IndexOf('+')>=0?Mathf.Max(20,displayFontSize-6):displayFontSize;
        keyLabel.rectTransform.localScale=source.WordUI.rectTransform.localScale;
        bool resting=keyLabel.text.StartsWith("REST",System.StringComparison.Ordinal);
        actionLabel.text=resting?"稍作喘息":Time.unscaledTime<acceptedUntil?accepted:StoryDirector.Active&&RunState.Chapter!=2?"修  补":"建  造";
        actionLabel.color=resting?new Color(.76f,.40f,.34f):Time.unscaledTime<acceptedUntil?ParisUI.Style.cream:ParisUI.Style.muted;
        keyLabel.color=resting?ParisUI.Style.muted:ParisUI.Style.cream;
    }
    public static void Attach(Tap tap)
    {
        Transform parent=tap.WordUI.canvas.transform;
        if(StoryDirector.Active)
        {
            var screen=Object.FindFirstObjectByType<StartScreenView>();if(screen!=null)parent=screen.transform;
        }
        var found=parent.Find("ConstructionCue");ConstructionCueView cue;
        if(found!=null)cue=found.GetComponent<ConstructionCueView>();
        else
        {
            var prefab=Resources.Load<GameObject>("UI/ConstructionCue");
            if(prefab==null){Debug.LogError("缺少 UI/ConstructionCue 建造提示预制体。");return;}
            var go=Object.Instantiate(prefab,parent,false);go.name="ConstructionCue";cue=go.GetComponent<ConstructionCueView>();
            var pause=parent.Find("PausePage");if(pause!=null)go.transform.SetSiblingIndex(pause.GetSiblingIndex());
        }
        cue.Bind(tap);
    }
}
