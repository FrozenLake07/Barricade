using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed partial class StoryDirector
{
    AudioClip ChapterMusicClip(int number)
    {
        return number >= 1 && number <= 8 && number <= table.chapters.Length ? table.chapters[number - 1].openingMusic : null;
    }

    void PlayChapterMusic(int number)
    {
        var clip = ChapterMusicClip(number);
        if (clip == null)
        {
            StopCampaignMusic();
            audioSource.clip = null;
            if (number >= 1 && number <= 8)
                Debug.LogWarning("[章节配乐] 第" + number + "章未配置openingMusic，保持静音。");
            return;
        }

        // Chapters one and two share a continuous song; later chapter openings get a fresh cue.
        StartMusic(clip, number != 2 || !musicPlaybackActive);
    }

    bool MusicSuspended
    {
        get
        {
#if UNITY_EDITOR
            if(UnityEditor.EditorApplication.isPaused)return true;
#endif
            return CampaignSession.Suspended || DebugConsole.CapturingInput || GameSettingsView.IsOpen;
        }
    }

    string MusicTrackId() => audioSource.clip == null ? "none" : audioSource.clip == mourningMusic ? "mourning" : audioSource.clip == finaleMusic ? "finale" : audioSource.clip == chapterMusic ? "people" : "chapter-" + RunState.Chapter;
    AudioClip ResolveMusicTrack(string id)
    {
        if (id == "none")
            return null;
        if (id == "mourning")
            return mourningMusic;
        if (id == "finale")
            return finaleMusic;
        if (id != null && id.StartsWith("chapter-") && int.TryParse(id.Substring(8), out int chapter))
            return ChapterMusicClip(chapter);
        if (id == "people")
            return ChapterMusicClip(RunState.Chapter);
        return null;
    }
}
