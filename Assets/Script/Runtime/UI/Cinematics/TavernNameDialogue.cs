using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;

// A name gate holds only its own Timeline; campaign music and UI keep running.
public sealed class TavernNameDialogue : MonoBehaviour
{
    public PlayableDirector director;
    public NameResponseTable responses;
    public TavernDialogueLines conversation;
    [Tooltip("完整酒馆演出使用独立 Signal 播放第二段姓名回应。")]
    public bool nameReplyOnSeparateSignal;
    public bool echoNameAfterConfirmation;
    public GameObject entryPanel,replyPanel;
    public UnityEngine.UI.InputField input;
    public UnityEngine.UI.Text errorText,replySpeaker,replyText;
    public UnityEngine.UI.Button confirmButton,unnamedButton,continueButton;
    public CanvasGroup authoredDialogue;
    enum Gate {Idle,Naming,Reply}
    Gate phase;
    TavernDialogueLines.Line pendingThird;
    static TavernNameDialogue active;
    int blockedThrough=-1,lastCompositionFrame=-100;
    bool pausedByUs,wasBlocked,wired;
    float originalDialogueAlpha=1;
    public bool IsNaming=>phase==Gate.Naming;
    public bool Waiting=>phase!=Gate.Idle;
    public string SelectedRuleId {get;private set;}="";
    public bool Composing=>!string.IsNullOrEmpty(Input.compositionString)||Time.frameCount<=lastCompositionFrame+1;
    public static bool InputCaptured=>active!=null&&active.Waiting;
    public static bool ComposingInput=>active!=null&&active.IsNaming&&active.Composing;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){active=null;}
    void Awake(){Wire();Hide();}
    void Wire()
    {
        SubtitleSizePreference.Attach(replyText);SubtitleSizePreference.Attach(replySpeaker);
        if(wired)return;
        if(confirmButton==null||unnamedButton==null||continueButton==null)return;
        confirmButton.onClick.AddListener(ConfirmName);unnamedButton.onClick.AddListener(ContinueUnnamed);continueButton.onClick.AddListener(ContinueResponse);
        if(director!=null)director.stopped+=Stopped;wired=true;
    }
    public void AskName()
    {
        // Editor scrubbing Signals must not change saved names or open input UI.
        if(!Application.isPlaying||RunState.ProtagonistIntroduced||Waiting)return;
        BeginEntry();
    }
    public void FirstDialogue(){ShowStory(conversation!=null?conversation.first:null);}
    public void ThirdDialogue()
    {
        // This existing Signal is at 9.4s, after Enjolras finishes walking at 9.0s.
        if(!Application.isPlaying||Waiting)return;
        var third=conversation!=null?conversation.third:null;
        var response=responses!=null?responses.Resolve(RunState.PlayerName):null;
        if(response!=null&&response.extraBeforeThird&&!string.IsNullOrWhiteSpace(ProtagonistNameRules.Format(response.extraReply,RunState.PlayerName)))
        {
            pendingThird=third;BeginReply(response.extraSpeaker,response.extraReply);
            if(!Waiting)pendingThird=null;
        }
        else ShowStory(third);
    }
    public void FourthDialogue(){ShowStory(conversation!=null?conversation.fourth:null);}
    void ShowStory(TavernDialogueLines.Line line)
    {
        if(!Application.isPlaying||Waiting||line==null)return;
        BeginReply(line.speaker,line.text);
    }
    public void SecondDialogue()
    {
        if(Application.isPlaying)BeginNameResponse();
    }
    void BeginNameResponse()
    {
        if(Waiting||responses==null)return;
        var response=responses.Resolve(RunState.PlayerName);SelectedRuleId=response!=null?response.id:"";
        BeginReply(response!=null?response.speaker:"",response!=null?response.reply:"");
    }
    void BeginReply(string speaker,string text)
    {
        string content=ProtagonistNameRules.Format(text,RunState.PlayerName);
        if(string.IsNullOrWhiteSpace(content)||director==null||replyPanel==null)return;
        if(!Waiting)
        {
            Wire();active=this;pausedByUs=director.state==PlayState.Playing;
            if(pausedByUs)director.Pause();
            if(authoredDialogue!=null){originalDialogueAlpha=authoredDialogue.alpha;authoredDialogue.alpha=0;}
        }
        phase=Gate.Reply;
        entryPanel.SetActive(false);replySpeaker.text=ProtagonistNameRules.Format(speaker,RunState.PlayerName);replyText.text=content;
        replyPanel.SetActive(true);blockedThrough=Time.frameCount+1;wasBlocked=false;
    }
    void BeginEntry()
    {
        if(director==null||responses==null||entryPanel==null||replyPanel==null||input==null||errorText==null||replySpeaker==null||replyText==null)
        {Debug.LogError("[TavernNameDialogue] 姓名输入引用不完整。",this);return;}
        Wire();active=this;phase=Gate.Naming;SelectedRuleId="";
        pausedByUs=director.state==PlayState.Playing;
        if(pausedByUs)director.Pause();
        if(authoredDialogue!=null){originalDialogueAlpha=authoredDialogue.alpha;authoredDialogue.alpha=0;}
        replyPanel.SetActive(false);entryPanel.SetActive(true);errorText.text="";input.text="";
        blockedThrough=Time.frameCount+1;lastCompositionFrame=-100;wasBlocked=false;Focus();
    }
    void Update()
    {
        if(!Waiting)return;
        if(!string.IsNullOrEmpty(Input.compositionString))lastCompositionFrame=Time.frameCount;
        bool blocked=CampaignSession.InputBlocked||DebugConsole.CapturingInput||DebugConsole.SimulationPaused;
        if(blocked){wasBlocked=true;return;}
        if(wasBlocked){wasBlocked=false;blockedThrough=Time.frameCount+1;if(IsNaming)Focus();return;}
        if(Time.frameCount<=blockedThrough||Composing)return;
        if(IsNaming)
        {
            if(input.wasCanceled)Focus();
            if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter))ConfirmName();
        }
        else if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)||Input.GetKeyDown(KeyCode.Space))ContinueResponse();
    }
    bool CanAct()=>Waiting&&!CampaignSession.InputBlocked&&!DebugConsole.CapturingInput&&!DebugConsole.SimulationPaused&&Time.frameCount>blockedThrough&&!Composing;
    void Focus()
    {
        if(input==null||EventSystem.current==null)return;
        EventSystem.current.SetSelectedGameObject(input.gameObject);input.ActivateInputField();
    }
    public void ConfirmName()=>Commit(false);
    public void ContinueUnnamed()=>Commit(true);
    void Commit(bool withoutName)
    {
        if(phase!=Gate.Naming||!CanAct())return;
        string name="";
        if(!withoutName&&!ProtagonistNameRules.TryNormalize(input.text,out name))
        {errorText.text="请输入1至12个字符，不含换行或控制字符。";if(Application.isPlaying)SfxPlayer.Play("ui_unavailable",.8f);Focus();return;}
        RunState.PlayerName=name;RunState.ProtagonistIntroduced=true;
        var response=responses.Resolve(name);SelectedRuleId=response!=null?response.id:"";
        CampaignTelemetry.Current?.Record("name_confirmed",(withoutName?"unnamed":"named")+";source=tavern;rule="+SelectedRuleId);
        if(Application.isPlaying)SfxPlayer.Play("ui_confirm",.8f);
        input.DeactivateInputField();if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);entryPanel.SetActive(false);
        replySpeaker.text=nameReplyOnSeparateSignal?(echoNameAfterConfirmation&&!string.IsNullOrEmpty(name)?"主角":""):ProtagonistNameRules.Format(response!=null?response.speaker:"",name);
        replyText.text=nameReplyOnSeparateSignal?(echoNameAfterConfirmation?name:""):ProtagonistNameRules.Format(response!=null?response.reply:"",name);
        blockedThrough=Time.frameCount+1;CampaignSession.BlockCurrentInput();
        if(string.IsNullOrWhiteSpace(replyText.text)){Finish();return;}
        phase=Gate.Reply;replyPanel.SetActive(true);
    }
    public void ContinueResponse()
    {
        if(phase!=Gate.Reply||!CanAct())return;
        if(Application.isPlaying)SfxPlayer.Play("ui_confirm",.8f);
        if(pendingThird!=null)
        {
            var next=pendingThird;pendingThird=null;
            if(!string.IsNullOrWhiteSpace(ProtagonistNameRules.Format(next.text,RunState.PlayerName)))
            {
                // Keep the same pause and original UI alpha until both lines are confirmed.
                BeginReply(next.speaker,next.text);CampaignSession.BlockCurrentInput();return;
            }
        }
        Finish();
    }
    void Finish()
    {
        bool resume=pausedByUs;pausedByUs=false;Hide();CampaignSession.BlockCurrentInput();
        if(resume&&director!=null&&director.playableGraph.IsValid())director.Resume();
    }
    void Hide()
    {
        pendingThird=null;
        if(phase!=Gate.Idle&&authoredDialogue!=null)authoredDialogue.alpha=originalDialogueAlpha;
        phase=Gate.Idle;if(active==this)active=null;
        if(entryPanel!=null)entryPanel.SetActive(false);if(replyPanel!=null)replyPanel.SetActive(false);
    }
    void Stopped(PlayableDirector source){pausedByUs=false;Hide();}
    void OnDisable(){pausedByUs=false;Hide();}
    void OnDestroy()
    {
        if(wired)
        {
            if(confirmButton!=null)confirmButton.onClick.RemoveListener(ConfirmName);
            if(unnamedButton!=null)unnamedButton.onClick.RemoveListener(ContinueUnnamed);
            if(continueButton!=null)continueButton.onClick.RemoveListener(ContinueResponse);
            if(director!=null)director.stopped-=Stopped;
        }
        if(active==this)active=null;
    }
}
