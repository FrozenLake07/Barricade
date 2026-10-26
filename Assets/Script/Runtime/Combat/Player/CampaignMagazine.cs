using System;

// 弹匣/换弹状态与Unity输入分离，使用连续拍坐标确保任何时刻开始都是完整两拍。
public sealed class CampaignMagazine
{
    int ammo=3,lastFireBeat=-1;
    double reloadBeat=-1,reloadTime=-1;
    double reloadLength;
    bool jammed;
    public bool IsJammed=>jammed&&IsReloading;
    public bool IsReloading=>reloadBeat>=0||reloadTime>=0;
    public int Ammo(int capacity,bool unlimited)=>unlimited?capacity:Math.Min(capacity,ammo);
    public bool CanFire(int beat,int capacity,bool unlimited)=>!IsReloading&&Ammo(capacity,unlimited)>0&&lastFireBeat!=beat;
    public bool ReserveShot(int beat,int capacity,bool unlimited)
    {if(!CanFire(beat,capacity,unlimited))return false;lastFireBeat=beat;return true;}
    public void Consume(bool unlimited){if(!unlimited)ammo=Math.Max(0,ammo-1);}
    public void Tick(double beat,double time,int capacity)
    {if((reloadBeat>=0&&beat>=reloadBeat)||(reloadTime>=0&&time>=reloadTime))Finish(capacity);}
    public bool Reload(bool perfect,bool unlimited,int capacity,double beat,double time,double seconds,double durationMultiplier=1)
    {
        if(unlimited||IsJammed)return false;
        if(perfect)
        {
            if(Ammo(capacity,false)>=capacity&&!IsReloading)return false;
            Finish(capacity);return true;
        }
        if(IsReloading||Ammo(capacity,false)>=capacity)return false;
        reloadLength=(seconds>=0?Math.Max(0,seconds):2)*Math.Max(.1,durationMultiplier);
        if(seconds>=0)reloadTime=time+reloadLength;else reloadBeat=beat+reloadLength;
        return true;
    }
    public bool Jam(int capacity,double beat,double durationMultiplier=1)
    {
        if(IsJammed||Ammo(capacity,false)<=0)return false;
        jammed=true;reloadTime=-1;
        reloadLength=4*Math.Max(.1,durationMultiplier);reloadBeat=beat+reloadLength;
        return true;
    }
    public void SetAmmo(int value,int capacity){ammo=Math.Max(0,Math.Min(capacity,value));jammed=false;reloadBeat=reloadTime=-1;reloadLength=0;}
    public void Finish(int capacity){ammo=capacity;jammed=false;reloadBeat=reloadTime=-1;reloadLength=0;}
    public double Progress(double beat,double time)
        =>!IsReloading?1:Math.Max(0,Math.Min(1,1-(reloadTime>=0?reloadTime-time:reloadBeat-beat)/Math.Max(.001,reloadLength)));
    public double Remaining(double beat,double time,double period)
        =>reloadTime>=0?Math.Max(0,reloadTime-time):reloadBeat>=0?Math.Max(0,reloadBeat-beat)*period:0;
}
