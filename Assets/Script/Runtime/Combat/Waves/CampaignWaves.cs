using UnityEngine;

public sealed class CampaignWaves
{
    readonly WaveDirector scheduler;
    public CampaignWaves(StoryDirector owner,WaveDirector.Progress saved=null)
    {
        scheduler=owner.gameObject.AddComponent<WaveDirector>();scheduler.Initialize(owner,saved==null,saved?.variant);
        if(saved!=null)scheduler.RestoreProgress(saved);
    }
    public void Beat(int beat){if(scheduler!=null)scheduler.Beat(beat);}
    public bool Finished => scheduler==null||scheduler.Finished;
    public void Complete(){if(scheduler!=null)scheduler.MarkCleared();}
    public void Dispose(){if(scheduler!=null){scheduler.enabled=false;Object.Destroy(scheduler);}}
}
