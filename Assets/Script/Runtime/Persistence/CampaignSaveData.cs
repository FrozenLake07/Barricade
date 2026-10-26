using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class CampaignSaveData
{
    public int version=2,chapter,phase;
    public string utc,scene,configurationId;
    public string debugSettings; // Optional, run-only non-design overrides; old v2 saves remain readable.
    public float sceneTime,gameTime,unscaledTime;
    public List<CampaignStateCodec.Field> director,clock,savedClock,run,wall,tap,tapSec,track,spawn,wave;
    public WaveDirector.Progress waveProgress;
    public CampaignSaveWorld.Data world;
    public string main,detail,hud,caption;
    public Color black,night,mainColor,detailColor;
    public int mainFontSize;
    public float musicTime;
    public string musicTrack;
    public bool musicPlaying,hasWave,hasSavedClock;
    public static bool Stable(CampaignPhase phase)=>phase==CampaignPhase.Preparation||phase==CampaignPhase.Defense||phase==CampaignPhase.Choice
        ||phase==CampaignPhase.Epilogue||phase==CampaignPhase.LastChance||phase==CampaignPhase.Results;
    public void Validate()
    {
        if(version!=2)throw new InvalidOperationException("存档来自旧章节编排，请开始新游戏；原文件仍已保留。");
        if(chapter<1||chapter>10||!Stable((CampaignPhase)phase)||(scene!=CampaignSceneNames.Construction&&scene!=CampaignSceneNames.Defense)
            ||world==null||clock==null||run==null||director==null)throw new InvalidOperationException("存档进度或版本不正确。");
        if(chapter==2&&((CampaignPhase)phase==CampaignPhase.Defense||(CampaignPhase)phase==CampaignPhase.LastChance))
            throw new InvalidOperationException("此存档位于已取消的第二章战斗，请开始新游戏；原文件仍已保留。");
        if(!DateTime.TryParse(utc,out var savedAt))throw new InvalidOperationException("存档时间格式不正确。");
        if(hasWave&&(waveProgress==null||waveProgress.done==null))throw new InvalidOperationException("存档波次缺失。");
        if(world.entities.Count>256||world.components.Count>2048||world.nodes.Count>4096)throw new InvalidOperationException("存档对象数量异常。");
    }
    public void ValidateConfiguration(string current)
    {
        if(string.IsNullOrEmpty(configurationId)||configurationId!=current)
            throw new InvalidOperationException("章节配置已变化或此存档缺少配置版本，请开始新游戏；原存档仍保留。");
    }
    public static bool TryRead(string path,out CampaignSaveData data,out string error)
    {
        data=null;if(!SingleSaveSlot.TryRead(path,out string json,out error))return false;
        try{data=JsonUtility.FromJson<CampaignSaveData>(json);if(data==null)throw new InvalidOperationException("存档内容为空。");data.Validate();if(CampaignTelemetry.Current!=null)data.ValidateConfiguration(CampaignTelemetry.Current.ConfigurationId);return true;}
        catch(Exception e){data=null;error="无法读取存档："+e.Message;return false;}
    }
}

