using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Shares the authored Canvas and EventSystem; pause pages remain above this layer.
public sealed class CampaignCinematicView : MonoBehaviour
{
    GameObject page,namePage,blackCardPage;
    UnityEngine.UI.Image previous,current;
    UnityEngine.UI.Text subtitle,skipText,errorText,prompt,placeholder,response,centerTitle;
    UnityEngine.UI.InputField input;
    UnityEngine.UI.Button confirm,unnamed;
    Material creditsBlur,savedPictureMaterial;
    bool pictureCredits;
    AudioSource voice;
    StoryDirector musicOwner;
    CampaignCinematics config;
    CinematicPlaybackState playback;
    double[] activeDurations;
    float namingSeconds;
    int shownFrame=-1,lastCompositionFrame=-100,shotStartedFrame=-1;
    public bool LastShotSkipRequested {get;private set;}
    public bool CanSkipFinalPicture=>Active&&shownFrame>=0&&activeDurations!=null&&shownFrame==activeDurations.Length-1&&Time.frameCount>shotStartedFrame+1&&Time.unscaledTime-shotStartedAt>.3f;
    float shotStartedAt;
    bool nameVisible,wasBlocked,voiced,debugSkip;
    public void DebugSkip(){debugSkip=true;}
    public bool IsNaming=>nameVisible;
    public bool Active=>page!=null&&page.activeSelf;
    public UnityEngine.UI.Image CurrentImage=>current;
    public bool IsPictureCredits=>pictureCredits&&IsBlackCardVisible;
    public bool HasNarrativeSubtitle=>Active&&(nameVisible||!string.IsNullOrWhiteSpace(subtitle.text)||!string.IsNullOrWhiteSpace(response.text));
    public bool IsBlackCardVisible=>blackCardPage!=null&&blackCardPage.activeInHierarchy;
    public bool Composing=>!string.IsNullOrEmpty(Input.compositionString)||Time.frameCount<=lastCompositionFrame+1;

