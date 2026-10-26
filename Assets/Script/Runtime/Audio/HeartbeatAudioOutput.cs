using System;
using System.Collections.Generic;
using UnityEngine;

// 一个HeartSet上的专用声源队列，不增加GameObject、不占SfxPlayer池。
public sealed class HeartbeatAudioOutput : IDisposable
{
    struct Key : IEquatable<Key>
    {
        public long Generation, Cycle;
        public int Hit;
        public bool Music;
        public bool Equals(Key k) => Generation == k.Generation && Cycle == k.Cycle && Hit == k.Hit && Music == k.Music;
        public override bool Equals(object o) => o is Key && Equals((Key)o);
        public override int GetHashCode() => Generation.GetHashCode() ^ Cycle.GetHashCode() * 397 ^ Hit * 17 ^ (Music ? 1 : 0);
    }
    sealed class Voice
    {
        public AudioSource Source;
        public Key Key;
        public double Start, End;
        public float Gain;
    }
    readonly GameObject host;
    readonly AudioClip[] clips = new AudioClip[3];
    readonly double[] attacks = new double[3];
    readonly List<Voice> voices = new List<Voice>();
    readonly Dictionary<Key, double> submitted = new Dictionary<Key, double>();
    readonly List<Key> expired = new List<Key>();
    readonly Queue<string> trace = new Queue<string>();
    double now, delay, lead;
    float volume;
    public int ScheduledCount { get; private set; }
    public int LateSkipped { get; private set; }
    public double MinimumLead => lead;
    public string[] Trace => trace.ToArray();

    public HeartbeatAudioOutput(GameObject host)
    {
        this.host = host;
        for (int i = 0; i < 3; i++)
        {
            clips[i] = Resources.Load<AudioClip>("SFX/HeartbeatPulse/Pulse-V" + (i + 1));
            if (clips[i] == null) { Debug.LogWarning("[Heartbeat] 缺少单跳音频Pulse-V" + (i + 1)); continue; }
            clips[i].LoadAudioData();
            float[] samples = new float[clips[i].samples * clips[i].channels];
            if (!clips[i].GetData(samples, 0)) continue;
            int peak = 0;
            for (int s = 1; s < samples.Length; s++) if (Math.Abs(samples[s]) > Math.Abs(samples[peak])) peak = s;
            attacks[i] = (double)(peak / clips[i].channels) / clips[i].frequency;
        }
    }

    public void BeginFrame(double dsp, double outputDelay, float masterVolume)
    {
        now = dsp; delay = outputDelay; volume = CampaignDebug.Value.muteHeartbeat?0:Mathf.Clamp01(masterVolume)*GamePresentationOptions.HeartbeatVolume;
        AudioSettings.GetDSPBufferSize(out int size, out _);
        lead = Math.Max(.02d, (double)size / Math.Max(1, AudioSettings.outputSampleRate) * 2d);
        foreach (var voice in voices) if (voice.Source != null) voice.Source.volume = volume * voice.Gain;
        expired.Clear();
        foreach (var entry in submitted) if (entry.Value < now - 2d) expired.Add(entry.Key);
        foreach (var key in expired) submitted.Remove(key);
    }

    public void Submit(long generation, long cycle, int hit, bool music,
        double audiblePeak, double audibleEnd, float gain)
    {
        var key = new Key { Generation = generation, Cycle = cycle, Hit = hit, Music = music };
        if (submitted.ContainsKey(key)) return;
        int index = (int)((cycle % 3 + hit + 3) % 3);
        AudioClip clip = clips[index];
        if (clip == null) return;
        if (!HeartbeatAudioTimeline.TryCue(now, lead, audiblePeak, audibleEnd, delay,
            attacks[index], clip.length, out var cue))
        {
            // 真正迟到的才标记，远在未来的事件保留给后续帧。
            if (audiblePeak - delay - attacks[index] < now + lead)
            { submitted[key] = now; LateSkipped++; }
            return;
        }
        Voice voice = voices.Find(v => v.End <= now);
        if (voice == null)
        {
            if (voices.Count >= 12) return;
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f;
            source.dopplerLevel = 0f; source.priority = 40; source.bypassReverbZones = true;
            voice = new Voice { Source = source }; voices.Add(voice);
        }
        voice.Source.Stop(); voice.Source.clip = clip; voice.Source.pitch = 1f;
        voice.Key = key; voice.Start = cue.Start; voice.End = cue.End;
        voice.Gain = Mathf.Clamp01(gain) * (hit == 2 ? .55f : 1f);
        voice.Source.volume = volume * voice.Gain;
        voice.Source.PlayScheduled(cue.Start);
        voice.Source.SetScheduledEndTime(cue.End);
        submitted[key] = cue.End; ScheduledCount++;
        if (trace.Count >= 100) trace.Dequeue();
        trace.Enqueue(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0} g={1} cycle={2} hit={3} dsp={4:F4}..{5:F4} heardPeak={6:F4} heardEnd={7:F4} beatEnd={8:F4}",
            music ? "MUSIC" : "FREE", generation, cycle, hit, cue.Start, cue.End,
            cue.AudiblePeak, cue.AudibleEnd, audibleEnd));
    }

    public void CancelFuture()
    {
        foreach (var v in voices)
        {
            if (v.Start <= now || v.End <= now) continue;
            v.Source.Stop(); v.End = 0d; submitted.Remove(v.Key);
        }
    }

    public void Stop()
    {
        foreach (var v in voices) { if (v.Source != null) v.Source.Stop(); v.End = 0d; }
        submitted.Clear();
    }
    public void Dispose()
    {
        Stop();
        foreach (var v in voices) if (v.Source != null) UnityEngine.Object.Destroy(v.Source);
        voices.Clear();
    }
}
