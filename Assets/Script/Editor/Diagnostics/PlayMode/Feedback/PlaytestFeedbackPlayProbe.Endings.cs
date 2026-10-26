#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator HopeTest()
    {
        RememberPreferences();
        yield return Frames(4);
        DebugConsole.Current?.SetOpen(false);
        var music = Get<AudioSource>(story, "audioSource");
        var film = story.GetComponent<CampaignCinematicView>();
        // Exercise the normal chapter-nine ending path, then the console chapter-ten entry path.
        StartCoroutine(story.DebugChapter(9, true));
        yield return Until(() => story.Phase == CampaignPhase.Defense && WaveDirector.Current != null);
        story.DebugFail();
        yield return Frames(2);
        Check(RunState.Ending == CampaignEnding.Hope && RunState.HopeMusicStarted && music.clip == story.finaleMusic && music.isPlaying, "Hope score starts immediately from the chapter-nine ending during its title card");
        yield return Until(() => film.Active && Get<CinematicPlaybackState>(film, "playback") != null);
        yield return Frames(2);
        var playback = Get<CinematicPlaybackState>(film, "playback");
        var durations = Get<double[]>(playback, "durations");
        float time = music.time;
        Check(film.CurrentImage.sprite.name == "CH10_A" && time > 0 && time < story.hopeFinaleCueSeconds, "Opening mourning picture uses the first section of the new continuous score");
        Check(Math.Abs(durations.Take(3).Sum() + time - playback.Age - story.hopeFinaleCueSeconds) < .15, "First three pictures reach the song transition including elapsed chapter card music");
        Check(Math.Abs(durations.Skip(3).Sum() + story.cinematics.creditsSeconds + story.hopeFinaleCueSeconds - music.clip.length) < .01, "Hope pictures plus credits use the remaining score exactly");
        playback.Advance(durations.Take(3).Sum() - .35 - playback.Age);
        music.time = story.hopeFinaleCueSeconds - .35f;
        yield return Frames(2);
        Check(film.CurrentImage.sprite.name == "CH10_C", "Marius remains at the table just before the hope section");
        yield return Until(() => film.CurrentImage.sprite.name == "CH10_D");
        Check(Math.Abs(music.time - story.hopeFinaleCueSeconds) < .15, "Marius looks outside at the opening of the People Sing section");
        float cursor = music.time;
        story.PlayHopeFinale();
        yield return null;
        Check(music.time >= cursor - .01 && music.clip == story.finaleMusic, "Door cue never restarts the full score");
        var session = CampaignSession.Current;
        DebugConsole.Current.SetOpen(true);
        yield return Frames(2);
        float pausedAt = music.time;
        double pausedAge = playback.Age;
        yield return Frames(4);
        Check(Math.Abs(music.time - pausedAt) < .03 && Math.Abs(playback.Age - pausedAge) < .001, "Console pause keeps pictures and music synchronized");
        DebugConsole.Current.SetOpen(false);
        yield return Frames(3);
        playback.Advance(1000);
        music.time = music.clip.length - story.cinematics.creditsSeconds + .1f;
        yield return Until(() => film.IsBlackCardVisible);
        Check(film.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text.Contains("制作人员")), "Hope ending shows credits with the last section of music");
        yield return new WaitForSeconds(1);
        var veil = Get<GameObject>(film, "blackCardPage").GetComponent<UnityEngine.UI.Image>();
        var title = Get<UnityEngine.UI.Text>(film, "centerTitle");
        Check(film.CurrentImage.sprite.name == "CH10_G" && film.CurrentImage.enabled && film.CurrentImage.gameObject.activeInHierarchy, "Credits retain the final G picture behind their overlay");
        Check(film.CurrentImage.material.shader.name == "UI/CampaignEndingBlur" && Math.Abs(film.CurrentImage.material.GetFloat("_BlurPixels") - 8) < .001, "Only the final picture uses the fully revealed blur material");
        Check(veil.color.a > .5f && veil.color.a < .7f && title.color.a > .99f && title.font == GameFont.Silver, "Readable game-font credits stay sharp over a translucent dark veil");
        Check(title.material.shader != film.CurrentImage.material.shader, "Credits text is not rendered through the picture blur shader");
        File.WriteAllText(Path.Combine(output, "制作人员表原文.txt"), title.text);
        CaptureUi(1280, 720, "最后一张图制作人员表");
        typeof(StoryDirector).GetMethod("StopCampaignMusic", Private).Invoke(story, null);
        film.DebugSkip();
        yield return Until(() => story.Phase == CampaignPhase.Title);
        Check(!music.isPlaying, "Credits completion returns to the title with music stopped");
        Check(film.CurrentImage.material.shader.name != "UI/CampaignEndingBlur", "Credits blur is cleaned up when returning to the title");
        RunState.Ending = CampaignEnding.Hope;
        StartCoroutine(story.DebugChapter(10));
        yield return Frames(2);
        Check(music.clip == story.finaleMusic && music.time < 1 && RunState.HopeMusicStarted, "Console entry to chapter ten restarts the new score from the beginning");
        StartCoroutine(story.DebugPhase("title"));
        yield return Until(() => story.Phase == CampaignPhase.Title);
        RunState.Ending = CampaignEnding.Mourning;
        StartCoroutine(story.DebugChapter(10));
        yield return Frames(2);
        Check(music.clip == story.mourningMusic && !RunState.HopeMusicStarted, "Mourning ending retains its original music");
        story.PlayHopeFinale();
        Check(music.clip == story.mourningMusic, "Mourning ending rejects the hope music cue");
    }
}
#endif