    public bool RequestFinalPictureSkip()
    {
        if(!CanSkipFinalPicture||CampaignSession.InputBlocked||DebugConsole.SimulationPaused||DebugConsole.CapturingInput)return false;
        LastShotSkipRequested=true;playback?.Skip();CampaignSession.BlockCurrentInput();return true;
    }
    public void Initialize(StartScreenView screen,StoryDirector director)
    {
        Hide();if(page!=null)Destroy(page);
        musicOwner=director;
        var canvas=screen.GetComponent<Canvas>();
        if(canvas!=null&&canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null)canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        page=Node("CampaignCinematics",screen.transform,Vector2.zero,Vector2.one);
        var background=page.AddComponent<UnityEngine.UI.Image>();background.color=Color.black;background.raycastTarget=true;
        var viewport=Node("PictureViewport",page.transform,Vector2.zero,Vector2.one);
        var aspect=viewport.AddComponent<UnityEngine.UI.AspectRatioFitter>();aspect.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=16f/9f;
        viewport.AddComponent<UnityEngine.UI.RectMask2D>();
        previous=Picture("Previous",viewport.transform);current=Picture("Current",viewport.transform);
        var shade=Node("SubtitleShade",page.transform,new Vector2(0,0),new Vector2(1,.2f)).AddComponent<ParisBackdropGraphic>();
        shade.fadeMode=3;shade.color=new Color(.015f,.02f,.025f,.88f);shade.raycastTarget=false;
        subtitle=Label("Subtitle",shade.transform,"",32,.07f,.23f,.93f,.84f);SubtitleSizePreference.Attach(subtitle);
        skipText=Label("Skip",page.transform,"",23,.76f,.93f,.97f,.98f);skipText.color=ParisUI.Style.muted;
        namePage=Node("NameEntry",page.transform,new Vector2(.20f,.04f),new Vector2(.80f,.35f));
        var nameShade=namePage.AddComponent<ParisBackdropGraphic>();nameShade.fadeMode=4;nameShade.color=new Color(.025f,.03f,.04f,.94f);nameShade.raycastTarget=true;
        Rule(namePage.transform,.04f,.94f,.96f,.947f,ParisUI.Style.red);
        prompt=Label("Prompt",namePage.transform,"",30,.05f,.71f,.95f,.90f);
        var field=Node("PlayerNameInput",namePage.transform,new Vector2(.09f,.40f),new Vector2(.91f,.68f));
        var surface=field.AddComponent<UnityEngine.UI.Image>();surface.color=new Color(.10f,.11f,.12f,.95f);surface.raycastTarget=true;
        input=field.AddComponent<UnityEngine.UI.InputField>();input.targetGraphic=surface;
        var value=Label("Text",field.transform,"",38,.025f,.02f,.975f,.98f);value.alignment=TextAnchor.MiddleLeft;
        placeholder=Label("Placeholder",field.transform,"",30,.025f,.02f,.975f,.98f);placeholder.color=ParisUI.Style.muted;placeholder.alignment=TextAnchor.MiddleLeft;
        input.textComponent=value;input.placeholder=placeholder;input.lineType=UnityEngine.UI.InputField.LineType.SingleLine;
        input.contentType=UnityEngine.UI.InputField.ContentType.Standard;input.characterLimit=0;input.caretColor=ParisUI.Style.cream;input.customCaretColor=true;
        input.selectionColor=new Color(.5f,.16f,.14f,.6f);
        Rule(namePage.transform,.09f,.39f,.91f,.398f,ParisUI.Style.muted);
        errorText=Label("Validation",namePage.transform,"",22,.09f,.28f,.91f,.39f);
        confirm=Button("Confirm",namePage.transform,.08f,.035f,.48f,.25f,()=>Submit(false));
        unnamed=Button("Unnamed",namePage.transform,.52f,.035f,.92f,.25f,()=>Submit(true));
        response=Label("NameResponse",page.transform,"",32,.08f,.045f,.92f,.14f);SubtitleSizePreference.Attach(response);
        blackCardPage=Node("BlackCardLayer",page.transform,Vector2.zero,Vector2.one);
        var cardBackground=blackCardPage.AddComponent<UnityEngine.UI.Image>();cardBackground.color=Color.black;cardBackground.raycastTarget=true;
        centerTitle=Label("OpeningCenteredText",blackCardPage.transform,"",44,.12f,.30f,.88f,.70f);
        centerTitle.gameObject.SetActive(false);
        blackCardPage.SetActive(false);
        if(voice==null)voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;EffectsVolumeGain.Set(voice,1);
        var pause=screen.transform.Find("PausePage");if(pause!=null)page.transform.SetSiblingIndex(pause.GetSiblingIndex());
        page.SetActive(false);namePage.SetActive(false);
    }
    static GameObject Node(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return go;
    }
    static UnityEngine.UI.Image Picture(string name,Transform parent)
    {
        var image=Node(name,parent,Vector2.zero,Vector2.one).AddComponent<UnityEngine.UI.Image>();
        image.preserveAspect=true;image.raycastTarget=false;image.transform.localScale=Vector3.one*1.025f;return image;
    }
    static UnityEngine.UI.Text Label(string name,Transform parent,string text,int size,float x0,float y0,float x1,float y1)
    {
        var label=ParisUI.Label(parent,name,text,size,ParisUI.Style.cream,x0,y0,x1,y1);
        label.supportRichText=false;return label;
    }
    static void Rule(Transform parent,float x0,float y0,float x1,float y1,Color color)
    {
        var image=Node("FineRule",parent,new Vector2(x0,y0),new Vector2(x1,y1)).AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;
    }
    static UnityEngine.UI.Button Button(string name,Transform parent,float x0,float y0,float x1,float y1,UnityEngine.Events.UnityAction clicked)
    {
        var go=Node(name,parent,new Vector2(x0,y0),new Vector2(x1,y1));go.AddComponent<UnityEngine.UI.Image>();
        var button=go.AddComponent<UnityEngine.UI.Button>();Label("Label",go.transform,"",30,.04f,0,.96f,1);
        ParisUI.StyleButton(button);button.onClick.AddListener(clicked);return button;
    }
    public IEnumerator Play(CampaignCinematics settings,CinematicRoute route,bool allowSkip,bool retainLast=false,double[] plannedDurations=null,bool skipLastOnAnyKey=false)
    {
        LastShotSkipRequested=false;debugSkip=false;config=settings;config.Validate();
        blackCardPage.SetActive(false);current.transform.parent.gameObject.SetActive(true);subtitle.transform.parent.gameObject.SetActive(true);centerTitle.gameObject.SetActive(false);
        previous.enabled=current.enabled=true;previous.rectTransform.anchoredPosition=current.rectTransform.anchoredPosition=Vector2.zero;
        bool introduce=route.nameAfterShot>=0&&!RunState.ProtagonistIntroduced;
        var durations=new double[route.shots.Length];for(int i=0;i<durations.Length;i++)durations[i]=route.shots[i].Duration;
        if(plannedDurations!=null&&plannedDurations.Length==durations.Length)durations=(double[])plannedDurations.Clone();
        if(introduce&&config.nameResponseVoice!=null)
            durations[route.nameAfterShot+1]=Math.Max(durations[route.nameAfterShot+1],config.nameResponseVoice.length+.15);
        activeDurations=durations;
        playback=new CinematicPlaybackState(durations,introduce?route.nameAfterShot:-1);
        shownFrame=-1;nameVisible=false;wasBlocked=false;lastCompositionFrame=-100;namingSeconds=0;
        musicOwner?.SetCinematicVoiceActive(false);
        response.text="";input.text="";errorText.text="";page.SetActive(true);namePage.SetActive(false);
        prompt.text=config.namePrompt;placeholder.text=config.namePlaceholder;
        confirm.GetComponentInChildren<UnityEngine.UI.Text>().text=string.IsNullOrWhiteSpace(config.confirmLabel)?"确认":config.confirmLabel;
        unnamed.GetComponentInChildren<UnityEngine.UI.Text>().text=string.IsNullOrWhiteSpace(config.continueUnnamedLabel)?"暂不命名":config.continueUnnamedLabel;
        skipText.text=allowSkip?config.skipLabel:"";
        ShowShot(route,playback.Index);
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)current.transform.parent);
        int started=Time.frameCount;
        try
        {
            while(!playback.Finished||DebugConsole.SimulationPaused)
            {
                if(debugSkip){if(route.nameAfterShot>=0&&!RunState.ProtagonistIntroduced){RunState.ProtagonistIntroduced=true;RunState.PlayerName="";}break;}
                if(!string.IsNullOrEmpty(Input.compositionString))lastCompositionFrame=Time.frameCount;
                if(CampaignSession.InputBlocked||DebugConsole.SimulationPaused){wasBlocked=true;yield return null;continue;}
                if(wasBlocked){wasBlocked=false;if(nameVisible)FocusInput();yield return null;continue;}
                if(!DebugConsole.CapturingInput&&allowSkip&&!nameVisible&&Time.frameCount>started+1&&Input.GetKeyDown(KeyCode.Space))
                {playback.Skip();CampaignTelemetry.Current?.Record("transition_skip",route.key);}
                if(playback.Index!=shownFrame)ShowShot(route,playback.Index);
                if(skipLastOnAnyKey&&!nameVisible&&Input.anyKeyDown&&RequestFinalPictureSkip())break;
                if(skipLastOnAnyKey&&CanSkipFinalPicture)skipText.text="按任意键显示结局选项";
                if(playback.AwaitingName&&!nameVisible)
                {
                    nameVisible=true;namePage.SetActive(true);skipText.text="";subtitle.gameObject.SetActive(false);
                    FocusInput();CampaignTelemetry.Current?.Record("name_prompt","opened");
                }
                if(!nameVisible)
                {
                    Animate(route.shots[shownFrame]);
                    playback.Advance(Time.deltaTime);
                }
                else{namingSeconds+=Time.deltaTime;if(input.wasCanceled)FocusInput();}
                musicOwner?.SetCinematicVoiceActive(voice.isPlaying);
                yield return null;
            }
            musicOwner?.CompleteHopeRoute(route);
            CampaignTelemetry.Current?.Record("cinematic_complete",route.key);
        }
        finally
        {
            if(!retainLast)Hide();
            else
            {
                voice.Stop();musicOwner?.SetCinematicVoiceActive(false);namePage.SetActive(false);
                nameVisible=false;skipText.text=skipLastOnAnyKey?"按任意键显示结局选项":"";current.color=Color.white;
                var last=route.shots[route.shots.Length-1];current.sprite=last.image;
                subtitle.text=ProtagonistNameRules.Format(last.subtitle,RunState.PlayerName);
                previous.sprite=current.sprite;previous.color=Color.white;
            }
        }
    }
    public IEnumerator PlayBlackCard(string content,float seconds,bool allowSkip,int fontSize,bool retainLast=false,bool epigraph=false,bool finalPicture=false)
    {
        RestoreCreditsPicture();debugSkip=false;if(seconds<=0)yield break;
        namePage.SetActive(false);nameVisible=false;blackCardPage.SetActive(true);
        subtitle.text=response.text=skipText.text="";
        centerTitle.supportRichText=epigraph;
        centerTitle.font=GameFont.Silver;
        centerTitle.fontStyle=FontStyle.Normal;
        centerTitle.text=content??"";centerTitle.fontSize=fontSize;
        centerTitle.resizeTextForBestFit=!epigraph;centerTitle.resizeTextMinSize=24;centerTitle.resizeTextMaxSize=fontSize;
        SubtitleSizePreference.SetBase(centerTitle,fontSize,24,fontSize);
        centerTitle.alignment=TextAnchor.MiddleCenter;centerTitle.gameObject.SetActive(true);page.SetActive(true);
        if(finalPicture&&current.sprite!=null)
        {
            pictureCredits=true;savedPictureMaterial=current.material;
            if(creditsBlur==null)
            {
                var shader=Resources.Load<Shader>("UI/EndingBlur");
                if(shader!=null)creditsBlur=new Material(shader);
            }
            current.material=creditsBlur;current.color=Color.white;previous.enabled=false;
            current.transform.parent.gameObject.SetActive(true);subtitle.transform.parent.gameObject.SetActive(false);
            CreditsPictureFade(0);
        }
        float elapsed=0;int firstFrame=Time.frameCount;
        try
        {
            while((elapsed<seconds||DebugConsole.SimulationPaused)&&!debugSkip)
            {
                if(CampaignSession.InputBlocked||DebugConsole.SimulationPaused){yield return null;continue;}
                if(!DebugConsole.CapturingInput&&allowSkip&&elapsed>.25f&&Time.frameCount>firstFrame+1&&Input.GetKeyDown(KeyCode.Space))break;
                elapsed+=Time.deltaTime;if(pictureCredits)CreditsPictureFade(Mathf.SmoothStep(0,1,elapsed/.8f));yield return null;
            }
        }
        finally{if(!retainLast)Hide();}
    }
    void CreditsPictureFade(float progress)
    {
        creditsBlur?.SetFloat("_BlurPixels",8*progress);
        blackCardPage.GetComponent<UnityEngine.UI.Image>().color=new Color(.012f,.016f,.022f,.6f*progress);
        var color=centerTitle.color;color.a=progress;centerTitle.color=color;
    }
    void RestoreCreditsPicture()
    {
        if(!pictureCredits)return;
        current.material=savedPictureMaterial;previous.enabled=true;
        blackCardPage.GetComponent<UnityEngine.UI.Image>().color=Color.black;
        var color=centerTitle.color;color.a=1;centerTitle.color=color;pictureCredits=false;
    }
    void ShowShot(CinematicRoute route,int index)
    {
        previous.sprite=shownFrame<0?route.shots[index].image:current.sprite;
        previous.rectTransform.anchoredPosition=current.rectTransform.anchoredPosition;
        previous.color=Color.white;current.sprite=route.shots[index].image;
        GetComponent<CampaignSoundscape>()?.Picture(route.chapter,current.sprite!=null?current.sprite.name:"");
        if(route.shots[index].triggerHopeFinale)musicOwner?.PlayHopeFinale();
        shotStartedFrame=Time.frameCount;shotStartedAt=Time.unscaledTime;shownFrame=index;voiced=false;voice.Stop();subtitle.gameObject.SetActive(true);
        subtitle.text=ProtagonistNameRules.Format(route.shots[index].subtitle,RunState.PlayerName);
        current.color=new Color(1,1,1,index==0?1:0);
        if(route.nameAfterShot>=0&&index==route.nameAfterShot+1&&RunState.ProtagonistIntroduced)
        {
            response.text=ProtagonistNameRules.Format(config.nameResponseTemplate,RunState.PlayerName);
            if(!string.IsNullOrEmpty(response.text)){subtitle.text=response.text;response.text="";}
            if(config.nameResponseVoice!=null){voice.PlayOneShot(config.nameResponseVoice);voiced=true;}
        }
        else response.text="";
    }
    void Animate(CinematicShot shot)
    {
        float age=(float)playback.Age;
        current.color=new Color(1,1,1,shownFrame==0?1:Mathf.SmoothStep(0,1,config.crossfadeSeconds<=0?1:age/config.crossfadeSeconds));
        float progress=Mathf.Clamp01(age/(float)activeDurations[shownFrame]);Vector2 pan=Vector2.Lerp(shot.panFrom,shot.panTo,Mathf.SmoothStep(0,1,progress));
        var size=((RectTransform)current.transform.parent).rect.size;
        current.rectTransform.anchoredPosition=new Vector2(Mathf.Round(pan.x)*size.x/320,Mathf.Round(pan.y)*size.y/180);
        if(!voiced&&age>=shot.voiceDelaySeconds){voiced=true;if(shot.voiceOver!=null){voice.clip=shot.voiceOver;voice.Play();}}
    }
    void FocusInput()
    {
        if(EventSystem.current==null)return;
        EventSystem.current.SetSelectedGameObject(input.gameObject);input.ActivateInputField();
    }
    void Submit(bool withoutName)
    {
        if(DebugConsole.CapturingInput||!nameVisible||CampaignSession.InputBlocked||Composing)return;
        string name="";
        if(!withoutName&&!ProtagonistNameRules.TryNormalize(input.text,out name))
        {SfxPlayer.Play("ui_unavailable",.8f);errorText.text=config.invalidNameMessage;FocusInput();return;}
        SfxPlayer.Play("ui_confirm",.8f);
        RunState.PlayerName=name;RunState.ProtagonistIntroduced=true;
        CampaignTelemetry.Current?.Record("name_confirmed",(withoutName?"unnamed":"named")+";activeSeconds="+namingSeconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
        playback.ConfirmName();nameVisible=false;namePage.SetActive(false);skipText.text="";
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        input.DeactivateInputField();
    }
    public void Hide()
    {
        RestoreCreditsPicture();if(voice!=null)voice.Stop();musicOwner?.SetCinematicVoiceActive(false);
        nameVisible=false;playback=null;if(blackCardPage!=null)blackCardPage.SetActive(false);if(page!=null)page.SetActive(false);
    }
    void OnDestroy(){Hide();if(creditsBlur!=null)Destroy(creditsBlur);if(page!=null)Destroy(page);}
}
