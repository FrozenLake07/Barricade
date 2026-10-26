using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class GameSettingsView
{
    sealed class HeldAudio{public AudioSource source;public bool muted,playing;}
    readonly List<HeldAudio> heldAudio=new List<HeldAudio>();
    GameObject calibrationPage,calibrationHost;
    HeartbeatAudioOutput calibrationOutput;
    UnityEngine.UI.Image calibrationHeart;
    UnityEngine.UI.Text calibrationState,calibrationDelay,calibrationVolume;
    bool calibrating;
    double calibrationOrigin;
    float calibrationEntered;
    void BuildSettingsCalibration()
    {
        calibrationPage=Node(page.transform,"SettingsCalibration",0,0,1,1);
        var title=Label(calibrationPage.transform,"Title","声音校准",.08f,.70f,.92f,.78f,48);title.alignment=TextAnchor.MiddleCenter;
        var tip=Label(calibrationPage.transform,"Tip","让听到的心跳与心脏跳动同步",.08f,.61f,.92f,.69f,32);tip.alignment=TextAnchor.MiddleCenter;
        calibrationHeart=Node(calibrationPage.transform,"Heart",.45f,.43f,.55f,.59f).AddComponent<UnityEngine.UI.Image>();
        calibrationHeart.sprite=ParisUI.Style.heart;calibrationHeart.preserveAspect=true;calibrationHeart.color=ParisUI.Style.heartTint;calibrationHeart.raycastTarget=false;
        calibrationState=Label(calibrationPage.transform,"State","",.08f,.34f,.92f,.41f,40);calibrationState.alignment=TextAnchor.MiddleCenter;
        Button(calibrationPage.transform,"Earlier","-",.24f,.235f,.33f,.30f,()=>AdjustSettingsDelay(-10));
        calibrationDelay=Label(calibrationPage.transform,"Delay","",.34f,.235f,.66f,.30f,32);calibrationDelay.alignment=TextAnchor.MiddleCenter;
        Button(calibrationPage.transform,"Later","+",.67f,.235f,.76f,.30f,()=>AdjustSettingsDelay(10));
        var keys=Label(calibrationPage.transform,"Keys","A / D 调整声音延迟    ← / → 调整心跳音量",.08f,.165f,.92f,.22f,28);keys.alignment=TextAnchor.MiddleCenter;
        calibrationVolume=Label(calibrationPage.transform,"Volume","",.08f,.08f,.60f,.145f,30);
        Button(calibrationPage.transform,"FinishCalibration","保存并返回",.65f,.08f,.92f,.145f,StopSettingsCalibration);
        calibrationPage.SetActive(false);
    }
    void HoldOtherAudio()
    {
        foreach(var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if(source.gameObject==calibrationHost)continue;
            if(!heldAudio.Exists(item=>item.source==source))heldAudio.Add(new HeldAudio{source=source,muted=source.mute,playing=source.isPlaying||(StoryDirector.Current!=null&&StoryDirector.Current.HasActiveMusicSource(source))||(AudioListener.pause&&source.timeSamples>0)});
            source.mute=true;source.Pause();
        }
    }
    void StartSettingsCalibration()
    {
        if(calibrating||!IsOpen)return;
        calibrating=true;listening=null;controlsPage.SetActive(false);audioPage.SetActive(false);
        controlsTab.gameObject.SetActive(false);audioTab.gameObject.SetActive(false);status.gameObject.SetActive(false);page.transform.Find("Close").gameObject.SetActive(false);
        calibrationPage.SetActive(true);calibrationHost=new GameObject("SettingsCalibrationHeartbeat");calibrationHost.transform.SetParent(transform,false);
        HoldOtherAudio();AudioListener.pause=false;
        calibrationOutput=new HeartbeatAudioOutput(calibrationHost);ResetSettingsCalibration();calibrationEntered=Time.unscaledTime;
        CampaignSession.BlockCurrentInput();
    }
    void ResetSettingsCalibration()
    {
        calibrationOutput?.Stop();calibrationOrigin=AudioSettings.dspTime+Math.Max(.4,RhythmManager.OutputDelaySeconds+.2);
    }
    void AdjustSettingsDelay(float amount){RhythmManager.AdjustOutputDelay(amount);ResetSettingsCalibration();}
    void StopSettingsCalibration()
    {
        if(!calibrating)return;
        AudioListener.pause=true;
        calibrationOutput?.Dispose();calibrationOutput=null;if(calibrationHost!=null)Destroy(calibrationHost);calibrationHost=null;
        foreach(var item in heldAudio)if(item.source!=null){item.source.mute=item.muted;if(item.playing)item.source.UnPause();}
        heldAudio.Clear();calibrating=false;calibrationPage.SetActive(false);
        controlsTab.gameObject.SetActive(true);audioTab.gameObject.SetActive(true);status.gameObject.SetActive(true);page.transform.Find("Close").gameObject.SetActive(true);
        SelectPreferenceTab(audioSelected);RefreshAudioPreferences();CampaignSession.BlockCurrentInput();
        PlayerPrefs.SetInt("Barricade.CalibrationReady",1);PlayerPrefs.Save();
    }
    void UpdateSettingsCalibrationInput()
    {
        if(DebugConsole.CapturingInput||DebugConsole.SimulationPaused)return;
        if(Input.GetKeyDown(KeyCode.A))AdjustSettingsDelay(-10);
        if(Input.GetKeyDown(KeyCode.D))AdjustSettingsDelay(10);
        if(Input.GetKeyDown(KeyCode.LeftArrow))GamePresentationOptions.HeartbeatVolume-=.05f;
        if(Input.GetKeyDown(KeyCode.RightArrow))GamePresentationOptions.HeartbeatVolume+=.05f;
        if(Time.unscaledTime>calibrationEntered+.3f&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Escape)))StopSettingsCalibration();
    }
    void LateUpdate()
    {
        if(!calibrating||calibrationOutput==null)return;
        HoldOtherAudio();
        double now=AudioSettings.dspTime;
        calibrationOutput.BeginFrame(now,RhythmManager.OutputDelaySeconds,OpeningCalibration.HeartbeatVolume);
        long cycle=(long)Math.Floor((now-calibrationOrigin)/1.35);
        for(long n=Math.Max(0,cycle);n<=cycle+2;n++)
        {
            double start=calibrationOrigin+n*1.35,end=start+.85;
            calibrationOutput.Submit(1,n,1,false,start+.18,end,1);
            calibrationOutput.Submit(1,n,2,false,start+.48,end,1);
        }
        double phase=now<calibrationOrigin?-1:(now-calibrationOrigin)%1.35;
        double pulse=phase<0?0:Math.Max(Math.Exp(-Math.Abs(phase-.18)*30),Math.Exp(-Math.Abs(phase-.48)*30));
        calibrationHeart.transform.localScale=Vector3.one*(1+(float)pulse*.18f);
        calibrationState.text=phase<0?"准备":phase>=.85?"静止":"心跳";
        calibrationDelay.text=$"声音延迟：{RhythmManager.ExtraOutputDelayMs:F0} ms";
        calibrationVolume.text=$"心跳音量：{GamePresentationOptions.HeartbeatVolume:P0}    Enter / Esc 返回";
    }
}

