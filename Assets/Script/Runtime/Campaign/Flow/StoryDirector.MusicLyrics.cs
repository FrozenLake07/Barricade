public sealed partial class StoryDirector
{
    CampaignMusicLyricsView musicLyrics;
    public bool MusicLyricsVisible
    {
        get
        {
            if(ChapterOpeningWordsActive||!musicPlaybackActive||Phase==CampaignPhase.Title||Phase==CampaignPhase.Defense||Phase==CampaignPhase.Preparation||PreparationHandoff)return false;
            bool film=cinematicView!=null&&cinematicView.Active;
            bool first=sequence!=null&&sequence.Current!=null&&sequence.Current.gameObject.activeInHierarchy;
            if(film&&(cinematicView.IsNaming||cinematicView.IsPictureCredits||cinematicView.HasNarrativeSubtitle))return false;
            if(first&&sequence.Subtitle!=null&&!string.IsNullOrWhiteSpace(sequence.Subtitle.text))return false;
            return film||first;
        }
    }
}
