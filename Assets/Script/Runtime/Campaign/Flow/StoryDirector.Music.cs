using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public sealed partial class StoryDirector
{
    internal bool HasActiveMusicSource(AudioSource source)=>source==audioSource&&musicPlaybackActive;
    void PlayMusic(AudioClip clip) => StartMusic(clip, false);
    void StartMusic(AudioClip clip, bool restart)
    {
        audioSource.loop = false;
        // Retain the clip after completion or chapter-nine silence, so chapter changes cannot restart it.
        if (audioSource.clip == clip && !restart)
            return;
        StopCampaignMusic();
        audioSource.clip = clip;
        if (clip != null)
        {
            audioSource.Play();
            musicPlaybackActive = true;
        }
    }

    void StopCampaignMusic()
    {
        audioSource.Stop();
        musicPlaybackActive = false;
    }
}
