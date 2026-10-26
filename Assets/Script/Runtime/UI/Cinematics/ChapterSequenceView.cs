using System;
using UnityEngine;

[Serializable]
public sealed class ChapterFrame
{
    public Sprite image;
    [Min(1)] public int beats=8;
    [TextArea,Tooltip("可选字幕；为空时不显示字幕区。")] public string subtitle;
    [Tooltip("可选配音，不影响本镜头的拍数。")] public AudioClip voiceOver;
    [Min(0f)] public float voiceDelaySeconds;
}

// Kept under the existing Canvas so Esc/pause and count-in remain above the film.
public sealed class ChapterSequenceView : MonoBehaviour
{
    public static event Action<int,ChapterFrame> FrameStarted;
    public int FrameIndex {get;private set;}=-1;
    public UnityEngine.UI.Image Previous {get;private set;}
    public UnityEngine.UI.Image Current {get;private set;}
    public UnityEngine.UI.Text Subtitle {get;private set;}
    GameObject page;
    AudioSource voice;
    ChapterFrame frame;
    float frameAge,fadeSeconds;
    bool voiced;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEvents(){FrameStarted=null;}
    public void Initialize(StartScreenView screen)
    {
        if(page!=null){if(Application.isPlaying)Destroy(page);else DestroyImmediate(page);}
        page=new GameObject("ChapterOneSequence",typeof(RectTransform));
        page.transform.SetParent(screen.transform,false);Stretch(page.GetComponent<RectTransform>());
        var backdrop=page.AddComponent<UnityEngine.UI.Image>();backdrop.color=Color.black;backdrop.raycastTarget=false;
        Previous=Picture("PreviousFrame");Current=Picture("CurrentFrame");
        var text=new GameObject("SubtitleSlot",typeof(RectTransform),typeof(UnityEngine.UI.Text));text.transform.SetParent(page.transform,false);
        var rect=text.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.08f,.03f);rect.anchorMax=new Vector2(.92f,.16f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        Subtitle=text.GetComponent<UnityEngine.UI.Text>();Subtitle.font=GameFont.Silver;Subtitle.fontSize=32;Subtitle.alignment=TextAnchor.MiddleCenter;
        Subtitle.color=ParisUI.Style.cream;Subtitle.raycastTarget=false;Subtitle.text="";SubtitleSizePreference.Attach(Subtitle);
        var shadow=text.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(2,-2);
        if(voice==null)voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;EffectsVolumeGain.Set(voice,1);
        var pause=screen.transform.Find("PausePage");if(pause!=null)page.transform.SetSiblingIndex(pause.GetSiblingIndex());
        page.SetActive(false);
    }
    UnityEngine.UI.Image Picture(string name)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(page.transform,false);Stretch(go.GetComponent<RectTransform>());
        var image=go.GetComponent<UnityEngine.UI.Image>();image.preserveAspect=true;image.raycastTarget=false;return image;
    }
    static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    public void ShowFrame(int index,ChapterFrame next,float crossfade)
    {
        bool first=FrameIndex<0;Previous.sprite=first?next.image:Current.sprite;Previous.color=Color.white;
        Current.sprite=next.image;Current.color=new Color(1,1,1,first?1:0);
        if(index==0&&FrameIndex!=0){SfxPlayer.Play("ambience_carriage_pass_candidate",.45f);SfxPlayer.Play("ambience_bell_distant_candidate",.3f);}
        FrameIndex=index;frame=next;frameAge=0;fadeSeconds=Mathf.Max(.01f,crossfade);voiced=false;
        Subtitle.text=next.subtitle??"";Subtitle.gameObject.SetActive(!string.IsNullOrWhiteSpace(next.subtitle));
        voice.Stop();page.SetActive(true);FrameStarted?.Invoke(index,next);
    }
    public void Advance(float delta)
    {
        if(frame==null||!page.activeSelf)return;
        frameAge+=Mathf.Max(0,delta);Current.color=new Color(1,1,1,Mathf.SmoothStep(0,1,frameAge/fadeSeconds));
        if(!voiced&&frameAge>=frame.voiceDelaySeconds){voiced=true;if(frame.voiceOver!=null){voice.clip=frame.voiceOver;voice.Play();}}
    }
    public void Hide(){voice.Stop();page.SetActive(false);FrameIndex=-1;frame=null;Subtitle.text="";SubtitleSizePreference.Attach(Subtitle);}
    void OnDestroy(){if(page!=null){if(Application.isPlaying)Destroy(page);else DestroyImmediate(page);}}
}
