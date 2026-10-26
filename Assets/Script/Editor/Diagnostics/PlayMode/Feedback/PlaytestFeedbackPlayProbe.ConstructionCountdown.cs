#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator ConstructionCountdownTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        yield return story.DebugChapter(3,true);CampaignDebug.Value.freeze=true;GameManager.WallHP=100;
        var bar=FindFirstObjectByType<ConstructionCountdownView>();Check(bar!=null&&!bar.Visible,"Countdown is hidden in defense");
        yield return story.DebugPhase("prep");yield return Frames(3);
        Check(bar.Visible&&bar.RemainingBeats==48&&bar.TotalSegments==12&&bar.LitSegments==12,"48-beat construction starts with twelve lit four-beat segments");
        CaptureUi(1920,1080,"建造分段倒计时_48拍");CaptureUi(960,540,"建造分段倒计时_960");
        story.Clock.DebugComplete(4);yield return Frames(2);
        Check(bar.RemainingBeats==44&&bar.LitSegments==11,"Four completed beats extinguish exactly one segment");
        CampaignSession.Current.Pause();int remaining=bar.RemainingBeats;yield return Frames(30);
        Check(bar.RemainingBeats==remaining,"Pause freezes displayed remaining construction beats");
        CampaignSession.Current.Resume();yield return Until(()=>!CampaignSession.Suspended);
        story.Clock.DebugComplete(40);yield return Frames(2);
        Check(bar.RemainingBeats==8&&bar.LitSegments==2,"Last eight beats keep exactly two segments");
        CaptureUi(1920,1080,"建造分段倒计时_最后8拍");
        CampaignDebug.Value.hideHud=true;yield return Frames(2);Check(!bar.Visible,"Countdown respects HUD visibility setting");CampaignDebug.Value.hideHud=false;
        yield return story.DebugChapter(7,true);GameManager.WallHP=100;yield return story.DebugPhase("prep");yield return Frames(3);
        Check(bar.Visible&&bar.RemainingBeats==41&&bar.TotalSegments==11,"Chapter seven correctly displays eleven segments for 41 beats");
        story.Clock.DebugComplete(1);yield return Frames(2);Check(bar.RemainingBeats==40&&bar.LitSegments==10,"Partial initial segment extinguishes without rounding remaining time");
        GameManager.WallHP=GameManager.WallMaxHP;yield return Frames(3);Check(!bar.Visible,"Full-health early completion hides countdown during handoff");
        yield return Until(()=>story.Phase==CampaignPhase.Defense&&!story.PreparationHandoff);Check(!bar.Visible,"Countdown stays hidden after transition to defense");
        report.coverage="Actual advancing Play construction UI: 48/41-beat chapters, segment depletion, pause, HUD visibility, full-health handoff and defense hiding; screenshots at 1920x1080 and 960x540. Preferences restored; save slots untouched.";
    }
}
#endif
