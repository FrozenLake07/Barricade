using System;

// 只计算“预计听到”的时间与DSP提交时间；不接触Unity和玩家输入。
public static class HeartbeatAudioTimeline
{
    public const int Version = 3;
    public static double EstimateDelay(int bufferLength, int buffers, int sampleRate, double extraMs)
        => Math.Max(0d, Math.Min(.6d, (sampleRate > 0
            ? (double)Math.Max(0, bufferLength) * Math.Max(0, buffers - 1) / sampleRate : 0d)
            + extraMs / 1000d));

    public struct Cue
    {
        public double Start, End, AudiblePeak, AudibleEnd;
    }

    public static bool TryCue(double now, double minimumLead, double audiblePeak,
        double audibleWindowEnd, double delay, double attack, double clipLength, out Cue cue)
    {
        cue = default;
        double start = audiblePeak - delay - attack;
        // 保留5ms静音边界。迟到的拍不补播，避免把原本属于beattime的声音拖进Perfect。
        double end = Math.Min(start + clipLength, audibleWindowEnd - delay - .005d);
        if (double.IsNaN(start) || double.IsInfinity(start) || start < now + minimumLead
            || end <= start + attack + .002d || clipLength <= 0d) return false;
        cue = new Cue { Start = start, End = end, AudiblePeak = start + attack + delay,
            AudibleEnd = end + delay };
        return true;
    }

    public struct FreeSpan
    {
        public double Start, End, Phase;
        public float Beat, SetBeat, Slow, Fast, TimeScale;
        public bool Rest, Blending;
        public float RestPeriod, BlendOrigin, BlendElapsed, BlendDuration;

        double Period(double seconds)
        {
            double scaled = seconds * TimeScale;
            double progress = Math.Max(0d, Math.Min(1d, 1d - (Beat - scaled) / Math.Max(.01f, SetBeat)));
            double target = Rest ? RestPeriod : Slow + (Fast - Slow) * progress;
            if (Blending)
            {
                double blend = Math.Max(0d, Math.Min(1d, (BlendElapsed + scaled) / Math.Max(.02f, BlendDuration)));
                target = BlendOrigin + (target - BlendOrigin) * blend;
            }
            return Math.Max(.02d, target);
        }

        // 2ms积分预测声音峰值。扫描未来，不等画面已经跳动才下发音频。
        public void Visit(double until, Action<double, long, int> visit)
        {
            if (TimeScale <= 0f || until <= Start) return;
            double end = Math.Min(End, until), phase = Phase;
            for (double t = Start; t < end;)
            {
                double dt = Math.Min(.002d, end - t);
                double next = phase + dt * TimeScale / Period(t - Start + dt * .5d);
                long cycle = (long)Math.Floor(phase);
                for (long c = cycle; c <= (long)Math.Floor(next); c++)
                {
                    for (int hit = 1; hit <= 2; hit++)
                    {
                        double target = c + (hit == 1 ? .12d : .30d);
                        if (target > phase && target <= next)
                            visit(t + dt * (target - phase) / (next - phase), c, hit);
                    }
                }
                phase = next; t += dt;
            }
        }
    }
}
