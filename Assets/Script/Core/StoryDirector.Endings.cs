using UnityEngine;

public sealed partial class StoryDirector
{
    bool MusicSuspended
    {
        get
        {
#if UNITY_EDITOR
            if(UnityEditor.EditorApplication.isPaused)return true;
#endif
            return CampaignSession.Suspended||DebugConsole.CapturingInput;
        }
    }
    void SelectEnding(CampaignEnding ending,int sourceChapter)
    {
        RunState.Ending=ending;RunState.EndingSourceChapter=sourceChapter;RunState.HopeMusicStarted=false;
        telemetry.Record("ending_selected",ending+";source="+sourceChapter);
    }
    void BeginEndingMusic()
    {
        if(RunState.Ending==CampaignEnding.Mourning)PlayMusic(mourningMusic);
        else {StopCampaignMusic();audioSource.clip=null;}
    }
    // Bind a Timeline Signal Receiver to this method at Marius's return to hope.
    public void PlayHopeFinale()
    {
        if(RunState.Chapter!=10||RunState.Ending!=CampaignEnding.Hope||RunState.HopeMusicStarted)return;
        RunState.HopeMusicStarted=true;PlayMusic(finaleMusic);telemetry.Record("ending_hope_music","Finale");
    }
    public void CompleteHopeRoute(CinematicRoute route)
    {
        if(route==null)return;
        foreach(var shot in route.shots)if(shot.triggerHopeFinale){PlayHopeFinale();break;}
    }
    string MusicTrackId()=>audioSource.clip==null?"none":audioSource.clip==mourningMusic?"mourning":audioSource.clip==finaleMusic?"finale":"people";
    AudioClip ResolveMusicTrack(string id)
    {
        if(id=="none")return null;
        if(id=="mourning")return mourningMusic;
        if(id=="finale")return finaleMusic;
        return RunState.Chapter==10?(RunState.Ending==CampaignEnding.Mourning?mourningMusic:RunState.HopeMusicStarted?finaleMusic:null):chapterMusic;
    }
}
