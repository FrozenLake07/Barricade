using UnityEngine;

public sealed partial class WaveDirector
{
    public const string NightmareSquadTag="nightmare-extra-squad";
    bool nightmareSquadSpawned;
    int nightmareSquadBeat,nightmareSquadLane;
    public bool NightmareSquadSpawned=>nightmareSquadSpawned;
    bool NightmareSquadPending=>GameOptions.Difficulty==GameDifficulty.Nightmare&&!nightmareSquadSpawned&&Script.chapter>=3&&Script.chapter<=9;
    void ConfigureNightmarePlan()
    {
        nightmareSquadBeat=GameDifficultyRules.ExtraSquadBeat(Script);
        nightmareSquadLane=GameDifficultyRules.ExtraSquadLane(Script);
    }
    void TryNightmareSquad()
    {
        if(!NightmareSquadPending||FinaleStarted||Frozen||!StoryDirector.CombatRunning||Position<nightmareSquadBeat)return;
        if(eventsThisBeat>=Script.maxEventsPerBeat||ActiveCount()+3>DebugCap||CampaignActor.ActiveEscort!=null)return;
        if(laneLast.TryGetValue(nightmareSquadLane,out int last)&&Position-last<Script.laneCooldown)return;
        if(!GameDifficultyRules.ExtraSquadWindowClear(Script,Position))return;
        nightmareSquadSpawned=true;eventsThisBeat++;
        laneLast[nightmareSquadLane]=Mathf.FloorToInt((float)Position);
        var e=new WaveEvent{beat=nightmareSquadBeat,type=WaveEventType.Squad,lane=nightmareSquadLane,tag=NightmareSquadTag};
        Execute(new Pending{source=e,beat=e.beat,alignTo=-1,lane=e.lane},new[]{e.lane});
        CampaignTelemetry.Current?.Record("nightmare_extra_squad","chapter="+Script.chapter+";lane="+e.lane);
        Log(Position.ToString("F1")+" 生成噩梦追加掷弹兵小队");
    }
    int SoldierCopies(string tag)
        =>tag=="opening"?1:GameDifficultyRules.SoldierCopiesAt(++reducedIndex,GameOptions.DensityPercent(Script.chapter));
    int PreviewSoldierCost(string tag,int laneCount,int repeats)
    {
        int index=reducedIndex,total=0,percent=GameOptions.DensityPercent(Script.chapter);
        for(int i=0;i<laneCount*repeats;i++)total+=tag=="opening"?1:GameDifficultyRules.SoldierCopiesAt(++index,percent);
        return total;
    }
}
