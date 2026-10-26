using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public sealed partial class StoryDirector
{
    IEnumerator AfterPreparation()
    {
        flowBusy = true;
        if (GameManager.WallHP >= GameManager.WallMaxHP)
            telemetry.Record("preparation_full", "beat=" + Clock.Completed + ";health=" + GameManager.WallHP);
        if (Chapter.defenseBeats == 0)
        {
            yield return ConstructionCompletionHold();
            flowBusy = false;
            CompleteChapter();
            yield break;
        }

        phaseOffset = Chapter.preparationBeats;
        GameManager.WallMaxHP = table.constructionLimit;
        if (!HasAuthoredPreparationTransition)
        {
            yield return SmoothPreparationToDefense();
            yield break;
        }

        string caption = Chapter.introduction;
        yield return Transition(Chapter.prepareToDefense, "prepare-to-defense", caption, Chapter.number != 9);
        yield return LoadScene(CampaignSceneNames.Defense);
        Phase = CampaignPhase.Defense;
        ResetClock();
        waves = new CampaignWaves(this);
        ui.Gameplay(Chapter.number == 6 ? .62f : 0);
        RhythmManager.StopMusic();
        flowBusy = false;
        if (Chapter.number == 5)
            ShowGameplayHint("开始管理弹药：R补弹；在心跳静止的间隙补弹更快。");
    }

    public void ConstructionInput(Tap tap)
    {
        if (GameSettingsView.IsOpen || DebugConsole.CapturingInput || CampaignSession.InputBlocked || Time.frameCount <= inputBlockedThroughFrame || Phase != CampaignPhase.Preparation || GameManager.WallHP >= GameManager.WallMaxHP)
            return;
        if (constructionCycle != Clock.Completed)
        {
            constructionCycle = Clock.Completed;
            ConstructionJudgement.Prompt(Chapter.number, constructionCycle + 1, out int first, out int second);
            construction.Reset(first, second, table.chordMaxGap, Time.unscaledTime);
            if (Chapter.number == 2 && constructionCycle + 1 == 49)
                ShowGameplayHint("这一拍，同时按下两个字母。先后相差不超过" + Mathf.RoundToInt(table.chordMaxGap * 1000) + "毫秒。");
        }

        if (tap.WordUI != null)
        {
            if (originalPromptSize == 0)
                originalPromptSize = tap.WordUI.fontSize;
            tap.WordUI.fontSize = construction.Chord ? Mathf.Max(20, originalPromptSize - 6) : originalPromptSize;
            tap.WordUI.text = restRemaining > 0 ? $"REST {Mathf.CeilToInt(restRemaining)}s" : GameBindings.Label(ConstructionKeys[construction.First]) + (construction.Chord ? " + " + GameBindings.Label(ConstructionKeys[construction.Second]) : "");
        }

        if (restRemaining > 0 || windowJudged)
            return;
        for (int key = 0; key < ConstructionKeys.Length; key++)
        {
            if (!Input.GetKeyDown(ConstructionKeys[key]))
                continue;
            var result = construction.Press(key, Time.unscaledTime, Clock.IsPerfect, GameOptions.PromptGraceEnabled);
            if (result == ConstructionResult.Grace)
            {
                tap.NotifyGraceInput(ConstructionKeys[key]);
                continue;
            }

            if (result == ConstructionResult.None)
                continue;
            ApplyConstructionResult(tap, result);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
            ApplyConstructionResult(tap, ConstructionResult.Wrong);
    }

    void ApplyConstructionResult(Tap tap, ConstructionResult result)
    {
        bool perfect = result == ConstructionResult.Perfect;
        RecordInput(perfect);
        if (result == ConstructionResult.Wrong)
        {
            telemetry.Record("rest");
            restRemaining = 3;
            SfxPlayer.Play("SFX-QTE-03");
            return;
        }

        if (result == ConstructionResult.Miss)
            return;
        if (perfect)
            Clock.ClaimPerfect();
        tap.NotifyCampaignInput(perfect, construction.Chord && perfect ? 1.12246f : 1f);
        int raw = CampaignRevisionRules.ConstructionReward(construction.Chord, perfect, table.chordPerfectHP, table.chordOrdinaryHP);
        raw = GameDifficultyRules.ConstructionReward(GameOptions.Difficulty, perfect, raw);
        builtHealth += raw;
        int total = Mathf.FloorToInt(builtHealth + .001f), gain = total - RunState.BuiltHP;
        RunState.BuiltHP = total;
        GameManager.AddWallHP(gain);
    }
}
