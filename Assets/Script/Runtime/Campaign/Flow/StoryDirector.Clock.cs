using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public sealed partial class StoryDirector
{
    void BoundaryBeat()
    {
        RunState.TotalHeartbeats++;
        if (!CampaignDebug.Value.muteHeartbeat)
            SfxPlayer.Play("HeartbeatPulse/Pulse-V1", .55f);
    }

    void ResetClock()
    {
        Clock = new CampaignBeatClock();
        Clock.Reset(CampaignDebug.Value.tempo > 0 ? CampaignDebug.Value.tempo : BaseWait, CampaignDebug.Value.window > 0 ? CampaignDebug.Value.window : table.perfectWindow);
        Clock.WindowOpened += OnWindow;
        Clock.CycleCompleted += OnCycleCompleted;
        windowJudged = false;
        constructionCycle = -1;
        judgementOffset = RunState.Judgements.Count;
        SyncTimers();
    }

    void OnWindow(int beat)
    {
        RunState.TotalHeartbeats++;
        if (GameplayRunning)
            EnsureJudgementSlot();
        if (CombatRunning)
            waves?.Beat(beat);
        if (Phase == CampaignPhase.Epilogue)
        {
            Clock.NextWait = CampaignDebug.Value.tempo > 0 ? CampaignDebug.Value.tempo : Mathf.Lerp(.5f, 1.2f, beat / (float)Mathf.Max(1, Chapter.epilogueBeats));
        }
    }

    void SyncTimers()
    {
        Tap.Setbeattime = TapSec.Setbeattime = Clock.Wait;
        Tap.oribeattime = TapSec.oribeattime = BaseWait;
        Tap.beattime = TapSec.beattime = Clock.BeatRemaining;
        Tap.perfecttime = TapSec.perfecttime = Clock.PerfectRemaining;
    }

    void EnsureJudgementSlot()
    {
        while (RunState.Judgements.Count <= judgementOffset + Clock.Completed)
            RunState.Judgements.Add(0);
    }

    public void RecordInput(bool perfect, bool missed = false)
    {
        if (windowJudged)
            return; // 每周期一次，后续误按不能覆盖已记录的Perfect。
        windowJudged = true;
        EnsureJudgementSlot();
        if (!RunState.TryRecordAt(judgementOffset + Clock.Completed, perfect))
            return;
        telemetry.Judge(perfect, missed);
        Clock.NextWait = CampaignDebug.Value.tempo > 0 ? CampaignDebug.Value.tempo : perfect && Chapter.accelerate ? Mathf.Max(.5f, Clock.NextWait - .05f) : BaseWait;
    }

    void OnCycleCompleted(int beat)
    {
        if (Phase == CampaignPhase.Preparation && !windowJudged && constructionCycle == Clock.Completed)
        {
            var result = construction.Close();
            if (result == ConstructionResult.Wrong)
            {
                telemetry.Record("rest");
                restRemaining = 3;
                SfxPlayer.Play("SFX-QTE-03");
            }
        }

        bool scoring = GameplayRunning;
        if (scoring && !windowJudged)
            RecordInput(false, true);
        telemetry.Beat(Phase, beat);
        windowJudged = false;
    }
}
