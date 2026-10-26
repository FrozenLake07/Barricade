using UnityEngine;

/// 全局节拍，但按"组"错开：同一组共用一套相位和骰子，不同组各算各的。
/// 组号由 Spawn 在生成时注入 —— 所以同一批怪必然同节奏，不同批互不同步。
public static class Beat
{
    public const float Cycle    = 3f;    // 一个周期总长（秒）
    public const float ActTime  = 1f;    // 动作占用时长
    public const float HoldTime = 0.5f;  // 动作结束后再停 0.5 秒

    // 每组用组号算出一个固定的相位偏移，所以各组的节奏天然错开
    static float Offset(int group) => Hash((uint)group) % 1000u / 1000f * Cycle;

    public static float Phase(int group) => (Time.timeSinceLevelLoad + Offset(group)) % Cycle;
    public static int   CycleIndex(int group) =>
        Mathf.FloorToInt((Time.timeSinceLevelLoad + Offset(group)) / Cycle);

    // 是否处在"停下来做动作"的时间窗内（动作时长 + 0.5 秒停顿）
    public static bool InActionWindow(int group) => Phase(group) < ActTime + HoldTime;

    // 每个"组 + 周期"只掷一次骰子：同组共享结果，不同组互不相干。
    // 注意这是"每个周期掷一次"的概率，不是每秒 —— Cycle = 3s 时，
    // 独立场景每周期50%，平均约6秒一次；战役采用可见入场后的固定拍数射击。
    public static bool SoldierFires(int group) => Roll(group, CycleIndex(group), 11, 50);
    public static bool LeaderBombs(int group)  => Roll(group, CycleIndex(group), 23, 20);
    public static bool LeaderFires(int group)  => Roll(group, CycleIndex(group), 37, 20);

    // 整数哈希（纯函数、不分配对象）：同样的输入必得同样的输出
    static uint Hash(uint x)
    {
        x ^= x >> 16; x *= 0x7feb352du;
        x ^= x >> 15; x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }

    static bool Roll(int group, int cycle, int salt, int percent)
    {
        uint h = Hash((uint)group  * 73856093u
                    ^ (uint)cycle  * 19349663u
                    ^ (uint)salt   * 83492791u);
        return h % 100u < (uint)percent;
    }
}
