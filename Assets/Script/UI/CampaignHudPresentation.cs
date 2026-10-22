using System;

// Player-facing text has no dependency on the internal beat counters or wall HP numbers.
public static class CampaignHudPresentation
{
    static readonly string[] ChapterNumerals={"一","二","三","四","五","六","七","八","九","十"};
    public static string Announcement(int chapter,string title)=>chapter==10?title:"第"+(chapter>=1&&chapter<=10?ChapterNumerals[chapter-1]:chapter.ToString())+"章\n"+title;
    public static string Heading(int chapter,string title)=>chapter==10?title:"第"+chapter+"章  "+title;
    public static string Results(int perfect,int judgements,int mistakes,int score,bool failed)
        =>"合拍率 "+(perfect*100f/Math.Max(1,judgements)).ToString("F0")+"%   失误 "+mistakes+"   分数 "+score+"\n\n"+(failed?"R：重开本章":"Enter：继续下一章");
    public static bool VitalsVisible(CampaignPhase phase)=>phase==CampaignPhase.Preparation||phase==CampaignPhase.Defense
        ||phase==CampaignPhase.Epilogue||phase==CampaignPhase.LastChance||phase==CampaignPhase.Choice;
    public static bool AmmoVisible(CampaignPhase phase)=>phase==CampaignPhase.Defense;
    public static float HealthRatio(int health,int maximum)=>Math.Max(0,Math.Min(1,(float)health/Math.Max(1,maximum)));
}
