using System;
using System.Collections;
using UnityEngine;

// 开场可跳过的音量/延迟试听，复用既有心跳排程与保存接口。
public static class OpeningCalibration
{
    public static bool DebugSkip;
    public const float HeartbeatVolume=.85f;
    public static IEnumerator Run(CampaignUI ui,Transform parent)
    {
        DebugSkip=false;
        var host=new GameObject("CalibrationHeartbeat");host.transform.SetParent(parent,false);
        var output=new HeartbeatAudioOutput(host);
        float size=ui.Main.fontSize;ui.Card("声音校准");
        AudioListener.volume=PlayerPrefs.GetFloat("Barricade.MasterVolume",.8f);
        double origin=AudioSettings.dspTime+Math.Max(.4,RhythmManager.OutputDelaySeconds+.2);
        float entered=Time.unscaledTime;
        var heart=ParisUI.Panel(ui.View.transform,"CalibrationHeart",ParisUI.Style.heart,.45f,.46f,.55f,.63f);
        heart.type=UnityEngine.UI.Image.Type.Simple;heart.preserveAspect=true;heart.color=ParisUI.Style.heartTint;heart.raycastTarget=false;
        try
        {
            while(true)
            {
                if(DebugSkip)break;
                if(DebugConsole.SimulationPaused){yield return null;continue;}
                double now=AudioSettings.dspTime;
                output.BeginFrame(now,RhythmManager.OutputDelaySeconds,HeartbeatVolume);
                long cycle=(long)Math.Floor((now-origin)/1.35);
                for(long n=Math.Max(0,cycle);n<=cycle+2;n++)
                {
                    double start=origin+n*1.35,end=start+.85;
                    output.Submit(1,n,1,false,start+.18,end,1);
                    output.Submit(1,n,2,false,start+.48,end,1);
                }
                double phase=now<origin?-1:(now-origin)%1.35;
                double pulse=Math.Max(Math.Exp(-Math.Abs(phase-.18)*30),Math.Exp(-Math.Abs(phase-.48)*30));
                ui.Main.text=phase<0?"声音校准":phase>=.85?"静止":"心跳";
                ui.Main.fontSize=(int)(48+pulse*8);
                heart.transform.localScale=Vector3.one*(1f+(float)pulse*.18f);
                ui.Detail.text=$"让听到的心跳与心脏跳动同步\n← / → 调整音量：{AudioListener.volume:P0}   A / D 调整声音延迟：{RhythmManager.ExtraOutputDelayMs:F0} ms\nEnter 保存并继续   Esc 使用当前设置跳过";
                if(DebugSkip)break;
                if(DebugConsole.CapturingInput){yield return null;continue;}
                if(Input.GetKeyDown(KeyCode.LeftArrow))AudioListener.volume=Mathf.Max(0,AudioListener.volume-.05f);
                if(Input.GetKeyDown(KeyCode.RightArrow))AudioListener.volume=Mathf.Min(1,AudioListener.volume+.05f);
                if(Input.GetKeyDown(KeyCode.A)){RhythmManager.AdjustOutputDelay(-10);output.Stop();}
                if(Input.GetKeyDown(KeyCode.D)){RhythmManager.AdjustOutputDelay(10);output.Stop();}
                if(Time.unscaledTime>entered+.3f&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Escape)))break;
                yield return null;
            }
            CampaignTelemetry.Current?.Record("calibration",$"volume={AudioListener.volume:F2};delay_ms={RhythmManager.ExtraOutputDelayMs:F0}");
            PlayerPrefs.SetFloat("Barricade.MasterVolume",AudioListener.volume);
            PlayerPrefs.SetInt("Barricade.CalibrationReady",1);PlayerPrefs.Save();
        }
        finally
        {output.Dispose();UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(heart.gameObject);ui.Main.fontSize=(int)size;}
    }
}
