using System.Collections;

public sealed partial class StoryDirector
{
    MechanicIntroductionView mechanicIntroduction;
    IEnumerator ShowMechanicIntroduction(int chapter)
    {
        if(!ChapterMechanicIntroduction.TryGet(chapter,table.chordMaxGap,out var title,out var body))yield break;
        Phase=CampaignPhase.Transition;ui.Card("","");cinematicView.Hide();
        if(mechanicIntroduction==null)mechanicIntroduction=gameObject.AddComponent<MechanicIntroductionView>();
        mechanicIntroduction.Initialize(ui.View);
        TransitionRequested?.Invoke(chapter,"mechanic-introduction");telemetry.Record("mechanic_introduction",title);
        yield return mechanicIntroduction.Show(chapter,Chapter.title,title,body);
    }
}
