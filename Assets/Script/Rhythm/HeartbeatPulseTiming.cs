using System;

// 声音与动画共用的波形和相位；不读取输入、不修改Perfect或beattime。
public static class HeartbeatPulseTiming
{
    // perfecttime 在beattime期间也可能保留初值，不能用它单独判断静止期。
    public static bool IsPerfectGap(float beatTime, float perfectTime)
        => beatTime <= 0f && perfectTime > 0f;

    public static bool CanPulse(float beatTime, bool resting = false)
        => resting || beatTime > 0f;

    // 把最后一跳的音频截在beattime结束处，不把尾音带入Perfect。
    public static float ClipSeconds(float clipLength, float beatTime, bool resting = false)
        => CanPulse(beatTime, resting) ? Math.Max(0f, Math.Min(clipLength,
            resting ? clipLength : beatTime)) : 0f;

    public static float Pulse(float phase, bool music)
    {
        float first = music ? .28f : .12f;
        float second = music ? .67f : .30f;
        float width = music ? .12f : .07f;
        double a = (phase - first) / width;
        double b = (phase - second) / width;
        return (float)(Math.Exp(-a * a) + .55d * Math.Exp(-b * b));
    }
}

// 只报告当前帧真实经过的主跳/副跳；静止、换模式和卡帧均不追补旧拍。
public sealed class HeartbeatPulseEvents
{
    bool valid, previousMusic;
    float previous;
    long previousCycle;

    public void Reset() => valid = false;

    public int Sample(float phase, long cycle, bool music, bool active)
    {
        if (!active || float.IsNaN(phase) || float.IsInfinity(phase)) { Reset(); return 0; }
        float first = music ? .28f : .12f;
        float second = music ? .67f : .30f;
        float width = music ? .12f : .07f;
        if (!valid || previousMusic != music || previousCycle != cycle
            || phase < previous || phase - previous > .5f)
        {
            valid = true; previousMusic = music; previousCycle = cycle; previous = phase;
            if (phase >= second && phase - second <= width * 1.8f) return 2;
            if (phase >= first && phase - first <= width * 1.8f) return 1;
            return 0;
        }
        int pulse = previous < first && phase >= first && phase - first <= width * 1.8f ? 1 : 0;
        if (previous < second && phase >= second && phase - second <= width * 1.8f) pulse = 2;
        previous = phase;
        return HeartbeatPulseTiming.Pulse(phase, music) >= .02f ? pulse : 0;
    }
}
