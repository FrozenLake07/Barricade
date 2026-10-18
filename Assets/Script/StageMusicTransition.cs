using UnityEngine;

/// 挂到过场导演／阶段流程物体，可让按钮或 Timeline Signal 调用。
public class StageMusicTransition : MonoBehaviour
{
    [Tooltip("填场景名字：开场结束填 1，建造结束填 2")]
    public string nextSceneName = "1";

    [ContextMenu("提前开始衔接音乐")]
    public void BeginMusic() => RhythmManager.PrepareMusicForScene(nextSceneName);

    [ContextMenu("播放衔接音乐并进入下一场景")]
    public void GoToNextScene() => RhythmManager.LoadSceneWithMusic(nextSceneName);
}
