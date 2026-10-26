using System.Collections;
using UnityEngine;

public sealed partial class StoryDirector
{
    public bool PreparationHandoff {get;private set;}
    public bool PreparationFeedback {get;private set;}
    public bool DefenseReady {get;private set;}
    public string PreparationCompletionText {get;private set;}
    public const float PreparationFeedbackSeconds=.4f,PreparationFadeOutSeconds=.25f,PreparationFadeInSeconds=.35f;
    Heartbeat handoffHeartbeat;
    bool heartbeatDetached;
    HeroActor handoffHero;
    bool HasAuthoredPreparationTransition=>Chapter.prepareToDefense!=null&&(Chapter.prepareToDefense.timeline!=null||Chapter.prepareToDefense.cinematicPrefab!=null||Chapter.prepareToDefense.waitForSignal);
    public bool SuppressHandoffHeartbeat(Heartbeat source)=>PreparationHandoff&&handoffHeartbeat!=null&&source!=handoffHeartbeat;
    bool HandoffPaused=>CampaignSession.Suspended||DebugConsole.SimulationPaused||GameSettingsView.IsOpen;
    void BeginPreparationFeedback()
    {
        PreparationHandoff=true;PreparationFeedback=true;DefenseReady=false;
        PreparationCompletionText=GameManager.WallHP>=GameManager.WallMaxHP?(Chapter.number==2?"街垒已成":"修补完成"):"敌人来了，准备迎战";
        Phase=CampaignPhase.Transition;
        handoffHeartbeat=FindFirstObjectByType<HeartFill>()?.GetComponent<Heartbeat>();
        handoffHero=FindFirstObjectByType<HeroConstructionDriver>()?.actor;
        if(handoffHero!=null)
        {
            bool carried=handoffHero.IsCarrying;
            handoffHero.SetMoving(false);handoffHero.SetResting(false);
            handoffHero.PlayStory(carried?HeroMotion.Place:HeroMotion.IdleLeft);
        }
        ui.Hud.text=CampaignHudPresentation.Heading(Chapter.number,Chapter.title);
        telemetry.Record("preparation_handoff","feedback;health="+GameManager.WallHP);
    }
    IEnumerator HandoffWait(float seconds)
    {
        float elapsed=0;while(elapsed<seconds){yield return null;if(!HandoffPaused)elapsed+=Time.deltaTime;}
    }
    IEnumerator HandoffFade(float from,float to,float seconds)
    {
        float elapsed=0;ui.Black.color=new Color(0,0,0,from);
        while(elapsed<seconds)
        {
            yield return null;if(HandoffPaused)continue;
            elapsed+=Time.deltaTime;ui.Black.color=new Color(0,0,0,Mathf.Lerp(from,to,Mathf.Clamp01(elapsed/seconds)));
        }
        ui.Black.color=new Color(0,0,0,to);
    }
    IEnumerator ConstructionCompletionHold()
    {
        BeginPreparationFeedback();
        try{yield return HandoffWait(PreparationFeedbackSeconds);}
        finally{CancelPreparationHandoff();}
    }
    // Keep the actual scene heartbeat and its scheduled AudioSources alive across the load.
    // The new scene's duplicate is suppressed before its first audio plan, then replaced.
    void CarryHeartbeatAcrossLoad()
    {
        if(handoffHeartbeat==null)return;
        handoffHeartbeat.transform.SetParent(transform,true);heartbeatDetached=true;
    }
    void AdoptHeartbeatInDefense()
    {
        if(handoffHeartbeat==null)return;
        foreach(var incoming in FindObjectsByType<Heartbeat>(FindObjectsSortMode.None))
        {
            if(incoming==handoffHeartbeat||incoming.GetComponent<HeartFill>()==null)continue;
            var dest=(RectTransform)incoming.transform;var old=(RectTransform)handoffHeartbeat.transform;
            old.SetParent(dest.parent,false);old.anchorMin=dest.anchorMin;old.anchorMax=dest.anchorMax;old.pivot=dest.pivot;
            old.anchoredPosition=dest.anchoredPosition;old.sizeDelta=dest.sizeDelta;old.localRotation=dest.localRotation;old.localScale=Vector3.one;
            incoming.gameObject.SetActive(false);Destroy(incoming.gameObject);heartbeatDetached=false;break;
        }
    }
    IEnumerator SmoothPreparationToDefense()
    {
        BeginPreparationFeedback();signalComplete=false;
        TransitionRequested?.Invoke(Chapter.number,"prepare-to-defense");transitionStarted.Invoke();
        try
        {
            yield return HandoffWait(PreparationFeedbackSeconds);PreparationFeedback=false;
            if(handoffHero!=null)handoffHero.PlayStory(HeroMotion.RaiseGun);
            SfxPlayer.Play("weapon_reload_mechanism",.2f);
            yield return HandoffFade(0,1,PreparationFadeOutSeconds);
            CarryHeartbeatAcrossLoad();
            // Deliberately bypass LoadScene's StopAll so the old heartbeat tail can finish.
            if(!Application.CanStreamedLevelBeLoaded("2"))throw new System.InvalidOperationException("场景未加入Build Settings：2");
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(CampaignSceneNames.Defense);yield return null;
            AdoptHeartbeatInDefense();
            ui.Gameplay(Chapter.number==6?.62f:0);ui.Black.color=Color.black;
            ui.Hud.text=CampaignHudPresentation.Heading(Chapter.number,Chapter.title)+"\n"+GameBindings.CombatHint+"\n准备迎战";
            handoffHero=FindFirstObjectByType<HeroCombatDriver>()?.actor;
            if(handoffHero!=null){handoffHero.ResumeGameplay();handoffHero.SetCombat(true);handoffHero.BeginAction(HeroMotion.RaiseGun);}
            if(Chapter.prepareToDefense?.audio!=null)transitionAudio.PlayOneShot(Chapter.prepareToDefense.audio);
            DefenseReady=true;float readyElapsed=0;int cycle=Clock.Completed;float period=Clock.Period;
            telemetry.Record("preparation_handoff","ready;period="+period);
            yield return HandoffFade(1,0,PreparationFadeInSeconds);
            // Give a full visible beat and release only on a real cycle boundary, so reset does not cut a pulse.
            while(readyElapsed<period||Clock.Completed==cycle||Clock.Elapsed>.001f)
            {
                yield return null;if(!HandoffPaused)readyElapsed+=Time.deltaTime;
            }
            DefenseReady=false;Phase=CampaignPhase.Defense;ResetClock();inputBlockedThroughFrame=Time.frameCount;
            waves=new CampaignWaves(this);RhythmManager.StopMusic();flowBusy=false;
            ui.Hud.text="";telemetry.Record("preparation_handoff","defense");
            if(Chapter.number==5)ShowGameplayHint("开始管理弹药："+GameBindings.Label(GameAction.Reload)+"补弹；在心跳静止的间隙补弹更快。");
            transitionFinished.Invoke();
        }
        finally{CancelPreparationHandoff();}
    }
    void CancelPreparationHandoff()
    {
        PreparationFeedback=DefenseReady=PreparationHandoff=false;
        if(handoffHero!=null&&handoffHero.IsStoryControlled)handoffHero.ResumeGameplay();handoffHero=null;
        if(heartbeatDetached&&handoffHeartbeat!=null)Destroy(handoffHeartbeat.gameObject);
        handoffHeartbeat=null;heartbeatDetached=false;
    }
}
