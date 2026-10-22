using System;
using System.Linq;
using UnityEngine;

public enum WaveEventType { Soldier, Squad, Shell, Decoy, Escort, Cannon }

[Serializable]
public sealed class WaveEvent
{
    [Min(0)] public int beat;
    [Tooltip("-1使用生成拍；>=0按到达拍反推生成，并保持到达对齐")]
    public int alignTo = -1;
    public WaveEventType type;
    [Range(1,3)] public int lane = 2;
    [Range(0,10),Tooltip("仅第3章战斗第一波opening允许画内起点；其他敌人固定从左侧画外入场。")] public float advance;
    [Min(1)] public int count = 1;
    [Min(0)] public int spacing = 4;
    [Min(0)] public int duration;
    public string tag;
    [Tooltip("同事件同拍的多轨士兵，覆盖lane/count，例如1,3")]
    public int[] lanes = new int[0];
}

[CreateAssetMenu(menuName="街垒/波次脚本")]
public sealed class WaveScript : ScriptableObject
{
    public int chapter;
    public string variant = "A";
    public bool relativeToDefense=true;
    [Min(1)] public int defenseBeats = 163;
    [Range(1,16)] public int maxOnScreen = 12;
    [Min(1)] public int maxEventsPerBeat = 2;
    [Min(0)] public int laneCooldown = 2;
    [Min(1)] public int shellTravelBeats = 3;
    public float auditDistance=14.88f,auditSoldierSpeed=.5f,auditSquadSeconds=14.3f,auditShellSeconds=1.5f;
    [Min(0)] public int requiredShellInterceptions;
    public int[] squadImpactOffsets = {10,15,20};
    public WaveEvent[] events = new WaveEvent[0];

    public void Validate()
    {
        if(chapter<2||chapter>9||defenseBeats<1||maxOnScreen<1||maxOnScreen>16||!relativeToDefense)
            throw new InvalidOperationException("波次章节、拍数或屏上上限无效："+name);
        if(squadImpactOffsets==null||squadImpactOffsets.Length!=3)throw new InvalidOperationException("小队需要三个命中偏移。");
        foreach(var e in events)
        {
            if(e==null||e.beat<0||e.beat>=defenseBeats||e.alignTo < -1||e.lane<1||e.lane>3||e.advance<0||e.advance>10||e.count<1||e.spacing<0)
                throw new InvalidOperationException("波次事件越界："+name);
            if(e.advance>0&&!(chapter==3&&e.beat==0&&e.tag=="opening"))
                throw new InvalidOperationException("只有首次战斗第一波允许画内生成："+e.tag);
            if(e.alignTo>defenseBeats&&e.type!=WaveEventType.Soldier)throw new InvalidOperationException("到达拍超出防守段："+e.tag);
            foreach(int lane in e.lanes)if(lane<1||lane>3)throw new InvalidOperationException("多轨配置越界："+e.tag);
        }
        foreach(var escort in events.Where(e=>e.type==WaveEventType.Escort))
        {
            int end=escort.beat+escort.duration;
            foreach(var other in events.Where(e=>e!=escort&&(e.type==WaveEventType.Escort||e.type==WaveEventType.Decoy)))
                if(other.beat<end&&other.beat+other.duration>escort.beat)throw new InvalidOperationException("护送窗口中出现第二个保护/诱饵对象："+escort.tag);
            foreach(var squad in events.Where(e=>e.type==WaveEventType.Squad))
            {
                int bombEnd=squad.beat+squadImpactOffsets.Max();
                if(squad.beat>=end||bombEnd<escort.beat)continue;
                if(squad.lane==escort.lane)throw new InvalidOperationException("护送与当拍小队必须分轨："+escort.tag);
                if(!(escort.tag??"").Contains("exam")||!(squad.tag??"").Contains("exam"))throw new InvalidOperationException("护送叠加小队必须显式标注exam："+escort.tag);
            }
            if(!events.Any(e=>e.type==WaveEventType.Soldier&&(e.lanes.Length>0?e.lanes.Contains(escort.lane):e.lane==escort.lane)&&e.alignTo>=escort.beat&&e.alignTo<=end))
                throw new InvalidOperationException("护送窗口必须有同轨且到达拍在窗口内的士兵（用alignTo明确指定）："+escort.tag);
        }
        var errors=WaveAudit.Errors(this);
        if(errors.Length>0)throw new InvalidOperationException(string.Join("\n",errors));
    }
}
