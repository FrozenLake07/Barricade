using System;
using System.Collections.Generic;

// Shared by editable assets and fallback defaults; pressure is visible arrivals, not damage inflation.
public static class CombatPressureRules
{
    public const string PacketPrefix="pressure-v4-";
    public static void Apply(WaveScript script)
    {
        var events=new List<WaveEvent>();
        foreach(var e in script.events)
        {
            if((e.tag??"").StartsWith("pressure-",StringComparison.Ordinal))continue;
            if(!(script.chapter==3&&e.beat==0&&e.tag=="opening"))e.advance=0;
            events.Add(e);
        }
        int step=script.chapter<=6?3:2;
        int last=script.defenseBeats-8,index=0;
        for(int beat=3;beat<=last;beat+=step,index++)
        {
            int lane=(index+script.chapter)%3+1;
            bool multi=script.chapter==3?index%3==2:script.chapter<=6?index%3!=0:index%2==1;
            var lanes=multi?new[]{lane,lane%3+1}:new int[0];
            if(script.variant=="B"){lane=4-lane;for(int i=0;i<lanes.Length;i++)lanes[i]=4-lanes[i];}
            events.Add(new WaveEvent{beat=beat,type=WaveEventType.Soldier,lane=lane,advance=0,tag=PacketPrefix+index,lanes=lanes});
        }
        script.events=events.ToArray();script.maxOnScreen=script.chapter>=7?16:script.chapter>=4?14:12;
        script.maxEventsPerBeat=3;script.laneCooldown=1;
    }
}
