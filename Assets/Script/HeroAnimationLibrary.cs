using System;
using UnityEngine;

[CreateAssetMenu(menuName = "街垒/主角动画资源")]
public sealed class HeroAnimationLibrary : ScriptableObject
{
    public const string ResourcePath = "HeroAnimations/HeroAnimationLibrary";
    public int formatVersion;
    public RuntimeAnimatorController controller;
    public Entry[] actions;
    public Vector3 groundMuzzle, upMuzzle;
    [Min(.01f)] public float walkingCycleDistance = .875f;
    [Min(.01f)] public float carryingCycleDistance = .75f;

    [Serializable]
    public sealed class Entry
    {
        public HeroMotion motion;
        public AnimationClip clip;
        public Sprite firstFrame;
        public float fps, duration;
        public bool loop;
    }

    public Entry Find(HeroMotion motion)
    {
        if (actions != null)
            foreach (Entry entry in actions)
                if (entry != null && entry.motion == motion) return entry;
        return null;
    }

    public float Duration(HeroMotion motion)
    {
        Entry entry = Find(motion);
        return entry != null ? Mathf.Max(.001f, entry.duration) : 1f;
    }
}
