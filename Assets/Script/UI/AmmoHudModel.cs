using System;

public enum AmmoHudMode { Ready, Low, Empty, Reloading, Unlimited }
public sealed class AmmoHudState
{
    public int ammo,capacity;public float progress;public bool unlimited,reloading,perfectFlash;
    public AmmoHudMode mode;public string counter,status,effect;
    public static AmmoHudState Build(int ammo,int capacity,bool unlimited,bool reloading,float remaining,float progress,float multiplier,bool flash)
    {
        capacity=Math.Max(1,capacity);ammo=Math.Max(0,Math.Min(capacity,ammo));
        var s=new AmmoHudState{ammo=ammo,capacity=capacity,unlimited=unlimited,reloading=reloading&&!unlimited,progress=Math.Max(0,Math.Min(1,progress)),perfectFlash=flash};
        s.mode=unlimited?AmmoHudMode.Unlimited:s.reloading?AmmoHudMode.Reloading:ammo==0?AmmoHudMode.Empty:ammo==1?AmmoHudMode.Low:AmmoHudMode.Ready;
        s.counter=unlimited?"∞":ammo+" / "+capacity;
        s.status=unlimited?"持续供弹":s.reloading?"装填中":ammo==0?"弹药用尽 · R 装填":"R 装填";
        s.effect=unlimited?"":capacity>3?"容量 +"+(capacity-3)+" 发":"";
        return s;
    }
}

