using UnityEngine;
using UnityEngine.Playables;

// Attach a prepared ending prefab to the future tavern scene; no combat win rule is invented here.
[DisallowMultipleComponent]
public sealed class HeroSequenceController : MonoBehaviour
{
    public HeroActor actor;
    public PlayableDirector director;
    [Tooltip("关闭时结尾保持末帧，直到主动调用ResumeGameplay")]
    public bool resumeWhenStopped;

    void OnEnable()
    {
        if (director != null) director.stopped += OnStopped;
    }
    void OnDisable()
    {
        if (director != null) director.stopped -= OnStopped;
    }
    [ContextMenu("播放已配置的结尾Timeline")]
    public void Play()
    {
        if (actor == null || director == null || director.playableAsset == null) return;
        actor.SetFacingLeft(true);
        actor.SetExternalControl(true);
        director.time = 0d;
        director.Play();
    }
    public void Stop()
    {
        if (director != null) director.Stop();
    }
    public void ResumeGameplay()
    {
        if (director != null) director.Stop();
        if (actor != null) actor.ResumeGameplay();
    }
    void OnStopped(PlayableDirector stopped)
    {
        if (resumeWhenStopped && actor != null) actor.ResumeGameplay();
    }
}
