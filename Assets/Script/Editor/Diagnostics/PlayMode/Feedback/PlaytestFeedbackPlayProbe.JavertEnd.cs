#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator JavertEndTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        yield return story.DebugChapter(5,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(2);
        bool exit=false;Action<int,string> listener=(n,k)=>{if(n==5&&k=="chapter-exit")exit=true;};StoryDirector.TransitionRequested+=listener;
        try
        {
            StartCoroutine((IEnumerator)typeof(StoryDirector).GetMethod("AfterDefense",Private).Invoke(story,null));
            Check(story.Phase==CampaignPhase.EndCard&&Get<CampaignUI>(story,"ui").Main.text=="第5章结束","Normal chapter-end card is preserved without obsolete narrative text");
            story.Clock.DebugComplete(2);
            yield return Until(()=>story.Phase==CampaignPhase.Results);
            Check(!exit,"No obsolete chapter-five exit text transition is dispatched");
            Check(Get<CampaignUI>(story,"ui").Main.text=="本章结束","Existing results page remains usable");
            yield return Frames(2);
        }
        finally{StoryDirector.TransitionRequested-=listener;}
        report.coverage="Actual chapter-five AfterDefense completion path in advancing Unity Play; generic EndCard and results retained, obsolete chapter-exit prose not dispatched. Save slots untouched; preferences restored.";
    }
}
#endif
