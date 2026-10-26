using System;
using System.Linq;
using UnityEngine;

// Difficulty affects density and construction forgiveness, never combat damage.
public static class GameDifficultyRules
{
    public static int DensityPercent(GameDifficulty difficulty,int chapter)
        =>difficulty==GameDifficulty.Nightmare?125:difficulty==GameDifficulty.Hard?100:difficulty==GameDifficulty.Easy&&chapter!=9?50:80;
    public static int ConstructionReward(GameDifficulty difficulty,bool perfect,int reward)
        =>difficulty==GameDifficulty.Nightmare&&!perfect?0:reward;
    public static int SoldierCopiesAt(int ordinal,int percent)
        =>ordinal<=0?0:(ordinal*percent+99)/100-((ordinal-1)*percent+99)/100;
    public static bool ExtraSquadWindowClear(WaveScript script,double start)
    {
        double end=start+script.squadImpactOffsets.Max()+2;
        return !script.events.Any(e=>e.type==WaveEventType.Escort&&e.beat<end&&e.beat+e.duration>start);
    }
    public static int ExtraSquadBeat(WaveScript script)
    {
        int first=Math.Min(4,Math.Max(0,script.defenseBeats-1));
        int last=Math.Max(first,script.defenseBeats-script.squadImpactOffsets.Max()-4);
        int desired=Mathf.Clamp(script.defenseBeats/3,first,last),span=last-first+1;
        for(int offset=0;offset<span;offset++)
        {
            int candidate=first+(desired-first+offset)%span;
            if(ExtraSquadWindowClear(script,candidate))return candidate;
        }
        return desired;
    }
    public static int ExtraSquadLane(WaveScript script)
    {int lane=(script.chapter-3)%3+1;return script.variant=="B"?4-lane:lane;}
}
