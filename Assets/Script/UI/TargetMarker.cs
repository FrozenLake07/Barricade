using UnityEngine;

/// 准星与开火共用目标选择，状态提示只读取战斗状态，不提示 Perfect 时间。
public class TargetMarker : MonoBehaviour
{
    [Header("准星")]
    public Transform marker;
    public Vector3 offset = Vector3.zero;

    [Header("四种状态图片")]
    public Sprite readySprite;
    public Sprite switchingSprite;
    public Sprite reloadingSprite;
    public Sprite noTargetSprite;

    [Tooltip("从空环到满环排列；根据真实换弹进度选择，不按固定速度播放")]
    public Sprite[] reloadFrames = new Sprite[0];

    [Header("无目标时的位置")]
    [Min(0f)]
    [Tooltip("在当前攻击轨道上、玩家左侧多远显示中心点")]
    public float noTargetDistance = 3f;

    public enum MarkerState { Ready, Switching, Reloading, NoTarget }
    public MarkerState CurrentState { get; private set; }

    SpriteRenderer markerSprite;
    SBullet shooter;

    void Start()
    {
        if (marker == null || (markerSprite = marker.GetComponent<SpriteRenderer>()) == null)
        {
            Debug.LogWarning("[TargetMarker] 请指定带 SpriteRenderer 的 marker，准星已停用。");
            enabled = false;
            return;
        }

        shooter = GetComponent<SBullet>();
        if (shooter == null)
        {
            SBullet[] found = FindObjectsByType<SBullet>(FindObjectsSortMode.None);
            if (found.Length > 0) shooter = found[0];
        }
        if (shooter == null)
        {
            Debug.LogWarning("[TargetMarker] 场景里没有 SBullet，准星已隐藏。");
            markerSprite.enabled = false;
            enabled = false;
            return;
        }

        if (readySprite == null) readySprite = markerSprite.sprite;
        if (switchingSprite == null || reloadingSprite == null || noTargetSprite == null)
            Debug.LogWarning("[TargetMarker] 四种准星图片尚未全部配置，缺失状态将使用普通准星。");
    }

    // 等目标移动和输入处理完成后再摆放，切换、换弹完成的同帧即可更新。
    void LateUpdate()
    {
        if (marker == null || markerSprite == null || shooter == null) return;
        Transform target = shooter.CurrentTarget();
        Vector3 anchor = target != null ? target.position
            : new Vector3(shooter.transform.position.x - noTargetDistance,
                          CapsuleTrack.AttackY, shooter.transform.position.z);
        marker.position = anchor + offset;

        Sprite sprite;
        // 没有目标也能看到切换和换弹状态。
        if (CapsuleTrack.IsSwitching)
        {
            CurrentState = MarkerState.Switching;
            sprite = switchingSprite;
        }
        else if (shooter.IsReloading)
        {
            CurrentState = MarkerState.Reloading;
            sprite = reloadingSprite;
            if (reloadFrames != null && reloadFrames.Length > 0)
            {
                float progress = StoryDirector.Active ? shooter.CampaignReloadProgress
                    : 1f - shooter.ReloadRemaining / Mathf.Max(0.001f, shooter.reloadTime);
                int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * (reloadFrames.Length - 1)),
                                        0, reloadFrames.Length - 1);
                if (reloadFrames[index] != null) sprite = reloadFrames[index];
            }
        }
        else if (target == null)
        {
            CurrentState = MarkerState.NoTarget;
            sprite = noTargetSprite;
        }
        else
        {
            // 玩家需依靠心跳和音乐判断时机，准星不提供 Perfect 窗口提示。
            CurrentState = MarkerState.Ready;
            sprite = readySprite;
        }

        markerSprite.sprite = sprite != null ? sprite : readySprite;
        markerSprite.enabled = markerSprite.sprite != null;
        // PNG已包含深浅层次，保持白色，避免二次染色或重复降低透明度。
        markerSprite.color = Color.white;
    }
}
