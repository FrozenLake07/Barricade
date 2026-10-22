using UnityEngine;

// Read the scene's original heart after its heartbeat/audio scheduler; never create a second scheduler.
[DefaultExecutionOrder(200),RequireComponent(typeof(CanvasGroup))]
public sealed class AmmoHudView : MonoBehaviour
{
    public AmmoHudGraphic illustration;
    public UnityEngine.UI.Text heading,counter,status,effect;
    public CanvasGroup visibility;
    public RectTransform heartPulse;
    public UnityEngine.UI.Image heartFill,heartEmpty,backdrop;
    HeartFill sourceHeart;
    Heartbeat sourcePulse;
    SBullet gun;
    string previousKey;
    void Awake()
    {
        if(visibility==null)visibility=GetComponent<CanvasGroup>();visibility.alpha=0;visibility.blocksRaycasts=false;visibility.interactable=false;
        foreach(var text in new[]{heading,counter,status,effect})if(text!=null){text.font=GameFont.Silver;text.raycastTarget=false;}
        EnsureHeart();
    }
    void EnsureHeart()
    {
        if(heartPulse==null)
        {
            var node=transform.Find("HeartPulse");if(node==null){var go=new GameObject("HeartPulse",typeof(RectTransform));node=go.transform;node.SetParent(transform,false);ParisUI.Place((RectTransform)node,.02f,.22f,.21f,.80f);}
            heartPulse=(RectTransform)node;
        }
        if(heartEmpty==null)heartEmpty=ParisUI.Panel(heartPulse,"HeartEmpty",ParisUI.Style.heart,0,0,1,1);
        if(heartFill==null)heartFill=ParisUI.Panel(heartPulse,"HeartFill",ParisUI.Style.heart,0,0,1,1);
        heartEmpty.sprite=ParisUI.Style.heartEmpty;heartFill.sprite=ParisUI.Style.heart;heartEmpty.type=UnityEngine.UI.Image.Type.Simple;heartEmpty.color=ParisUI.Style.heartEmptyTint;
        heartEmpty.preserveAspect=heartFill.preserveAspect=true;heartEmpty.raycastTarget=heartFill.raycastTarget=false;heartFill.color=ParisUI.Style.heartTint;
        heartFill.type=UnityEngine.UI.Image.Type.Filled;heartFill.fillMethod=UnityEngine.UI.Image.FillMethod.Vertical;heartFill.fillOrigin=0;
    }
    void LateUpdate()
    {
        bool show=!CampaignDebug.Value.hideHud&&StoryDirector.Active&&CampaignHudPresentation.VitalsVisible(StoryDirector.Current.Phase);
        visibility.alpha=show?1:0;if(!show)return;
        if(sourceHeart==null){sourceHeart=FindFirstObjectByType<HeartFill>();sourcePulse=sourceHeart!=null?sourceHeart.GetComponent<Heartbeat>():null;}
        heartFill.fillAmount=sourceHeart!=null&&sourceHeart.fill!=null?sourceHeart.fill.fillAmount:CampaignHudPresentation.HealthRatio(GameManager.WallHP,GameManager.WallMaxHP);
        heartPulse.localScale=sourcePulse!=null?sourcePulse.transform.localScale:Vector3.one;
        bool combat=CampaignHudPresentation.AmmoVisible(StoryDirector.Current.Phase);
        if(combat&&gun==null)gun=FindFirstObjectByType<SBullet>();
        bool showAmmo=combat&&gun!=null;
        illustration.gameObject.SetActive(showAmmo);
        foreach(var text in new[]{heading,counter,status,effect})text.gameObject.SetActive(showAmmo);
        if(backdrop!=null)backdrop.rectTransform.anchorMax=new Vector2(showAmmo?1:.31f,1);
        if(!showAmmo)return;
        bool unlimited=gun.DebugUnlimited;
        var model=AmmoHudState.Build(gun.CampaignAmmo,gun.CampaignCapacity,unlimited,gun.IsReloading,gun.ReloadRemaining,gun.CampaignReloadProgress,RunState.ReloadMultiplier,Time.unscaledTime<gun.ReloadFlashUntil);
        counter.text=model.counter;status.text=model.status;effect.text=model.effect;
        counter.color=ParisUI.Style.cream;status.color=model.mode==AmmoHudMode.Empty?new Color(.73f,.39f,.34f):ParisUI.Style.cream;effect.color=ParisUI.Style.muted;
        string key=model.ammo+":"+model.capacity+":"+model.mode+":"+(int)(model.progress*100)+":"+model.perfectFlash;
        if(key!=previousKey){illustration.SetState(model);previousKey=key;}
    }
    public static AmmoHudView Attach(Transform parent)
    {
        var existing=parent.Find("AmmoHUD");if(existing!=null)return existing.GetComponent<AmmoHudView>();
        var prefab=Resources.Load<GameObject>("UI/AmmoHUD");if(prefab==null){Debug.LogError("[HUD] 缺少Resources/UI/AmmoHUD预制体。");return null;}
        var go=Instantiate(prefab,parent,false);go.name="AmmoHUD";var pause=parent.Find("PausePage");if(pause!=null)go.transform.SetSiblingIndex(pause.GetSiblingIndex());return go.GetComponent<AmmoHudView>();
    }
}
