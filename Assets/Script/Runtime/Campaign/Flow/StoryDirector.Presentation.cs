using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public sealed partial class StoryDirector
{
    IEnumerator OpeningCard(string key, string text, float seconds, int fontSize, bool allowSkip)
    {
        if (seconds <= 0)
            yield break;
        Phase = CampaignPhase.Transition;
        ui.Card("", "");
        ui.Night.color = Color.clear;
        TransitionRequested?.Invoke(RunState.Chapter, key);
        transitionStarted.Invoke();
        telemetry.Record(key == "chapter-announcement" ? "chapter_announcement" : "opening_card", key);
        yield return cinematicView.PlayBlackCard(text, seconds, allowSkip, fontSize, epigraph: key == "opening-subtitle");
        CampaignSession.BlockCurrentInput();
        transitionFinished.Invoke();
    }

    IEnumerator PlayFirstChapter()
    {
        Phase = CampaignPhase.Cinematic;
        ResetClock();
        ui.Card("", "");
        int[] durations = new int[table.firstChapterFrames.Length];
        for (int i = 0; i < durations.Length; i++)
            durations[i] = table.firstChapterFrames[i].beats;
        sequence.ShowFrame(0, table.firstChapterFrames[0], table.firstChapterCrossfadeSeconds);
        while (Clock.Completed < Chapter.cinematicBeats)
        {
            if (!CampaignSession.InputBlocked)
            {
                int index = CampaignRevisionRules.FrameAt(Clock.Completed, durations);
                if (index != sequence.FrameIndex)
                    sequence.ShowFrame(index, table.firstChapterFrames[index], table.firstChapterCrossfadeSeconds);
                sequence.Advance(Time.deltaTime);
            }

            yield return null;
        }

        sequence.Hide();
        telemetry.EndChapter("completed");
        yield return BeginChapter(2, false);
    }

    IEnumerator LoadScene(string name)
    {
        SfxPlayer.StopAll();
        Phase = CampaignPhase.Transition;
        if (!Application.CanStreamedLevelBeLoaded(name))
            throw new InvalidOperationException("场景未加入Build Settings：" + name);
        yield return SceneManager.LoadSceneAsync(name);
        yield return null;
    // 场景Start完成后再开放输入，避免旧计时/HP初始化覆盖章节状态。
    }

    // 动画可用Signal调用此方法完成；空接口使用定时文字卡，暂停Timeline不会误切场。
    public void CompleteTransition() => signalComplete = true;
    public void PlayTransitionCue(AudioClip clip)
    {
        if (clip != null)
            transitionAudio.PlayOneShot(clip);
    }

    IEnumerator Transition(ChapterTransition slot, string key, string text, bool useMusic, bool transparent = false)
    {
        Phase = CampaignPhase.Transition;
        signalComplete = false;
        if (!string.IsNullOrEmpty(slot.caption))
            text = slot.caption;
        TransitionRequested?.Invoke(RunState.Chapter, key);
        transitionStarted.Invoke();
        // Explicit prefab/Timeline overrides remain authoritative; otherwise use the configured image route.
        var route = cinematics != null ? cinematics.Find(RunState.Chapter, key, RunState.Decision, RunState.Ending) : null;
        if (route != null && slot.cinematicPrefab == null && slot.timeline == null)
        {
            ui.Card("", "");
            ui.Black.color = Color.clear;
            ui.Night.color = Color.clear;
            if (RunState.Chapter == 9)
                StopCampaignMusic();
            if (slot.audio != null)
                transitionAudio.PlayOneShot(slot.audio);
            yield return cinematicView.Play(cinematics, route, slot.allowSkip, plannedDurations: OpeningPictureDurations(route, key));
            CampaignSession.BlockCurrentInput();
            transitionAudio.Stop();
            transitionFinished.Invoke();
            yield break;
        }

        if (!transparent)
            ui.Card(text, slot.allowSkip ? "空格：跳过" : "");
        if (RunState.Chapter == 9)
            StopCampaignMusic();
        if (slot.audio != null)
            transitionAudio.PlayOneShot(slot.audio);
        GameObject visual = slot.cinematicPrefab != null ? Instantiate(slot.cinematicPrefab) : null;
        if (visual != null || slot.timeline != null)
        {
            ui.Black.color = Color.clear;
            ui.Main.text = "";
        }

        PlayableDirector supplied = visual != null ? visual.GetComponentInChildren<PlayableDirector>() : null;
        PlayableDirector director = supplied != null ? supplied : cutscene;
        if (slot.timeline != null)
            director.playableAsset = slot.timeline;
        bool timeline = director != null && director.playableAsset != null;
        Action<PlayableDirector> stopped = d => signalComplete = true;
        musicFader.SetAnimationActive(timeline || visual != null);
        try
        {
            if (timeline)
            {
                director.stopped += stopped;
                director.extrapolationMode = DirectorWrapMode.None;
                director.time = 0;
                director.Play();
            }

            float elapsed = 0;
            while (!signalComplete || CampaignSession.Suspended)
            {
                if (CampaignSession.Suspended || DebugConsole.SimulationPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.unscaledDeltaTime;
                if (!timeline && !slot.waitForSignal && elapsed >= Mathf.Max(.05f, slot.placeholderSeconds))
                    break;
                if (!DebugConsole.CapturingInput && !TavernNameDialogue.InputCaptured && !CampaignSession.InputBlocked && slot.allowSkip && elapsed > .25f && Input.GetKeyDown(KeyCode.Space))
                {
                    telemetry.Record("transition_skip", key);
                    break;
                }

                if (slot.waitForSignal && !timeline && elapsed >= Mathf.Max(1, slot.signalTimeoutSeconds))
                {
                    Debug.LogWarning("[StoryDirector] 过场完成信号超时：" + key);
                    break;
                }

                yield return null;
            }
        }
        finally
        {
            musicFader.SetAnimationActive(false);
            if (timeline)
            {
                director.stopped -= stopped;
                director.Stop();
            }

            if (visual != null)
                Destroy(visual);
            cutscene.playableAsset = null;
            transitionAudio.Stop();
        }

        CompleteHopeRoute(route);
        transitionFinished.Invoke();
    }

    // Timeline narrative captions are allowed during transitions only. Chapter prose is removed.
    public void ShowCaption(string text)
    {
        if (Phase == CampaignPhase.Transition)
            ShowGameplayHint(text);
    }

    public void ShowGameplayHint(string text)
    {
        ui.Caption.text = text;
        captionUntil = Time.time + 6f;
    }

    void UpdateHUD()
    {
        if (!GameplayRunning)
            return;
        // ChapterHUD is the data source; CampaignGameplayHud renders its separate rows.
        ui.Hud.enabled = false;
        ui.Caption.enabled = !CampaignDebug.Value.hideHud;
        ui.Hud.color = ParisUI.Style.cream;
        string actions = Phase == CampaignPhase.Preparation ? (Chapter.number == 2 ? "建造：按画面字母；双键间隔不超过" + Mathf.RoundToInt(table.chordMaxGap * 1000) + "ms" : "修补：按画面字母；双键间隔不超过" + Mathf.RoundToInt(table.chordMaxGap * 1000) + "ms") : GameBindings.CombatHint;
        ui.Hud.text = CampaignHudPresentation.Heading(RunState.Chapter, Chapter.title) + "\n" + actions + (Phase == CampaignPhase.Defense && WaveDirector.Current != null && WaveDirector.Current.FinaleStarted ? "\n击退剩余敌人，守住街垒。" : "");
    }
}
