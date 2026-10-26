using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public sealed partial class StoryDirector
{
    public bool CanPause => Phase != CampaignPhase.Title && Phase != CampaignPhase.Calibration && Phase != CampaignPhase.Finished;
    public bool CanSave => !flowBusy && CampaignSaveData.Stable(Phase);

    static readonly string[] SavedDirectorFields =
    {
        "phaseOffset",
        "lastChancesUsed",
        "constructionCycle",
        "judgementOffset",
        "builtHealth",
        "restRemaining",
        "windowJudged",
        "failed",
        "<FrozenUntilBeat>k__BackingField",
        "originalPromptSize",
        "captionUntil",
        "titleSince",
        "failureReason",
        "construction"
    };
    public CampaignSaveData CaptureSavedGame()
    {
        if (!CanSave)
            throw new InvalidOperationException("当前过场尚未结束。");
        var world = new CampaignSaveWorld();
        var state = world.Capture();
        var data = new CampaignSaveData
        {
            debugSettings = CampaignDebug.Value.nonDesign ? JsonUtility.ToJson(CampaignDebug.Value) : null,
            configurationId = telemetry.ConfigurationId,
            chapter = RunState.Chapter,
            phase = (int)Phase,
            utc = DateTime.UtcNow.ToString("O"),
            scene = SceneManager.GetActiveScene().name,
            sceneTime = Time.timeSinceLevelLoad,
            gameTime = Time.time,
            unscaledTime = Time.unscaledTime,
            world = state,
            director = CampaignStateCodec.Capture(this, world.Reference, SavedDirectorFields),
            clock = CampaignStateCodec.Capture(Clock, world.Reference),
            hasSavedClock = savedClock != null,
            savedClock = savedClock != null ? CampaignStateCodec.Capture(savedClock, world.Reference) : null,
            run = CampaignStateCodec.Capture(typeof(RunState), world.Reference),
            wall = CampaignStateCodec.Capture(typeof(GameManager), world.Reference, "WallHP", "WallMaxHP", "bulletDamageRemainder", "bulletDamageHealth"),
            tap = CampaignStateCodec.Capture(typeof(Tap), world.Reference, "beattime", "oribeattime", "Setbeattime", "perfecttime", "Setperfecttime"),
            tapSec = CampaignStateCodec.Capture(typeof(TapSec), world.Reference),
            track = CampaignStateCodec.Capture(typeof(CapsuleTrack), world.Reference),
            spawn = CampaignStateCodec.Capture(typeof(Spawn), world.Reference, "nextGroupId"),
            main = ui.Main.text,
            detail = ui.Detail.text,
            hud = ui.Hud.text,
            caption = ui.Caption.text,
            mainFontSize = ui.Main.fontSize,
            black = ui.Black.color,
            night = ui.Night.color,
            mainColor = ui.Main.color,
            detailColor = ui.Detail.color,
            musicTrack = MusicTrackId(),
            musicPlaying = musicPlaybackActive && (audioSource.isPlaying || MusicSuspended),
            musicTime = audioSource.clip != null ? audioSource.time : 0
        };
        if (WaveDirector.Current != null)
        {
            data.hasWave = true;
            data.waveProgress = WaveDirector.Current.CaptureProgress();
            data.wave = CampaignStateCodec.Capture(WaveDirector.Current, world.Reference, "cannon", "eventsBeat", "eventsThisBeat", "reducedIndex", "debugDensityCarry", "<FinaleStarted>k__BackingField", "<ShellInterceptions>k__BackingField", "cannonRetreated");
        }

        data.Validate();
        return data;
    }

    public IEnumerator RestoreSavedGame(CampaignSaveData data)
    {
        data.Validate();
        data.ValidateConfiguration(telemetry.ConfigurationId);
        CampaignDebug.Restore(data.debugSettings);
        telemetry.StartRun("load");
        SfxPlayer.StopAll();
        StopAllCoroutines();
        Phase = CampaignPhase.Transition;
        flowBusy = true;
        foreach (var director in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
            director.Stop();
        cutscene.playableAsset = null;
        StopCampaignMusic();
        transitionAudio.Stop();
        RhythmManager.StopMusic();
        ClearWorld();
        yield return SceneManager.LoadSceneAsync(data.scene);
        yield return null;
        var world = new CampaignSaveWorld();
        RunState.NewCampaign();
        CampaignStateCodec.Restore(typeof(RunState), data.run, world.Resolve);
        float sceneOffset = Time.timeSinceLevelLoad - data.sceneTime, gameOffset = Time.time - data.gameTime, unscaledOffset = Time.unscaledTime - data.unscaledTime;
        Clock = new CampaignBeatClock();
        Clock.Reset(CampaignDebug.Value.tempo > 0 ? CampaignDebug.Value.tempo : BaseWait, CampaignDebug.Value.window > 0 ? CampaignDebug.Value.window : table.perfectWindow);
        CampaignStateCodec.Restore(Clock, data.clock, world.Resolve);
        bool rescue = (CampaignPhase)data.phase == CampaignPhase.LastChance;
        if (rescue)
            Clock.WindowOpened += beat => RunState.TotalHeartbeats++;
        else
        {
            Clock.WindowOpened += OnWindow;
            Clock.CycleCompleted += OnCycleCompleted;
        }

        savedClock = null;
        if (data.hasSavedClock && data.savedClock != null)
        {
            savedClock = new CampaignBeatClock();
            savedClock.Reset(CampaignDebug.Value.tempo > 0 ? CampaignDebug.Value.tempo : BaseWait, CampaignDebug.Value.window > 0 ? CampaignDebug.Value.window : table.perfectWindow);
            CampaignStateCodec.Restore(savedClock, data.savedClock, world.Resolve);
            savedClock.WindowOpened += OnWindow;
            savedClock.CycleCompleted += OnCycleCompleted;
        }

        WaveDirector.NewRun();
        if (data.hasWave)
            waves = new CampaignWaves(this, data.waveProgress);
        world.Create(data.world);
        yield return null;
        world.Apply(sceneOffset, gameOffset, unscaledOffset);
        CampaignStateCodec.Restore(this, data.director, world.Resolve, sceneOffset, gameOffset, unscaledOffset);
        GameManager.ResetBulletDamage();
        CampaignStateCodec.Restore(typeof(GameManager), data.wall, world.Resolve);
        CampaignStateCodec.Restore(typeof(Tap), data.tap, world.Resolve);
        CampaignStateCodec.Restore(typeof(TapSec), data.tapSec, world.Resolve);
        CampaignStateCodec.Restore(typeof(CapsuleTrack), data.track, world.Resolve, sceneOffset, gameOffset, unscaledOffset);
        CampaignStateCodec.Restore(typeof(Spawn), data.spawn, world.Resolve);
        if (WaveDirector.Current != null)
            CampaignStateCodec.Restore(WaveDirector.Current, data.wave, world.Resolve);
        telemetry.BeginChapter("loaded_partial");
        Phase = (CampaignPhase)data.phase;
        flowBusy = false;
        inputBlockedThroughFrame = Time.frameCount;
        ui.Gameplay(0);
        ui.Main.text = data.main;
        ui.Detail.text = data.detail;
        ui.Hud.text = "";
        ui.Caption.text = data.caption;
        ui.Main.fontSize = data.mainFontSize;
        ui.Main.color = data.mainColor;
        ui.Detail.color = data.detailColor;
        ui.Black.color = data.black;
        ui.Night.color = data.night;
        foreach (var visual in FindObjectsByType<BarricadeHealthVisual>(FindObjectsSortMode.None))
            visual.RefreshNow();
        foreach (var heart in FindObjectsByType<HeartFill>(FindObjectsSortMode.None))
            heart.RefreshNow();
        musicFader.SetVoiceActive(false);
        musicFader.SetAnimationActive(false);
        musicFader.FadeToPhase();
        audioSource.loop = false;
        audioSource.clip = ResolveMusicTrack(data.musicTrack);
        if (data.musicPlaying && Chapter.number != 9 && audioSource.clip != null)
        {
            audioSource.time = Mathf.Clamp(data.musicTime, 0, Mathf.Max(0, audioSource.clip.length - .05f));
            audioSource.Play();
            musicPlaybackActive = true;
        }

        if (Phase == CampaignPhase.Results)
            ui.Detail.text = CampaignHudPresentation.Results(RunState.Perfects, RunState.Judgements.Count, RunState.Mistakes, RunState.Score, failed);
        if (Phase == CampaignPhase.Choice)
        {
            ui.Detail.text = "Q：放走沙威\nE：处决沙威";
            UpdateChoiceDisplay();
        }

        if (Phase == CampaignPhase.LastChance)
        {
            ui.Main.text = "听心跳，抓住静止的瞬间";
            ui.Detail.text = "在下一次静止间歇按空格，尝试再次站稳。";
        }

        SyncTimers();
    }

    public IEnumerator RecoverLoadFailure()
    {
        Phase = CampaignPhase.Transition;
        ClearWorld();
        yield return SceneManager.LoadSceneAsync(CampaignSceneNames.Title);
        ShowTitle();
    }
}
