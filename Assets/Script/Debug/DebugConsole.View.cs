using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class DebugConsole
{
    GameObject consoleCanvas,panel,previousSelection;
    UnityEngine.UI.InputField input;
    UnityEngine.UI.Text log,status,beatText;
    UnityEngine.UI.ScrollRect scroll;
    RectTransform content;
    bool viewDirty,beatOverlay,navigationWasEnabled=true;
    float frameSeconds=.016f;int focusAfter=-1,lastComposition=-100;
    EventSystem capturedEvents;
    bool restoreFocusPending;
    int pendingCaret=-1;
    bool ownsPause,previousAudioPause;
    float resumeTimeScale;
    sealed class FrozenTimeline
    {public UnityEngine.Playables.PlayableAsset asset;public UnityEngine.Playables.DirectorUpdateMode mode;public double time;}
    readonly System.Collections.Generic.Dictionary<UnityEngine.Playables.PlayableDirector,FrozenTimeline> pausedTimelines=new System.Collections.Generic.Dictionary<UnityEngine.Playables.PlayableDirector,FrozenTimeline>();
    public static bool SimulationPaused=>Current!=null&&Current.IsOpen&&!Current.stepping;
    void PauseSimulation()
    {
        if(!ownsPause){resumeTimeScale=Time.timeScale;previousAudioPause=AudioListener.pause;ownsPause=true;}
        HoldSimulation();
    }
    void HoldSimulation()
    {
        if(!ownsPause)return;
        AudioListener.pause=true;
        if(stepping)return;
        Time.timeScale=0;
        foreach(var director in FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None))
        {
            if(pausedTimelines.TryGetValue(director,out var held))
            {
                if(director.playableAsset!=held.asset||!director.playableGraph.IsValid())continue;
                director.timeUpdateMode=UnityEngine.Playables.DirectorUpdateMode.Manual;
                if(director.state==UnityEngine.Playables.PlayState.Playing)director.Pause();
                director.time=held.time;
            }
            else if(director.state==UnityEngine.Playables.PlayState.Playing)
            {
                pausedTimelines[director]=new FrozenTimeline{asset=director.playableAsset,mode=director.timeUpdateMode,time=director.time};
                director.timeUpdateMode=UnityEngine.Playables.DirectorUpdateMode.Manual;director.Pause();
            }
        }
    }
    void ResumeSimulation()
    {
        if(!ownsPause)return;
        ownsPause=false;Time.timeScale=resumeTimeScale;AudioListener.pause=previousAudioPause;
        foreach(var pair in pausedTimelines)
        {
            if(pair.Key==null)continue;
            pair.Key.timeUpdateMode=pair.Value.mode;
            if(pair.Key.playableAsset==pair.Value.asset&&pair.Key.playableGraph.IsValid()&&pair.Key.state==UnityEngine.Playables.PlayState.Paused)
            {pair.Key.time=pair.Value.time;pair.Key.Resume();}
        }
        pausedTimelines.Clear();
    }
    void SetGameTimeScale(float scale)
    {if(ownsPause){resumeTimeScale=scale;if(!stepping)Time.timeScale=0;}else Time.timeScale=scale;}
    static GameObject Node(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;
        r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;return g;
    }
    UnityEngine.UI.Text Label(string name,Transform parent,string text,int size,float x0,float y0,float x1,float y1)
    {
        var t=Node(name,parent,x0,y0,x1,y1).AddComponent<UnityEngine.UI.Text>();t.font=owner.font;t.text=text;t.fontSize=size;
        t.color=ParisUI.Style.cream;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;t.supportRichText=false;return t;
    }
    void BuildView()
    {
        consoleCanvas=Node("DebugConsoleCanvas",transform,0,0,1,1);
        var canvas=consoleCanvas.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;
        var scaler=consoleCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        consoleCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        panel=Node("InputCapture",consoleCanvas.transform,0,0,1,1);var blocker=panel.AddComponent<UnityEngine.UI.Image>();blocker.color=new Color(0,0,0,.08f);
        var box=Node("ConsolePanel",panel.transform,.025f,.32f,.975f,.97f);box.AddComponent<UnityEngine.UI.Image>().color=new Color(.025f,.03f,.035f,.96f);
        var rule=Node("Rule",box.transform,0,.992f,1,1).AddComponent<UnityEngine.UI.Image>();rule.color=ParisUI.Style.red;rule.raycastTarget=false;
        Label("Title",box.transform,"街垒 / 调试控制台",30,.018f,.91f,.48f,.986f);
        Label("Shortcut",box.transform,"~ / Esc 关闭    ↑↓ 历史    Tab 补全",23,.52f,.91f,.98f,.986f).alignment=TextAnchor.MiddleRight;
        var viewportRoot=Node("ScrollView",box.transform,.02f,.205f,.98f,.90f);
        scroll=viewportRoot.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
        var viewport=Node("Viewport",viewportRoot.transform,0,0,1,1);viewport.AddComponent<UnityEngine.UI.Image>().color=Color.black;viewport.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        var cg=Node("Content",viewport.transform,0,1,1,1);content=(RectTransform)cg.transform;content.pivot=new Vector2(.5f,1);
        var layout=cg.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        var fitter=cg.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        log=Label("Output",cg.transform,"",25,0,0,1,1);log.alignment=TextAnchor.UpperLeft;log.supportRichText=true;log.horizontalOverflow=HorizontalWrapMode.Wrap;log.verticalOverflow=VerticalWrapMode.Overflow;
        scroll.viewport=(RectTransform)viewport.transform;scroll.content=content;
        var field=Node("CommandInput",box.transform,.02f,.072f,.86f,.177f);var surface=field.AddComponent<UnityEngine.UI.Image>();surface.color=new Color(.11f,.12f,.13f,.98f);
        input=field.AddComponent<UnityEngine.UI.InputField>();input.targetGraphic=surface;input.textComponent=Label("Text",field.transform,"",29,.015f,.05f,.985f,.95f);
        var placeholder=Label("Placeholder",field.transform,"输入 help 查看命令…",26,.015f,.05f,.985f,.95f);placeholder.color=ParisUI.Style.muted;input.placeholder=placeholder;
        input.characterLimit=16384;input.lineType=UnityEngine.UI.InputField.LineType.SingleLine;input.customCaretColor=true;input.caretColor=ParisUI.Style.cream;input.selectionColor=new Color(.5f,.16f,.14f,.6f);
        var copy=Node("CopyOutput",box.transform,.875f,.072f,.98f,.177f);var copyImage=copy.AddComponent<UnityEngine.UI.Image>();copyImage.color=new Color(.22f,.19f,.17f,1);
        var button=copy.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=copyImage;button.onClick.AddListener(()=>{GUIUtility.systemCopyBuffer=PlainOutput;Focus();});
        Label("Label",copy.transform,"复制输出",25,.05f,0,.95f,1).alignment=TextAnchor.MiddleCenter;
        status=Label("Status",box.transform,"",21,.02f,.006f,.98f,.064f);status.color=ParisUI.Style.muted;
        beatText=Label("BeatOverlay",consoleCanvas.transform,"",26,.28f,.45f,.72f,.56f);beatText.alignment=TextAnchor.MiddleCenter;beatText.gameObject.SetActive(false);
        panel.SetActive(false);
    }
    public void SetOpen(bool open)
    {
        if(IsOpen==open||!CampaignDebug.Available)return;
        if(restoreFocusPending){RestoreFocus();restoreFocusPending=false;}
        blockedUntil=Time.frameCount+1;IsOpen=open;panel.SetActive(open);
        if(open)
        {
            PauseSimulation();
            capturedEvents=EventSystem.current;
            if(capturedEvents!=null){previousSelection=capturedEvents.currentSelectedGameObject;navigationWasEnabled=capturedEvents.sendNavigationEvents;capturedEvents.sendNavigationEvents=false;capturedEvents.SetSelectedGameObject(null);}
            focusAfter=Time.frameCount+1;
        }
        else{ResumeSimulation();input.DeactivateInputField();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);restoreFocusPending=true;}
    }
    void RestoreFocus()
    {
        if(capturedEvents==null)return;capturedEvents.sendNavigationEvents=navigationWasEnabled;
        capturedEvents.SetSelectedGameObject(previousSelection!=null&&previousSelection.activeInHierarchy?previousSelection:null);capturedEvents=null;previousSelection=null;
    }
    void Focus(){if(input==null||!IsOpen||EventSystem.current==null)return;EventSystem.current.SetSelectedGameObject(input.gameObject);input.ActivateInputField();}
    void SetInput(string value){input.text=value;input.caretPosition=value.Length;pendingCaret=value.Length;Focus();}
    void Update()
    {
        if(IsOpen)HoldSimulation();
        frameSeconds=Mathf.Lerp(frameSeconds,Time.unscaledDeltaTime,.08f);
        if(!string.IsNullOrEmpty(Input.compositionString))lastComposition=Time.frameCount;
        bool composing=Time.frameCount<=lastComposition+1;
        if(Input.GetKeyDown(KeyCode.BackQuote)&&!composing){SetOpen(!IsOpen);return;}
        if(!IsOpen)return;
        if(Input.GetKeyDown(KeyCode.Escape)&&!composing){SetOpen(false);return;}
        if(Time.frameCount<=focusAfter)return;
        if(composing)return;
        if(Input.GetKeyDown(KeyCode.UpArrow))SetInput(history.Move(-1,input.text));
        else if(Input.GetKeyDown(KeyCode.DownArrow))SetInput(history.Move(1,input.text));
        else if(Input.GetKeyDown(KeyCode.Tab))
        {
            string prefix=input.text.Trim();if(prefix.Contains(" "))return;
            var matches=commands.Keys.Where(k=>k.StartsWith(prefix,System.StringComparison.OrdinalIgnoreCase)).ToArray();
            if(matches.Length==1)SetInput(matches[0]+" ");else if(matches.Length>1)Print(string.Join("  ",matches));
        }
        else if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string line=input.text.Trim();if(line.Length>0){history.Add(line);Print("> "+line);Execute(line);}input.text="";focusAfter=Time.frameCount+1;
        }
    }
    void RefreshView()
    {
        if(IsOpen)HoldSimulation();
        // Restore after EventSystem.Update, so the closing ` key cannot type into the name field.
        if(restoreFocusPending){RestoreFocus();restoreFocusPending=false;}
        if(pendingCaret>=0){input.caretPosition=pendingCaret;pendingCaret=-1;}
        if(IsOpen&&Time.frameCount>=focusAfter&&!input.isFocused)Focus();
        if(viewDirty&&log!=null){log.text=string.Join("\n",output);Canvas.ForceUpdateCanvases();UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);scroll.verticalNormalizedPosition=0;viewDirty=false;}
        if(status!=null)status.text=confirmation!=null?"等待二次确认：confirm / cancel":busy?"执行中："+activeCommand+"    可用 skip / time / beat / help":"控制台暂停中    当前测试："+(CampaignDebug.Value.nonDesign?"非设计值":"设计值")+"    排队 "+queue.Count;
        if(beatText!=null){beatText.gameObject.SetActive(beatOverlay);if(beatOverlay)beatText.text=BeatInfo();}
    }
}
