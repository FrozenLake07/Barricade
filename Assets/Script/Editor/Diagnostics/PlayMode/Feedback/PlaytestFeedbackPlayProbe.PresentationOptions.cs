#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator PresentationOptionsTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        report.coverage="Actual settings controls, category AudioSource gains including loops/heartbeat/count-in/narration, subtitle scaling and layouts, title/paused recalibration and restored BGM cursor, difficulty tutorial consistency, in-memory save/restore retaining local preferences. Listener muted; no hardware pointer injection or listening. User save slots and scene not saved, preferences restored.";
        GamePresentationOptions.MusicVolume=GamePresentationOptions.EffectsVolume=GamePresentationOptions.HeartbeatVolume=1;GamePresentationOptions.SubtitleScale=1;
        GameOptions.Difficulty=GameDifficulty.Normal;
        ChapterMechanicIntroduction.TryGet(2,.14f,out _,out string normal);
        Check(normal.Contains("普通成功增加5")&&normal.Contains("短暂保护"),"Normal construction introduction matches healing and grace");
        GameOptions.Difficulty=GameDifficulty.Nightmare;
        ChapterMechanicIntroduction.TryGet(2,.14f,out _,out string nightmare);
        Check(nightmare.Contains("普通成功不加血")&&nightmare.Contains("不提供换字保护")&&!nightmare.Contains("普通成功增加5"),"Nightmare introduction states no ordinary healing and no grace");
        ChapterMechanicIntroduction.TryGet(4,.14f,out string escortTitle,out string escort);
        Check(escortTitle.Contains("Gavroche")&&escort.Contains("Gavroche")&&!escort.Contains("伽弗洛什"),"Escort tutorial uses English character name");
        ChapterMechanicIntroduction.TryGet(5,.14f,out _,out string ammunition);
        Check(!ammunition.Contains("卡壳"),"Ammunition introduction has no added jam explanation");
        GameOptions.Difficulty=GameDifficulty.Normal;
        yield return story.DebugPhase("title");yield return Frames(3);
        var settings=GameSettingsView.Current;settings.Open();yield return Frames(3);
        var page=Get<GameObject>(settings,"page");
        Check(GameSettingsView.IsOpen&&Time.timeScale==0&&AudioListener.pause,"Opening title settings freezes gameplay and pauses audio");
        Check(Get<GameObject>(settings,"controlsPage").activeSelf&&!Get<GameObject>(settings,"audioPage").activeSelf,"Original controls/difficulty page remains available");
        CaptureUi(1920,1080,"操作与难度1920");CaptureUi(960,540,"操作与难度960");
        page.transform.Find("AudioTab").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        var musicSlider=Get<UnityEngine.UI.Slider>(settings,"musicSlider");var effectsSlider=Get<UnityEngine.UI.Slider>(settings,"effectsSlider");var heartSlider=Get<UnityEngine.UI.Slider>(settings,"heartbeatSlider");var subSlider=Get<UnityEngine.UI.Slider>(settings,"subtitleSlider");
        Get<UnityEngine.UI.Slider>(settings,"masterSlider").value=65;
        Check(Math.Abs(AudioListener.volume-.65f)<.001f,"Master slider updates actual listener volume");AudioListener.volume=0;
        musicSlider.value=25;effectsSlider.value=40;heartSlider.value=75;subSlider.value=150;yield return Frames(3);
        Check(GamePresentationOptions.MusicVolume==.25f&&GamePresentationOptions.EffectsVolume==.4f&&GamePresentationOptions.HeartbeatVolume==.75f&&GamePresentationOptions.SubtitleScale==1.5f,"All four category/subtitle sliders save independent preferences");
        Check(PlayerPrefs.GetFloat(GamePresentationOptions.MusicKey)==.25f&&PlayerPrefs.GetFloat(GamePresentationOptions.SubtitleKey)==1.5f,"Preferences are persisted outside game save slots");
        Check(page.transform.Find("AudioPage/SubtitlePreview").GetComponent<UnityEngine.UI.Text>().fontSize==48,"Subtitle preview grows without scaling the menu");
        var labels=page.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        Check(labels.All(t=>t.font==GameFont.Silver&&t.text.Where(c=>!char.IsWhiteSpace(c)).All(c=>t.font.HasCharacter(c))),"Every settings and calibration label uses game font without missing glyphs");
        CaptureUi(1920,1080,"声音与字幕1920");CaptureUi(960,540,"声音与字幕960");
        page.transform.Find("AudioPage/Recalibrate").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return Until(()=>Get<HeartbeatAudioOutput>(settings,"calibrationOutput").ScheduledCount>=4);
        Check(story.Phase==CampaignPhase.Title&&Time.timeScale==0&&!AudioListener.pause,"Title recalibration runs its preview without starting a chapter");
        float delay=RhythmManager.ExtraOutputDelayMs;
        page.transform.Find("SettingsCalibration/Later").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(RhythmManager.ExtraOutputDelayMs==Mathf.Clamp(delay+10,-100,500),"Recalibration adjusts and persists actual rhythm latency");
        yield return Until(()=>Get<UnityEngine.UI.Text>(settings,"calibrationState").text=="心跳");
        CaptureUi(1920,1080,"设置内声音校准1920");CaptureUi(960,540,"设置内声音校准960");
        page.transform.Find("SettingsCalibration/FinishCalibration").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return Frames(2);
        Check(AudioListener.pause&&Get<GameObject>(settings,"audioPage").activeSelf&&GameSettingsView.IsOpen,"Finishing calibration returns to sound settings and restores its audio pause");
        settings.Close();Check(Time.timeScale==1&&!AudioListener.pause,"Closing title settings restores live state");
        OpenLyricsChapter(3);yield return Until(()=>RunState.Chapter==3&&Film.Active&&!Film.IsBlackCardVisible,true);yield return Frames(3);
        var music=Get<AudioSource>(story,"audioSource");
        float originalGain=music.volume/.25f;
        Check(music.isPlaying&&originalGain>0,"Chapter music is running for actual gain verification");
        GamePresentationOptions.MusicVolume=0;yield return Frames(2);Check(music.volume==0&&music.isPlaying,"Music mute preserves playback cursor rather than stopping the cue");
        GamePresentationOptions.MusicVolume=.5f;yield return Frames(2);Check(Math.Abs(music.volume-originalGain*.5f)<.01f,"Music volume multiplies existing cinematic mix gain");
        CampaignSession.Current.Pause();yield return Frames(2);settings.Open();yield return Frames(2);
        float musicAt=music.time;int beatAt=story.Clock.Completed;
        page=Get<GameObject>(settings,"page");page.transform.Find("AudioPage/Recalibrate").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return Until(()=>Get<HeartbeatAudioOutput>(settings,"calibrationOutput").ScheduledCount>=4);
        double previewDsp=AudioSettings.dspTime;yield return new WaitForSecondsRealtime(1.8f);
        Check(AudioSettings.dspTime-previewDsp>1.5,"Calibration audio clock actually advances while gameplay remains paused");
        Check(Math.Abs(music.time-musicAt)<.05f&&story.Clock.Completed==beatAt&&CampaignSession.Suspended,"Paused recalibration holds campaign music cursor and beat clock");
        page.transform.Find("SettingsCalibration/FinishCalibration").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();settings.Close();
        Check(AudioListener.pause&&CampaignSession.Suspended&&Time.timeScale==0,"Closing recalibration/settings retains original pause menu");
        CampaignSession.Current.Resume();yield return Until(()=>!CampaignSession.Suspended);yield return Frames(3);
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"音乐恢复检查.txt"),"playing="+music.isPlaying+";before="+musicAt+";after="+music.time+";active="+Get<bool>(story,"musicPlaybackActive")+";listenerPaused="+AudioListener.pause+";scale="+Time.timeScale);
        Check(music.isPlaying&&music.time>=musicAt&&music.time<musicAt+.5f,"Resuming after calibration continues the same music position");
        var lyricView=story.GetComponent<CampaignMusicLyricsView>();
        Check(Get<UnityEngine.UI.Text>(lyricView,"chinese").fontSize==63&&Get<UnityEngine.UI.Text>(lyricView,"english").fontSize==45,"Both lyric languages follow subtitle size");
        CaptureUi(1920,1080,"放大双语字幕1920");CaptureUi(960,540,"放大双语字幕960");
        SfxPlayer.StopAll();GamePresentationOptions.EffectsVolume=1;SfxPlayer.Play("weapon_reload_mechanism",.8f);yield return Frames(1);
        var source=FindObjectsByType<AudioSource>(FindObjectsSortMode.None).First(s=>s.clip==SfxPlayer.ClipFor("weapon_reload_mechanism")&&s.isPlaying);float effectGain=source.volume;
        GamePresentationOptions.EffectsVolume=0;yield return Frames(2);Check(source.volume==0&&GamePresentationOptions.HeartbeatVolume==.75f,"Active effect mute does not change heartbeat preference");
        GamePresentationOptions.EffectsVolume=.5f;yield return Frames(2);Check(Math.Abs(source.volume-effectGain*.5f)<.001f,"Active effects respond immediately to volume changes");
        var loopHost=new GameObject("OptionsLoopProbe");var loop=loopHost.AddComponent<SelectedSoundLoop>();loop.Set("threat_grenade_fuse_loop",true,.4f);yield return Frames(1);
        var loopSource=loopHost.GetComponent<AudioSource>();float loopGain=loopSource.volume;
        GamePresentationOptions.EffectsVolume=0;yield return Frames(2);Check(loopSource.volume==0&&loop.Active,"Already playing looping effects respond to mute");
        GamePresentationOptions.EffectsVolume=.5f;yield return Frames(2);Check(Math.Abs(loopSource.volume-loopGain)<.001f,"Loop effects restore their original gain without double scaling");
        loop.Set("threat_grenade_fuse_loop",false,0);Destroy(loopHost);
        var voiceSource=Get<AudioSource>(Film,"voice");GamePresentationOptions.EffectsVolume=.3f;yield return Frames(2);
        Check(Math.Abs(voiceSource.volume-.3f)<.001f&&Math.Abs(Get<AudioSource>(story,"transitionAudio").volume-.3f)<.001f,"Narration and non-music transition sounds share effects preference");
        GamePresentationOptions.EffectsVolume=.5f;
        var host=new GameObject("OptionsHeartbeatProbe");var heart=new HeartbeatAudioOutput(host);
        try
        {
            double now=AudioSettings.dspTime;heart.BeginFrame(now,0,.8f);heart.Submit(1,0,1,false,now+.5,now+1,1);
            var heartSource=host.GetComponent<AudioSource>();Check(heartSource!=null&&Math.Abs(heartSource.volume-.6f)<.001f,"Heartbeat has independent gain even with effects volume changed");
            GamePresentationOptions.HeartbeatVolume=0;heart.BeginFrame(AudioSettings.dspTime,0,.8f);Check(heartSource.volume==0,"Heartbeat mute applies to already scheduled voices");
        }
        finally{heart.Dispose();Destroy(host);}
        GamePresentationOptions.HeartbeatVolume=.75f;
        yield return story.DebugChapter(5,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(2);
        var saved=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(story.CaptureSavedGame()));yield return story.RestoreSavedGame(saved);yield return Frames(3);
        Check(story.Phase==CampaignPhase.Defense&&GamePresentationOptions.MusicVolume==.5f&&GamePresentationOptions.EffectsVolume==.5f&&GamePresentationOptions.HeartbeatVolume==.75f&&GamePresentationOptions.SubtitleScale==1.5f,"Actual game save/restore preserves local presentation preferences");
        GamePresentationOptions.EffectsVolume=0;CampaignSession.Current.Pause();yield return Frames(2);settings=GameSettingsView.Current;settings.Open();yield return Frames(2);
        page=Get<GameObject>(settings,"page");page.transform.Find("AudioTab").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        int defenseBeat=story.Clock.Completed;float defenseHP=GameManager.WallHP;
        page.transform.Find("AudioPage/Recalibrate").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.8f);
        Check(story.Phase==CampaignPhase.Defense&&story.Clock.Completed==defenseBeat&&GameManager.WallHP==defenseHP,"Defense recalibration preserves active phase, beat and wall health");
        page.transform.Find("SettingsCalibration/FinishCalibration").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();settings.Close();CampaignSession.Current.Resume();yield return Frames(3);
        Check(CampaignSession.Counting&&Math.Abs(Get<AudioSource>(CampaignSession.Current,"pulse").volume-.45f)<.001f,"Resume countdown heartbeat uses heartbeat volume even when effects are muted");
        yield return Until(()=>!CampaignSession.Suspended);
        Check(Time.timeScale==1&&!AudioListener.pause,"Defense resumes through its normal count-in after recalibration");
    }
}
#endif
