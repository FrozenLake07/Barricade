using System;
using System.Collections.Generic;

// 设计给出的是等待时间；实际周期始终加上已确认的0.5秒窗口。
public static class WaveAudit
{
    public static readonly double[] Periods={1.35,1.2,1.0};
    public static double WorstArrival(WaveScript script,WaveEvent e)
    {
        if(e.alignTo>=0)return e.alignTo;
        double worst=e.beat;
        foreach(double period in Periods)
        {
            double travel=e.type==WaveEventType.Soldier?Math.Max(0,script.auditDistance-e.advance)/Math.Max(.01,script.auditSoldierSpeed)/period:
                e.type==WaveEventType.Squad?Math.Max(script.auditSquadSeconds/period,MaxImpact(script)):
                e.type==WaveEventType.Shell?Math.Max(script.auditShellSeconds/period,script.shellTravelBeats):0;
            worst=Math.Max(worst,e.beat+(e.count-1)*e.spacing+travel);
        }
        return worst;
    }
    static int MaxImpact(WaveScript s){int max=0;foreach(int offset in s.squadImpactOffsets)max=Math.Max(max,offset);return max;}
    public static string[] Errors(WaveScript script)
    {
        var errors=new List<string>();
        foreach(var e in script.events)
        {
            double worst=WorstArrival(script,e);
            int end=script.defenseBeats;
            if((e.type==WaveEventType.Squad||e.type==WaveEventType.Shell)&&worst>end-4)errors.Add($"{script.chapter}{script.variant} {e.tag} 扣血事件最坏命中{worst:F2}>{end-4}");
        }
        return errors.ToArray();
    }
    public static string[] Warnings(WaveScript script)
    {
        var warnings=new List<string>();int end=script.defenseBeats;
        foreach(var e in script.events)
        {
            double worst=WorstArrival(script,e);
            if(e.beat+(e.count-1)*e.spacing>end-4)warnings.Add($"{script.chapter}{script.variant} {e.tag} 生成晚于齐射起点{end-4}");
            if(e.type==WaveEventType.Soldier&&worst>end-4)warnings.Add($"{script.chapter}{script.variant} {e.tag} 士兵预计{worst:F2}拍到达，由末端齐射收场");
        }
        return warnings.ToArray();
    }
}
