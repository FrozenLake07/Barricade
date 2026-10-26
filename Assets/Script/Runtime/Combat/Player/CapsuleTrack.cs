using UnityEngine;

/// 玩家的攻击轨道。A / D 上下切换（A 降一条、D 升一条），**玩家本身原地不动**。
/// 切换不吃拍，只重新开始短暂的瞄准调整；当前攻击轨道广播成静态值，
/// 子弹直接读 CapsuleTrack.AttackY，只打这条轨道上的敌人和手榴弹。
[DefaultExecutionOrder(-100)] // 同帧切轨道与开枪时，必须先更新轨道和禁射状态。
public class CapsuleTrack : MonoBehaviour
{
    [Header("三条轨道（拖 S1 / S2 / S3 进来，只取它们的 y 坐标）")]
    public Transform s1, s2, s3;

    [Header("起始攻击轨道（0 = 最低那条，1 = 中间，2 = 最高）")]
    [Range(0, 2)] public int startTrack = 1;

    [Header("瞄准调整")]
    [Min(0f)]
    [Tooltip("实际切换攻击轨道或空中／地面瞄准后，禁止开枪的秒数；连续切换重新计时，不累加")]
    public float switchSettleTime = 0.15f;

    static float switchReadyTime;
    static float settleDuration;
    public static bool IsSwitching => Time.timeSinceLevelLoad < switchReadyTime;
    public static float SwitchRemaining => Mathf.Max(0f, switchReadyTime - Time.timeSinceLevelLoad);

    void Awake()
    {
        // 组件随场景重新创建，避免下一局沿用上一局的静态禁射截止时间。
        switchReadyTime = 0f;
        settleDuration = Mathf.Max(0f, switchSettleTime);
    }

    public static void BeginAimAdjustment()
    {
        switchReadyTime = Time.timeSinceLevelLoad + settleDuration;
    }

    /// 当前攻击轨道的序号与它的 y。子弹读这两个值判断该打哪一条
    public static int AttackIndex;
    public static float AttackY;

    Transform[] tracks;
    int track;

    void Start()
    {
        if (s1 == null || s2 == null || s3 == null)
        {
            Debug.LogError("[CapsuleTrack] s1 / s2 / s3 必须都拖上（用 S1、S2、S3 的 Transform）");
            enabled = false;
            return;
        }

        tracks = new[] { s1, s2, s3 };

        // 按 y 从低到高排序，index 0 就是最低那条轨道
        System.Array.Sort(tracks, (a, b) => a.position.y.CompareTo(b.position.y));

        track = Mathf.Clamp(startTrack, 0, tracks.Length - 1);
        // 攻击轨道只决定准星与目标筛选，保留场景中为街垒构图设置的站位。
        BroadcastTrack();

        Debug.Log($"[CapsuleTrack] 就绪：{tracks.Length} 条轨道 y = " +
                  $"{tracks[0].position.y} / {tracks[1].position.y} / {tracks[2].position.y}，" +
                  $"当前攻击轨道 {track}（y = {AttackY}）");
    }

    void Update()
    {
        if (GameSettingsView.IsOpen || CampaignSession.InputBlocked || DebugConsole.CapturingInput || (StoryDirector.Active && !StoryDirector.CombatRunning&&!StoryDirector.Current.DefenseReady)) return;
        if (GameBindings.Down(GameAction.LaneDown)) { Debug.Log("[CapsuleTrack] 收到 A"); TrySwitch(-1); }       // A：降一条
        else if (GameBindings.Down(GameAction.LaneUp)) { Debug.Log("[CapsuleTrack] 收到 D"); TrySwitch(+1); }  // D：升一条
    }

    public void DebugLane(int lane){if(tracks==null)throw new System.InvalidOperationException("轨道尚未初始化。");TrySwitch(lane-1-track);}
    void TrySwitch(int dir)
    {
        int next = track + dir;

        // 没有发生切换就不重置禁射，也不影响节拍。
        if (next < 0 || next >= tracks.Length)
        {
            Debug.Log($"[CapsuleTrack] 已在边缘轨道 {track}，无法切换");
            return;
        }

        track = next;
        BroadcastTrack();          // 逻辑轨道立即更新；准星自行平滑移动，人物保持原位
        settleDuration = Mathf.Max(0f, switchSettleTime);
        BeginAimAdjustment();
        SfxPlayer.Play("SFX-PLY-05", 0.35f);

        Debug.Log($"[CapsuleTrack] 攻击轨道切到 {track}（y = {AttackY}）");
    }

    // 把当前攻击轨道广播出去，子弹那边直接读
    void BroadcastTrack()
    {
        AttackIndex = track;
        AttackY = tracks[track].position.y;
    }

}
