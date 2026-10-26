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
    Coroutine lyricFlow;
    void OpenLyricsChapter(int chapter, bool hard = false)
    {
        if (lyricFlow != null)
            StopCoroutine(lyricFlow);
        lyricFlow = StartCoroutine(story.DebugChapter(chapter, hard));
    }

    IEnumerator LyricsAt(float seconds, string speakerName)
    {
        var music = Get<AudioSource>(story, "audioSource");
        var lyrics = story.GetComponent<CampaignMusicLyricsView>();
        music.time = seconds;
        yield return Frames(3);
        var cue = MusicLyricsCatalog.Load().Find(music.clip.name).At(music.time);
        Check(lyrics.Visible && cue != null && lyrics.CurrentEnglish == cue.en && lyrics.CurrentChinese == cue.zh, "Bilingual lyrics follow actual music cursor: " + music.clip.name + " @ " + seconds);
        Check(lyrics.CurrentSpeaker == speakerName, "English speaker: " + speakerName);
    }

    IEnumerator LyricsTest()
    {
        RememberPreferences();
        yield return Frames(4);
        DebugConsole.Current?.SetOpen(false);
        report.coverage = "Actual chapter openings, actual AudioSource seeking and pause, bilingual overlay, English speakers, narrative priority, combat/instrumental/credits/title suppression. No complete musical listening or manual timing review.";
        var music = Get<AudioSource>(story, "audioSource");
        var film = story.GetComponent<CampaignCinematicView>();
        var lyrics = story.GetComponent<CampaignMusicLyricsView>();
        OpenLyricsChapter(1);
        yield return Until(() => story.Phase == CampaignPhase.Cinematic);
        yield return LyricsAt(17f, "Ensemble");
        CaptureUi(1280, 720, "第一章合唱字幕");
        music.time = 2;
        yield return Frames(2);
        Check(!lyrics.Visible, "Instrumental lead-in does not invent lyric lines");
        OpenLyricsChapter(3);
        yield return Until(() => RunState.Chapter == 3 && film.Active && !film.IsBlackCardVisible);
        yield return LyricsAt(.5f, "Javert");
        CaptureUi(1280, 720, "第三章双语字幕");
        var lyricPage = Get<GameObject>(lyrics, "page");
        Check(lyricPage.GetComponentsInChildren<UnityEngine.UI.Text>().All(t => t.font == GameFont.Silver && !t.raycastTarget), "All lyric text uses the game font and does not intercept input");
        OpenLyricsChapter(4);
        yield return Until(() => RunState.Chapter == 4 && film.Active && !film.IsBlackCardVisible);
        yield return LyricsAt(.5f, "Gavroche");
        CaptureUi(1280, 720, "第四章Gavroche字幕");
        yield return LyricsAt(20f, "Gavroche");
        CaptureUi(1280, 720, "第四章硌字补齐");
        RunState.Chapter = 5;
        yield return Frames(2);
        Check(lyrics.Visible && lyrics.CurrentSpeaker == "", "Gavroche speaker label is suppressed after chapter four");
        RunState.Chapter = 4;
        OpenLyricsChapter(5);
        yield return Until(() => RunState.Chapter == 5 && film.Active && !film.IsBlackCardVisible);
        yield return LyricsAt(5f, "Javert");
        yield return LyricsAt(11.5f, "Javert");
        CaptureUi(1280, 720, "第五章绰字补齐");
        OpenLyricsChapter(6);
        yield return Until(() => RunState.Chapter == 6 && film.Active && !film.IsBlackCardVisible);
        yield return LyricsAt(.5f, "Enjolras");
        yield return LyricsAt(10f, "Ensemble");
        yield return LyricsAt(21f, "Ensemble");
        CaptureUi(1280, 720, "第六章谊字补齐");
        OpenLyricsChapter(7);
        yield return Until(() => RunState.Chapter == 7 && film.Active && !film.IsBlackCardVisible);
        yield return LyricsAt(1f, "Enjolras");
        yield return LyricsAt(10f, "Javert");
        CaptureUi(1280, 720, "第七章Javert回应字幕");
        var narrative = Get<UnityEngine.UI.Text>(film, "subtitle");
        narrative.text = "剧情字幕测试";
        yield return Frames(2);
        Check(!lyrics.Visible, "Existing narrative subtitles take priority over lyrics");
        narrative.text = "";
        yield return Frames(2);
        Check(lyrics.Visible, "Lyrics return after narrative text clears");
        DebugConsole.Current.SetOpen(true);
        yield return Frames(3);
        float at = music.time;
        string pausedLine = lyrics.CurrentEnglish;
        yield return Frames(5);
        Check(Math.Abs(music.time - at) < .03 && lyrics.CurrentEnglish == pausedLine, "Pause freezes the audio cursor and lyric sentence together");
        DebugConsole.Current.SetOpen(false);
        yield return Frames(3);
        OpenLyricsChapter(3, true);
        yield return Until(() => story.Phase == CampaignPhase.Defense);
        music.time = .5f;
        yield return Frames(3);
        Check(!lyrics.Visible, "Lyrics are hidden during actual combat");
        OpenLyricsChapter(8);
        yield return Until(() => RunState.Chapter == 8 && film.Active && !film.IsBlackCardVisible);
        yield return Frames(3);
        Check(!lyrics.Visible, "Instrumental chapter eight has no lyric overlay");
        RunState.Ending = CampaignEnding.Hope;
        OpenLyricsChapter(10);
        yield return Until(() => RunState.Chapter == 10 && film.Active && !film.IsBlackCardVisible);
        yield return LyricsAt(12f, "Marius");
        var hopePlayback = Get<CinematicPlaybackState>(film, "playback");
        hopePlayback.Advance(Get<double[]>(hopePlayback, "durations").Take(3).Sum() + .1 - hopePlayback.Age);
        yield return LyricsAt(66f, "Ensemble");
        CaptureUi(1280, 720, "希望结局合唱字幕");
        yield return Until(() => Get<CinematicPlaybackState>(film, "playback") != null);
        Get<CinematicPlaybackState>(film, "playback").Advance(1000);
        music.time = music.clip.length - story.cinematics.creditsSeconds + .1f;
        yield return Until(() => film.IsPictureCredits);
        yield return Frames(3);
        Check(!lyrics.Visible, "Credits stay readable without simultaneous lyrics");
        typeof(StoryDirector).GetMethod("StopCampaignMusic", Private).Invoke(story, null);
        film.DebugSkip();
        yield return Until(() => story.Phase == CampaignPhase.Title);
        yield return Frames(3);
        Check(!lyrics.Visible && FindObjectsByType<CampaignMusicLyricsView>(FindObjectsSortMode.None).Length == 1, "Returning to title hides lyrics and keeps exactly one overlay controller");
        RunState.Ending = CampaignEnding.Mourning;
        OpenLyricsChapter(10);
        yield return Until(() => film.Active && !film.IsBlackCardVisible);
        yield return LyricsAt(12f, "Marius");
        CaptureUi(1280, 720, "哀悼结局Marius字幕");
    }
}
#endif
