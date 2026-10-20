using System;

// 一拍保留“等待+0.5秒判定窗口”，不偷改窗口来迎合设计文档的时长估算。
public sealed class CampaignBeatClock
{
    public float Wait {get;private set;}
    public float NextWait {get;set;}
    public float Window {get;private set;}
    public float Elapsed {get;private set;}
    public int Opened {get;private set;}
    public int Completed {get;private set;}
    bool opened, claimed;
    public bool IsPerfect => opened && Elapsed < Wait+Window;
    public float BeatRemaining => Math.Max(0f,Wait-Elapsed);
    public float PerfectRemaining => opened?Math.Max(0f,Wait+Window-Elapsed):Window;
    public float Period => Wait+Window;
    public event Action<int> WindowOpened;
    public event Action<int> CycleCompleted;
    public void Reset(float wait,float window)
    {Wait=NextWait=Math.Max(.05f,wait);Window=Math.Max(.01f,window);Elapsed=0;Opened=Completed=0;opened=claimed=false;}
    public void Advance(float delta)
    {
        if(delta<=0)return;
        Elapsed+=delta;
        // 一帧至多处理一个周期；卡帧不瞬间吐出多个波次。
        if(!opened && Elapsed>=Wait){opened=true;Opened++;WindowOpened?.Invoke(Opened);}
        if(Elapsed>=Wait+Window){CycleCompleted?.Invoke(Completed+1);Completed++;Elapsed=0;Wait=Math.Max(.05f,NextWait);opened=claimed=false;}
    }
    public bool ClaimPerfect(){if(!IsPerfect||claimed)return false;claimed=true;return true;}
}
