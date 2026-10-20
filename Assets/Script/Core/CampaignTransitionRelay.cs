using UnityEngine;

// 放在未来过场Prefab内；Timeline Signal不必保存跨Prefab的场景引用。
public sealed class CampaignTransitionRelay : MonoBehaviour
{
    public void CompleteTransition()=>StoryDirector.Current?.CompleteTransition();
    public void PlayAudioCue(AudioClip clip)=>StoryDirector.Current?.PlayTransitionCue(clip);
    public void ShowCaption(string text)=>StoryDirector.Current?.ShowCaption(text);
}
