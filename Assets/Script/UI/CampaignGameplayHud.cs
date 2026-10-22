using UnityEngine;

// Keep the saved/director HUD source intact, but give its heading, controls and objective their own hierarchy.
[DefaultExecutionOrder(210)]
public sealed class CampaignGameplayHud : MonoBehaviour
{
    public StartScreenView view;
    public UnityEngine.UI.Text heading,controls,objective;
    public UnityEngine.UI.Image topVeil,bottomVeil;
    string previous;
    public void Bind(StartScreenView screen)
    {
        view=screen;view.hud.enabled=false;
        var panel=view.transform.Find("ChapterLeather");
        if(heading==null)heading=ParisUI.Label(panel,"GameplayHeading","",42,ParisUI.Style.cream,.015f,.54f,.96f,1);
        if(controls==null)controls=ParisUI.Label(panel,"GameplayControls","",26,ParisUI.Style.muted,.015f,.26f,.96f,.52f);
        if(objective==null)objective=ParisUI.Label(panel,"GameplayObjective","",26,ParisUI.Style.cream,.015f,.02f,.96f,.26f);
        heading.alignment=controls.alignment=objective.alignment=TextAnchor.MiddleLeft;
        if(topVeil==null)topVeil=ParisUI.Panel(view.transform,"GameplayTopVeil",null,0,.81f,1,1);
        var shade=topVeil as ParisBackdropGraphic;if(shade!=null)shade.fadeMode=2;
        topVeil.color=new Color(.012f,.018f,.025f,.86f);ParisUI.Behind(topVeil.transform,panel);
        if(bottomVeil==null)bottomVeil=ParisUI.Panel(view.transform,"GameplayBottomVeil",null,0,0,1,.14f);
        var lower=bottomVeil as ParisBackdropGraphic;if(lower!=null)lower.fadeMode=3;
        bottomVeil.color=new Color(.012f,.018f,.025f,.76f);ParisUI.Behind(bottomVeil.transform,view.caption.transform);
        previous=null;Refresh();
    }
    void LateUpdate(){Refresh();}
    void Refresh()
    {
        if(view==null||heading==null)return;
        bool show=StoryDirector.Active&&!view.titlePage.activeSelf&&CampaignHudPresentation.VitalsVisible(StoryDirector.Current.Phase);
        topVeil.gameObject.SetActive(show);
        bottomVeil.gameObject.SetActive(!view.titlePage.activeSelf&&!string.IsNullOrEmpty(view.caption.text));
        var text=view.hud.text??"";
        if(previous==text)return;previous=text;
        int first=text.IndexOf('\n');heading.text=first<0?text:text.Substring(0,first);
        var rest=first<0?"":text.Substring(first+1);int second=rest.IndexOf('\n');
        controls.text=second<0?rest:rest.Substring(0,second);objective.text=second<0?"":rest.Substring(second+1);
    }
}
