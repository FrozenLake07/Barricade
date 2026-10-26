using System;
using System.Linq;
using UnityEngine;

public sealed partial class WaveDirector
{
    public string DebugInfo()=>$"第{Script.chapter}章 {Script.variant}  拍{Position:F2}/{Script.defenseBeats}  待生成{pending.Count(p=>!p.done)}  屏上{ActiveCount()}/{DebugCap}\n"+
        string.Join("\n",pending.Where(p=>!p.done).Take(6).Select(p=>$"{p.source.type} 轨{p.lane} 生成{p.beat} 对齐{p.alignTo}"));
    int DebugCap=>CampaignDebug.Value.cap>0?CampaignDebug.Value.cap:Mathf.CeilToInt(Script.maxOnScreen*(GameOptions.Difficulty==GameDifficulty.Nightmare?1.25f:1f));
    public void DebugOverlay(bool visible){debug=visible;if(debugCanvas==null&&visible)CreateDebugUI();if(debugCanvas!=null)debugCanvas.SetActive(visible);}
    public void DebugFinale(){if(FinaleStarted)throw new InvalidOperationException("已进入章末收尾。");FinalVolley();}
    public void DebugSpawn(WaveEventType type,int lane,int count,int arrival)
    {
        if(FinaleStarted)throw new InvalidOperationException("章末收尾中无法新增敌人。");
        if(lane<1||lane>3||count<1||count>16)throw new ArgumentException("轨道1—3，数量1—16。");
        if(arrival>=0&&type!=WaveEventType.Soldier&&type!=WaveEventType.Shell&&type!=WaveEventType.Squad)throw new ArgumentException("at 支持 soldier（到墙拍）、shell（命中拍）、squad（首次齐投命中拍）。");
        if(arrival>=0&&arrival<Position+2)throw new ArgumentException("到达拍至少晚于当前2拍。");
        if(type==WaveEventType.Escort&&(count!=1||CampaignActor.ActiveEscort!=null))throw new InvalidOperationException("一次只能存在一名护送对象。");
        int cost=type==WaveEventType.Squad?count*3:type==WaveEventType.Shell?0:count;
        if(type==WaveEventType.Soldier&&GameOptions.Difficulty==GameDifficulty.Nightmare)cost=PreviewSoldierCost("debug",1,count);
        if(ActiveCount()+cost>DebugCap)throw new InvalidOperationException("超过屏上上限，请先 kill 或调整 cap。");
        for(int i=0;i<count;i++)
        {
            int now=(int)Math.Floor(Position);
            if(type==WaveEventType.Shell)
            {
                var prefab=Resources.Load<CannonShell>("arrow/CannonShell");
                if(prefab==null)throw new InvalidOperationException("缺少arrow/CannonShell。");
                int impact=arrival>=0?arrival:now+Script.shellTravelBeats;
                float seconds=(float)WaveTiming.SecondsUntil(Position,impact,Period);
                float x=spawns[lane-1].WaveEntryX(false,0),y=LaneY(lane);
                var shell=Instantiate(prefab,new Vector3(x,y,0),Quaternion.identity);
                shell.lifetime=Math.Max(shell.lifetime,seconds+2);
                shell.Launch(Vector2.right,Math.Max(1,(WallX(lane)-x)/Math.Max(.1f,seconds)),prefab.wallDamageFraction,y);
                shell.gameObject.AddComponent<WaveThreat>().Initialize(this,type,lane,impact,"debug");
                continue;
            }
            var e=new WaveEvent{type=type,lane=lane,beat=now,count=1,advance=0,duration=8,tag="debug",alignTo=arrival};
            // A squad's first throw must happen after walking in from outside the screen.
            if(type==WaveEventType.Squad)
            {
                int earliest=now+(int)Math.Ceiling(Math.Max(0,(SquadThrowX(lane)-spawns[lane-1].WaveEntryX(false,0))/spawns[lane-1].WaveSpeed/Period))+3;
                if(arrival>=0&&arrival-Script.squadImpactOffsets.Min()<earliest)throw new ArgumentException("到达拍不足以让小队从画外步行并完成投弹；请延后at。");
                e.beat=arrival>=0?arrival-Script.squadImpactOffsets.Min():earliest;
            }
            if(type==WaveEventType.Shell&&arrival>=0)e.beat=arrival-Script.shellTravelBeats;
            var p=new Pending{source=e,lane=lane,beat=e.beat,alignTo=arrival};
            Execute(p,new[]{lane});
        }
    }
}
