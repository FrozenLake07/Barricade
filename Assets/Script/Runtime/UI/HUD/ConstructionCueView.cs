using UnityEngine;

// Presentation only: mirror the scene's existing key/pulse and accepted inputs; never query or reveal its Perfect window.
[DefaultExecutionOrder(220)]
public sealed class ConstructionCueView : MonoBehaviour
{
    public UnityEngine.UI.Text keyLabel,actionLabel;
    public CanvasGroup visibility;
    Tap source;
    string accepted;
    int displayFontSize,actionFontSize,actionFontMin,actionFontMax;
    float acceptedUntil,graceUntil;
    public void Bind(Tap tap)
    {
        ParisUI.Place((RectTransform)transform,.35f,.415f,.65f,.585f);
        if(source!=null){source.InputAccepted-=Accepted;source.GraceInputReceived-=Grace;}
        source=tap;source.InputAccepted+=Accepted;source.GraceInputReceived+=Grace;acceptedUntil=graceUntil=0;
        if(displayFontSize==0)displayFontSize=keyLabel.fontSize;
        if(actionFontSize==0){actionFontSize=actionLabel.fontSize;actionFontMin=actionLabel.resizeTextMinSize;actionFontMax=actionLabel.resizeTextMaxSize;}
        keyLabel.font=actionLabel.font=GameFont.Silver;
        source.WordUI.enabled=false;
        visibility.blocksRaycasts=false;visibility.interactable=false;
        Refresh();
    }
    void Accepted(bool perfect)
    {
        graceUntil=0;
        bool repair=StoryDirector.Active&&RunState.Chapter!=2;
        accepted=perfect?"合  拍":GameOptions.Difficulty==GameDifficulty.Nightmare?"未合拍 · 无恢复":repair?"继续修补":"继续建造";acceptedUntil=Time.unscaledTime+.65f;
    }
    void Grace(KeyCode key)
    {
        accepted="切换保护 · "+key;acceptedUntil=graceUntil=Time.unscaledTime+.35f;
    }
    void OnDestroy(){if(source!=null){source.InputAccepted-=Accepted;source.GraceInputReceived-=Grace;}}
    void LateUpdate(){Refresh();}
    void Refresh()
    {
        if(StoryDirector.Active&&StoryDirector.Current.PreparationFeedback)
        {
            visibility.alpha=CampaignDebug.Value.hideHud?0:1;
            keyLabel.text=Time.unscaledTime<acceptedUntil&&source!=null&&source.WordUI!=null?source.WordUI.text:"";
            actionLabel.fontSize=38;actionLabel.resizeTextMinSize=28;actionLabel.resizeTextMaxSize=38;
            string feedback=Time.unscaledTime<acceptedUntil?(accepted=="合  拍"?"合拍 · ":accepted!=null&&accepted.StartsWith("继续")?"+5 · ":""):"";
            actionLabel.text=feedback+StoryDirector.Current.PreparationCompletionText;actionLabel.color=ParisUI.Style.cream;
            return;
        }
        actionLabel.fontSize=actionFontSize;actionLabel.resizeTextMinSize=actionFontMin;actionLabel.resizeTextMaxSize=actionFontMax;
        bool show=!CampaignDebug.Value.hideHud&&source!=null&&source.WordUI!=null&&(!StoryDirector.Active||(StoryDirector.Current.Phase==CampaignPhase.Preparation&&GameManager.WallHP<GameManager.WallMaxHP));
        visibility.alpha=show?1:0;if(!show)return;
        source.WordUI.enabled=false;
        keyLabel.text=source.WordUI.text;keyLabel.fontSize=keyLabel.text.IndexOf('+')>=0?Mathf.Max(20,displayFontSize-6):displayFontSize;
        keyLabel.rectTransform.localScale=source.WordUI.rectTransform.localScale;
        bool resting=keyLabel.text.StartsWith("REST",System.StringComparison.Ordinal);
        actionLabel.text=resting?"稍作喘息":Time.unscaledTime<acceptedUntil?accepted:StoryDirector.Active&&RunState.Chapter!=2?"修  补":"建  造";
        actionLabel.color=resting?new Color(.76f,.40f,.34f):Time.unscaledTime<acceptedUntil?ParisUI.Style.cream:ParisUI.Style.muted;
        keyLabel.color=resting?ParisUI.Style.muted:Time.unscaledTime<graceUntil?new Color(.92f,.76f,.44f):ParisUI.Style.cream;
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
