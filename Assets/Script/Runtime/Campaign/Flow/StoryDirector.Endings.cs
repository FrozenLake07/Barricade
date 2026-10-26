using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed partial class StoryDirector
{
    CampaignEndingView endingView;
    public double EndingMusicRemaining => audioSource.clip != null && musicPlaybackActive ? Mathf.Max(0, audioSource.clip.length - audioSource.time) : 0;

    bool WaitingForEndingMusic => musicPlaybackActive && audioSource.clip != null;

    CinematicRoute EndingRoute()
    {
        var shots = new List<CinematicShot>();
        foreach (string key in new[]
        {
            "chapter-entry",
            "ending"
        }

        )
        {
            var route = cinematics?.Find(10, key, RunState.Decision, RunState.Ending);
            if (route == null)
                continue;
            foreach (var shot in route.shots)
            {
                var last = shots.Count > 0 ? shots[shots.Count - 1] : null;
                if (last != null && last.image == shot.image && last.subtitle == shot.subtitle && last.voiceOver == shot.voiceOver && !shot.triggerHopeFinale)
                    continue;
                shots.Add(shot);
            }
        }

        return shots.Count == 0 ? null : new CinematicRoute
        {
            chapter = 10,
            key = "ending",
            ending = RunState.Ending,
            shots = shots.ToArray()
        };
    }

    IEnumerator RunEnding()
    {
        flowBusy = true;
        ClearWorld();
        Phase = CampaignPhase.Transition;
        ui.Gameplay(0);
        ui.Hud.text = ui.Caption.text = "";
        ui.Black.color = Color.clear;
        var route = EndingRoute();
        if (route != null)
        {
            var durations = new double[route.shots.Length];
            int cue = -1;
            for (int i = 0; i < durations.Length; i++)
            {
                durations[i] = route.shots[i].Duration;
                if (cue < 0 && route.shots[i].triggerHopeFinale)
                    cue = i;
            }

            if (RunState.Ending == CampaignEnding.Hope)
                durations = EndingSequenceTiming.HopeDurations(durations, cue, hopeFinaleCueSeconds, finaleMusic != null ? finaleMusic.length : 0, cinematics.creditsSeconds, audioSource.clip == finaleMusic ? audioSource.time : 0);
            yield return cinematicView.Play(cinematics, route, false, true, durations, RunState.Ending == CampaignEnding.Mourning);
        }

        if (RunState.Ending == CampaignEnding.Mourning)
        {
            // A new press on the final picture can end the song early and reveal the menu.
            bool skip = cinematicView.LastShotSkipRequested;
            while (!skip && (WaitingForEndingMusic || CampaignSession.InputBlocked || DebugConsole.SimulationPaused))
            {
                if (Input.anyKeyDown && cinematicView.RequestFinalPictureSkip())
                    skip = true;
                else
                    yield return null;
            }

            if (skip)
            {
                StopCampaignMusic();
                telemetry.Record("ending_last_picture_skip", "mourning");
            }

            var sprite = cinematicView.CurrentImage != null ? cinematicView.CurrentImage.sprite : null;
            endingView = CampaignEndingView.Attach(ui.View);
            endingView.Show(sprite, RestartEndingChapter, () => StartCoroutine(ReturnToTitle()), () => session.Quit());
            cinematicView.Hide();
            yield return endingView.Reveal();
            telemetry.EndChapter("completed");
            telemetry.FinishRun("completed");
            Phase = CampaignPhase.Finished;
            flowBusy = false;
        }
        else
        {
            float credits = cinematics != null ? cinematics.creditsSeconds : 18;
            while (WaitingForEndingMusic && EndingMusicRemaining > credits)
                yield return null;
            yield return cinematicView.PlayBlackCard(cinematics != null ? cinematics.creditsText : "制作人员\n\n感谢游玩", Mathf.Max(credits, (float)EndingMusicRemaining), false, 38, true, false, true);
            while (WaitingForEndingMusic || CampaignSession.InputBlocked || DebugConsole.SimulationPaused)
                yield return null;
            telemetry.EndChapter("completed");
            telemetry.FinishRun("completed");
            yield return ReturnToTitle();
        }
    }

    void RestartEndingChapter()
    {
        if (flowBusy || Phase != CampaignPhase.Finished)
            return;
        int chapter = Mathf.Clamp(RunState.EndingSourceChapter, 2, 9);
        RunState.Ending = CampaignEnding.None;
        RunState.HopeMusicStarted = false;
        RunState.EndingSourceChapter = 0;
        telemetry.StartRun("ending_retry");
        endingView.Hide();
        StartCoroutine(BeginChapter(chapter, true));
    }

    void SelectEnding(CampaignEnding ending, int sourceChapter)
    {
        RunState.Ending = ending;
        RunState.EndingSourceChapter = sourceChapter;
        RunState.HopeMusicStarted = false;
        telemetry.Record("ending_selected", ending + ";source=" + sourceChapter);
    }

    void BeginEndingMusic()
    {
        if (RunState.Ending == CampaignEnding.Mourning)
            PlayMusic(mourningMusic);
        else
        {
            RunState.HopeMusicStarted = false;
            PlayHopeFinale();
        }
    }

    // The full hope score starts on entry; later picture/Timeline cues must not restart it.
    public void PlayHopeFinale()
    {
        if (RunState.Chapter != 10 || RunState.Ending != CampaignEnding.Hope || RunState.HopeMusicStarted)
            return;
        RunState.HopeMusicStarted = true;
        StartMusic(finaleMusic, true);
        telemetry.Record("ending_hope_music", "Finale");
    }

    public void CompleteHopeRoute(CinematicRoute route)
    {
        if (route == null)
            return;
        foreach (var shot in route.shots)
            if (shot.triggerHopeFinale)
            {
                PlayHopeFinale();
                break;
            }
    }
}
