using UnityEngine;

public static class ParisSceneUI
{
    public static void Construction(Tap tap)
    {
        if(tap.WordUI==null)return;
        var key=tap.WordUI;ParisUI.Text(key,76,ParisUI.Style.cream);
        var pulse=key.GetComponent<KeyPulse>();if(pulse!=null){pulse.peakScale=1.12f;pulse.duration=.16f;}
        ConstructionCueView.Attach(tap);
        if(tap.cntui!=null)tap.cntui.gameObject.SetActive(false);
    }
    public static void Heart(HeartFill heart)
    {
        if(heart.fill==null)return;
        var rect=heart.transform as RectTransform;if(rect==null)return;
        rect.anchorMin=rect.anchorMax=new Vector2(.92f,.93f);rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(72,72);
        foreach(var image in heart.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            image.raycastTarget=false;
            image.sprite=image==heart.fill?ParisUI.Style.heart:ParisUI.Style.heartEmpty;
            image.color=image==heart.fill?ParisUI.Style.heartTint:ParisUI.Style.heartEmptyTint;
            // Keep the existing Heartbeat and audio scheduler in its scene. The shared HUD mirrors its visuals only.
            image.enabled=!StoryDirector.Active||Object.FindFirstObjectByType<AmmoHudView>()==null;
            ParisUI.Place(image.rectTransform,0,0,1,1);image.preserveAspect=true;
        }
        heart.fill.type=UnityEngine.UI.Image.Type.Filled;heart.fill.fillMethod=UnityEngine.UI.Image.FillMethod.Vertical;heart.fill.fillOrigin=0;
    }
}
