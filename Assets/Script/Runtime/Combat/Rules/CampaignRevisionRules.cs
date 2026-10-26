using System;

// Baseline budgets for the editable chapter table and missing-asset fallback.
public static class CampaignRevisionRules
{
    public const int ChapterBeatBudget = 1832;
    public const int FirstChapterBeats = 32;
    public static int PreparationBeats(int chapter)
    {
        switch(chapter)
        {
            case 2:return 127;
            case 3:case 4:return 48;
            case 5:return 48;
            case 6:return 48;
            case 7:return 41;
            case 8:return 48;
            case 9:return 48;
            default:return 0;
        }
    }
    public static int DefenseBeats(int chapter)=>chapter==7?135:chapter>=3&&chapter<=9?171:0;
    public static float CombatDamageMultiplier(int chapter)
    {
        switch(chapter){case 3:return 2.5f;case 4:return 3f;case 5:return 2f;case 6:return 2.2f;case 7:return 3f;default:return 1f;}
    }
    public static int ScaleCombatDamage(int amount,float multiplier)=>Math.Max(0,(int)Math.Ceiling(Math.Max(0,amount)*multiplier));
    public static int ClampHealth(int health,int limit)=>Math.Max(0,Math.Min(limit,health));
    public static int EnterChapterHealth(int health,int limit,bool retry,int checkpoint)
        =>ClampHealth(retry?checkpoint:health,limit);
    public static int ConstructionReward(bool chord,bool perfect,int chordPerfect,int chordOrdinary)
        =>chord?(perfect?chordPerfect:chordOrdinary):(perfect?10:5);
    public static int FrameAt(int completed,int[] durations)
    {
        if(durations==null||durations.Length==0)return -1;
        int end=0;
        for(int i=0;i<durations.Length;i++){end+=durations[i];if(completed<end)return i;}
        return durations.Length-1;
    }
}
