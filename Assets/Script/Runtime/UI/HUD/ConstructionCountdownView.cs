using UnityEngine;

// Remaining construction time in four-beat blocks; reads the campaign clock without affecting input or timing.
[DefaultExecutionOrder(230)]
public sealed class ConstructionCountdownView : MonoBehaviour
{
    public const int BeatsPerSegment=4;
    GameObject panel;
    readonly System.Collections.Generic.List<UnityEngine.UI.Image> segments=new System.Collections.Generic.List<UnityEngine.UI.Image>();
    StartScreenView view;
    int previousRemaining=-1,previousTotal=-1;
    public int RemainingBeats {get;private set;}
    public int LitSegments {get;private set;}
    public int TotalSegments=>segments.Count;
    public bool Visible=>panel!=null&&panel.activeInHierarchy;
    public static void Attach(StartScreenView screen)
    {
        var countdown=screen.GetComponent<ConstructionCountdownView>();
        if(countdown==null)countdown=screen.gameObject.AddComponent<ConstructionCountdownView>();
        countdown.Bind(screen);
    }
    void Bind(StartScreenView screen)
    {
        view=screen;
        if(panel==null)
        {
            panel=new GameObject("ConstructionCountdown",typeof(RectTransform),typeof(UnityEngine.UI.Image));panel.transform.SetParent(screen.transform,false);
            ParisUI.Place((RectTransform)panel.transform,.35f,.921f,.70f,.941f);
            var background=panel.GetComponent<UnityEngine.UI.Image>();background.color=new Color(.025f,.035f,.045f,.88f);background.raycastTarget=false;
            var pause=screen.transform.Find("PausePage");if(pause!=null)panel.transform.SetSiblingIndex(pause.GetSiblingIndex());
        }
        previousRemaining=previousTotal=-1;Refresh();
    }
    void LateUpdate(){Refresh();}
    void Refresh()
    {
        if(panel==null)return;
        var story=StoryDirector.Current;
        bool show=StoryDirector.Active&&story.Phase==CampaignPhase.Preparation&&!story.PreparationHandoff&&!story.PreparationFeedback
            &&GameManager.WallHP<GameManager.WallMaxHP&&!CampaignDebug.Value.hideHud&&!view.titlePage.activeSelf;
        panel.SetActive(show);if(!show)return;
        int total=story.Chapter.preparationBeats;
        RemainingBeats=Mathf.Max(0,total-story.Clock.Completed);
        if(total==previousTotal&&RemainingBeats==previousRemaining)return;
        int count=Mathf.Max(1,Mathf.CeilToInt(total/(float)BeatsPerSegment));
        if(count!=segments.Count)
        {
            foreach(var segment in segments)Destroy(segment.gameObject);segments.Clear();
            for(int i=0;i<count;i++)
            {
                var go=new GameObject("Segment"+(i+1),typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(panel.transform,false);
                float step=.95f/count,gap=Mathf.Min(.003f,step*.12f);
                ParisUI.Place((RectTransform)go.transform,.025f+i*step+gap,.225f,.025f+(i+1)*step-gap,.785f);
                var image=go.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=false;segments.Add(image);
            }
        }
        LitSegments=Mathf.CeilToInt(RemainingBeats/(float)BeatsPerSegment);
        var lit=RemainingBeats<=8?new Color(.80f,.34f,.29f):ParisUI.Style.cream;
        for(int i=0;i<segments.Count;i++)segments[i].color=i<LitSegments?lit:new Color(.19f,.21f,.23f,.9f);
        previousRemaining=RemainingBeats;previousTotal=total;
    }
}
