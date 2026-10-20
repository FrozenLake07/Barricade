using System.Collections.Generic;
using UnityEngine;

/// 全局音效播放器：运行时自建声源池，不需要在场景里挂任何东西。
/// 音频放在 Assets/Resources/SFX/ 下，文件名用文档编号，例如 SFX-PLY-01.wav；
/// 变体用 SFX-PLY-01-V1.wav / -V2 / -V3。缺文件只提示一次，不会中断游戏。
public static class SfxPlayer
{
    public const string Folder = "SFX/";
    public static float MasterVolume = 1f;
    const int VoiceCount = 12;

    static AudioSource[] voices;
    static int cursor;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static readonly HashSet<string> reported = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        voices = null;
        cursor = 0;
        clips.Clear();
        reported.Clear();
    }

    public static void Play(string id, float volume = 1f, float pitch = 1f)
    {
        if (StoryDirector.Active && RunState.Chapter == 9) return;
        if (string.IsNullOrEmpty(id)) return;
        AudioClip clip = Resolve(id);
        if (clip == null) return;
        AudioSource source = NextSource();
        if (source == null) return;
        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        source.volume = Mathf.Clamp01(volume * MasterVolume);
        source.clip = clip;
        source.Play();
    }

    /// 变体随机 + 轻微音高抖动，避免连发听起来像机关枪。
    /// 没有对应变体文件时回退到基础文件，只放一个也能响。
    public static void PlayVariants(string id, int variants, float volume = 1f, float pitchJitter = 0f)
    {
        if (StoryDirector.Active && RunState.Chapter == 9) return;
        float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        if (variants > 1)
        {
            string variant = id + "-V" + Random.Range(1, variants + 1);
            if (Resolve(variant, true) != null)
            {
                Play(variant, volume, pitch);
                return;
            }
        }
        Play(id, volume, pitch);
    }

    static AudioClip Resolve(string id, bool quiet = false)
    {
        AudioClip cached;
        if (clips.TryGetValue(id, out cached)) return cached;
        AudioClip clip = Resources.Load<AudioClip>(Folder + id);
        clips[id] = clip;
        if (clip == null && !quiet && reported.Add(id))
            Debug.LogWarning("[SfxPlayer] 缺少音频：Resources/" + Folder + id);
        return clip;
    }

    static AudioSource NextSource()
    {
        if (voices == null)
        {
            GameObject host = new GameObject("[SfxPlayer]");
            Object.DontDestroyOnLoad(host);
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = false;
                voices[i] = source;
            }
        }
        for (int i = 0; i < voices.Length; i++)
        {
            int index = (cursor + i) % voices.Length;
            if (!voices[index].isPlaying)
            {
                cursor = (index + 1) % voices.Length;
                return voices[index];
            }
        }
        AudioSource fallback = voices[cursor];
        cursor = (cursor + 1) % voices.Length;
        return fallback;
    }
}
