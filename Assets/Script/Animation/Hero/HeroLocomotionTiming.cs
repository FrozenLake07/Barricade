using System;

// Fit one complete visual stride to its measured travel distance.
public static class HeroLocomotionTiming
{
    public static float PlaybackRate(float speed, float clipSeconds, float cycleDistance)
    {
        if (!Positive(speed) || !Positive(clipSeconds) || !Positive(cycleDistance)) return 1f;
        return Math.Max(.15f, Math.Min(3f, speed * clipSeconds / cycleDistance));
    }

    static bool Positive(float value) => value > .0001f && !float.IsNaN(value) && !float.IsInfinity(value);
}
