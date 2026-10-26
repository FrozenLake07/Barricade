#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator TitleCalibrationTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        yield return story.DebugPhase("title");yield return Frames(4);
        var view=FindFirstObjectByType<StartScreenView>();
        var logo=view.titlePage.transform.Find("GameTitleMark").GetComponent<UnityEngine.UI.Image>();
        Check(logo.enabled&&logo.sprite==ParisUI.Style.titleArtwork&&logo.material==UnityEngine.UI.Graphic.defaultGraphicMaterial,"Actual title renders original artwork using default UI material without tricolor or relief");
        CaptureUi(1920,1080,"原色标题");
        yield return story.DebugChapter(5,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(2);
        var run=OpeningCalibration.Run(Get<CampaignUI>(story,"ui"),story.transform);StartCoroutine(run);
        yield return Until(()=>GameObject.Find("CalibrationHeartbeat")!=null&&GameObject.Find("CalibrationHeartbeat").GetComponents<AudioSource>().Length>=2);
        var voices=GameObject.Find("CalibrationHeartbeat").GetComponents<AudioSource>();
        Check(voices.Any(v=>Math.Abs(v.volume-.85f)<.001f)&&voices.Any(v=>Math.Abs(v.volume-.4675f)<.001f),"Actual calibration sources use louder 0.85 primary and 0.4675 secondary heartbeat");
        OpeningCalibration.DebugSkip=true;yield return Until(()=>GameObject.Find("CalibrationHeartbeat")==null);
        bool exit=false;Action<int,string> listener=(n,k)=>{if(n==5&&k=="chapter-exit")exit=true;};
        StoryDirector.TransitionRequested+=listener;
        try{yield return (IEnumerator)typeof(StoryDirector).GetMethod("CompleteWithExit",Private).Invoke(story,null);}
        finally{StoryDirector.TransitionRequested-=listener;}
        Check(story.Phase==CampaignPhase.Results&&!exit,"Chapter five completes directly to results without obsolete slogan transition");
        int[] expected={0,127,48,48,48,48,41,48,48,0};
        Check(story.table.chapters.Select(c=>c.preparationBeats).SequenceEqual(expected)&&story.table.chapters.Sum(c=>c.TotalBeats)==CampaignRevisionRules.ChapterBeatBudget,"Loaded chapter preparation counts match revised allocation and total 1832");
        report.coverage="Actual advancing Play: title UI material and screenshot, scheduled calibration AudioSource volumes, chapter-five completion, loaded construction allocations. No user save slots written; preferences restored.";
    }
}
#endif
