using System;

// 弹匣/换弹状态与Unity输入分离，使用连续拍坐标确保任何时刻开始都是完整两拍。
public sealed class CampaignMagazine
{
    int ammo=3,lastFireBeat=-1;
    double reloadBeat=-1,reloadTime=-1;
    public bool IsReloading=>reloadBeat>=0||reloadTime>=0;
    public int Ammo(int capacity,bool unlimited)=>unlimited?capacity:Math.Min(capacity,ammo);
    public bool CanFire(int beat,int capacity,bool unlimited)=>!IsReloading&&Ammo(capacity,unlimited)>0&&lastFireBeat!=beat;
    public bool ReserveShot(int beat,int capacity,bool unlimited)
    {if(!CanFire(beat,capacity,unlimited))return false;lastFireBeat=beat;return true;}
    public void Consume(bool unlimited){if(!unlimited)ammo=Math.Max(0,ammo-1);}
    public void Tick(double beat,double time,int capacity)
    {if((reloadBeat>=0&&beat>=reloadBeat)||(reloadTime>=0&&time>=reloadTime))Finish(capacity);}
    public bool Reload(bool perfect,bool unlimited,int capacity,double beat,double time,double seconds)
    {
        if(unlimited)return false;
        if(perfect)
        {
            if(Ammo(capacity,false)>=capacity&&!IsReloading)return false;
            Finish(capacity);return true;
        }
        if(IsReloading||Ammo(capacity,false)>=capacity)return false;
        if(seconds>=0)reloadTime=time+Math.Max(0,seconds);else reloadBeat=beat+2;
        return true;
    }
    public void Finish(int capacity){ammo=capacity;reloadBeat=reloadTime=-1;}
    public double Remaining(double beat,double time,double period)
        =>reloadTime>=0?Math.Max(0,reloadTime-time):reloadBeat>=0?Math.Max(0,reloadBeat-beat)*period:0;
}
