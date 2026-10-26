using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public sealed partial class StoryDirector
{
    void StartChoice()
    {
        waves?.Complete();
        ClearWorld();
        Phase = CampaignPhase.Choice;
        phaseOffset = Chapter.preparationBeats + Chapter.defenseBeats;
        ResetClock();
        ui.Card("选择：放走或处决", "Q：放走 —— 下一章追兵减少\nE：处决 —— 重振防线，下一章心跳加快");
        UpdateChoiceDisplay();
    }

    void UpdateChoice()
    {
        UpdateChoiceDisplay();
        if (Clock.Completed >= Chapter.choiceBeats)
        {
            ResolveChoice(JavertDecision.Released, true);
            return;
        }

        HandleChoice();
    }

    void UpdateChoiceDisplay()
    {
        float remaining = Mathf.Max(0, (Chapter.choiceBeats - Clock.Completed) * Clock.Period - Clock.Elapsed);
        ui.Main.text = $"选择：放走或处决\n剩余 {remaining:F1} 秒（超时放走）";
    }

    void HandleChoice()
    {
        if (DebugConsole.CapturingInput)
            return;
        if (Input.GetKeyDown(KeyCode.Q))
            ResolveChoice(JavertDecision.Released, false);
        else if (Input.GetKeyDown(KeyCode.E))
            ResolveChoice(JavertDecision.Executed, false);
    }

    void ResolveChoice(JavertDecision decision, bool timedOut)
    {
        if (Phase != CampaignPhase.Choice || flowBusy || RunState.Decision != JavertDecision.Unchosen)
            return;
        RunState.Decision = decision;
        if (decision == JavertDecision.Executed)
            GameManager.AddWallHP(Mathf.CeilToInt(GameManager.WallMaxHP * .3f));
        telemetry.Record("choice", timedOut ? "timeout_released" : decision.ToString());
        CompleteChapter();
    }

    void BeginLastChance()
    {
        telemetry.Record("rescue");
        lastChancesUsed++;
        savedClock = Clock;
        Clock = new CampaignBeatClock();
        Clock.Reset(CampaignDebug.Value.tempo > 0 ? CampaignDebug.Value.tempo : BaseWait, CampaignDebug.Value.window > 0 ? CampaignDebug.Value.window : table.perfectWindow);
        Clock.WindowOpened += beat => RunState.TotalHeartbeats++;
        Phase = CampaignPhase.LastChance;
        ui.Card("最后一拍：听心跳", "在下一次静止间歇按空格，尝试再次站稳。剩余机会：" + (table.lastChancesPerChapter - lastChancesUsed) + "。");
    }

    void FailChapter()
    {
        if (flowBusy || Chapter.number >= 10)
            return;
        flowBusy = true;
        SelectEnding(Chapter.number == 9 ? CampaignEnding.Hope : CampaignEnding.Mourning, Chapter.number);
        telemetry.EndChapter("defeated");
        StartCoroutine(BeginChapter(10, false));
    }

    IEnumerator FailureTransition()
    {
        flowBusy = true;
        ClearWorld();
        yield return Transition(Chapter.exit, "chapter-failure", failureReason ?? "街垒没有守住。", false);
        failed = true;
        ShowResults();
    }

    void CompleteChapter()
    {
        if (flowBusy)
            return;
        waves?.Complete();
        if (Chapter.number == 9)
        {
            StartCoroutine(AfterDefense());
            return;
        }

        StartCoroutine(CompleteWithExit());
    }

    IEnumerator CompleteWithExit()
    {
        flowBusy = true;
        ClearWorld();
        string text = Chapter.number == 7 && RunState.Decision != JavertDecision.Unchosen ? (RunState.Decision == JavertDecision.Released ? "已放走沙威" : "已处决沙威") : $"第{Chapter.number}章结束";
        // Chapter five has no authored exit pictures; omit its obsolete spy-choice slogan card.
        if(Chapter.number!=5)yield return Transition(Chapter.exit, "chapter-exit", text, false);
        failed = false;
        ShowResults();
    }

    void ShowResults()
    {
        telemetry.EndChapter(failed ? "failed" : "completed");
        flowBusy = false;
        ClearWorld();
        Phase = CampaignPhase.Results;
        titleSince = Time.unscaledTime;
        ui.Card(failed ? "本章未守住" : "本章结束", CampaignHudPresentation.Results(RunState.Perfects, RunState.Judgements.Count, RunState.Mistakes, RunState.Score, failed));
    }

    public void CombatInput(TapSec input)
    {
        if (PreparationHandoff)
        {
            if (DefenseReady && !DebugConsole.CapturingInput && !CampaignSession.InputBlocked)
            {
                if (GameBindings.Down(GameAction.AimUp))
                    input.CampaignSetAim("Up");
                else if (GameBindings.Down(GameAction.AimDown))
                    input.CampaignSetAim("Down");
            }

            return;
        }

        if (GameSettingsView.IsOpen || DebugConsole.CapturingInput || !CombatRunning || Time.frameCount <= inputBlockedThroughFrame)
            return;
        if (GameBindings.Down(GameAction.AimUp))
            input.CampaignSetAim("Up");
        else if (GameBindings.Down(GameAction.AimDown))
            input.CampaignSetAim("Down");
        var gun = input.sbullet;
        if (gun == null)
            return;
        bool firePressed=GameBindings.Down(GameAction.Fire),reloadPressed=GameBindings.Down(GameAction.Reload);
        if(gun.TryJam(firePressed,reloadPressed))return;
        if (reloadPressed)
            gun.BeginCampaignReload();
        if (!firePressed || !gun.CanFire)
            return;
        bool forced = CampaignDebug.Value.crit && gun.CurrentTarget() != null;
        bool perfect = forced || Clock.IsPerfect;
        if (gun.CurrentTarget() == null)
            perfect = false;
        if (perfect && !forced && !Clock.ClaimPerfect())
            return;
        if (forced)
            CampaignDebug.Value.crit = false;
        RecordInput(perfect);
        gun.Fire(TapSec.ShootStatu, perfect);
    }

    IEnumerator AfterDefense()
    {
        flowBusy = true;
        if (Chapter.number == 9)
        {
            yield return FinalArtilleryBarrage();
            SelectEnding(CampaignEnding.Hope, 9);
            telemetry.EndChapter("completed");
            yield return BeginChapter(10, false);
            yield break;
        }

        waves?.Complete();
        if (Chapter.number == 7)
        {
            // Keep the authored lead-in image route; its legacy key is not a player choice.
            if (cinematics != null && cinematics.Find(7, "before-choice", RunState.Decision) != null)
                yield return Transition(Chapter.exit, "before-choice", "", true);
            RunState.Decision = JavertDecision.Released;
            telemetry.Record("story_event", "javert_released");
            yield return CompleteWithExit();
            yield break;
        }

        if (Chapter.choiceBeats > 0)
        {
            if (cinematics != null && cinematics.Find(Chapter.number, "before-choice", RunState.Decision) != null)
                yield return Transition(Chapter.exit, "before-choice", "", true);
            StartChoice();
            flowBusy = false;
            yield break;
        }

        Phase = CampaignPhase.EndCard;
        ui.Card($"第{Chapter.number}章结束", "");
        Clock = new CampaignBeatClock();
        Clock.Reset(CampaignDebug.Value.tempo > 0 ? CampaignDebug.Value.tempo : BaseWait, CampaignDebug.Value.window > 0 ? CampaignDebug.Value.window : table.perfectWindow);
        Clock.WindowOpened += beat => RunState.TotalHeartbeats++;
        while (Clock.Completed < 2 || CampaignSession.Suspended || DebugConsole.SimulationPaused)
            yield return null;
        flowBusy = false;
        CompleteChapter();
    }
}
