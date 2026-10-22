using System;
using System.Collections.Generic;

public enum CampaignPhase { Title, Transition, Preparation, Defense, Choice, Epilogue, LastChance, Results, Finished, EndCard, Calibration, Cinematic }
public enum CampaignEnding { None, Mourning, Hope }
public enum JavertDecision { Unchosen, Released, Executed }
public static class RunState
{
    public static CampaignEnding Ending;
    public static bool HopeMusicStarted;
    public static int EndingSourceChapter;
    public static string PlayerName="";
    public static bool ProtagonistIntroduced;
    public static int Chapter=1, ChapterStartHP, BuiltHP, Score, Perfects, Attempts, TotalHeartbeats, AmmoBonus;
    public static int EscortSuccesses, EscortFailures,ChapterStartEscortSuccesses,ChapterStartEscortFailures,ChapterStartAmmoBonus;
    public static float ChapterStartReloadMultiplier=1;
    public static float ReloadMultiplier=1;
    public static bool SpySpared=true;
    public static JavertDecision Decision;
    public static readonly List<int> Judgements=new List<int>();
    public static readonly List<string> Memories=new List<string>();
    public static int Mistakes=>Attempts-Perfects;
    // Three appearances are one child's complete rescue; grant one capacity only at 3/3, without failures.
    public static bool RegisterEscortResult(bool survived)
    {
        int before=AmmoBonus;
        if(survived)EscortSuccesses++;else EscortFailures++;
        if(Chapter==4&&EscortSuccesses-ChapterStartEscortSuccesses==3&&EscortFailures==ChapterStartEscortFailures)
            AmmoBonus=1;
        ReloadMultiplier=EscortFailures>0?Math.Min(2f,(float)Math.Pow(1.5,EscortFailures)):EscortSuccesses>=3?.8f:1;
        return AmmoBonus>before;
    }
    public static void NewCampaign(){Ending=CampaignEnding.None;HopeMusicStarted=false;EndingSourceChapter=0;PlayerName="";ProtagonistIntroduced=false;AmmoBonus=ChapterStartAmmoBonus=0;EscortSuccesses=EscortFailures=ChapterStartEscortSuccesses=ChapterStartEscortFailures=0;ReloadMultiplier=ChapterStartReloadMultiplier=1;Chapter=1;ChapterStartHP=0;TotalHeartbeats=0;Decision=JavertDecision.Unchosen;SpySpared=true;Memories.Clear();ResetChapter();}
    public static void EnterChapter(bool retry)
    {
        if(retry){AmmoBonus=ChapterStartAmmoBonus;EscortSuccesses=ChapterStartEscortSuccesses;EscortFailures=ChapterStartEscortFailures;ReloadMultiplier=ChapterStartReloadMultiplier;}
        else{ChapterStartAmmoBonus=AmmoBonus;ChapterStartEscortSuccesses=EscortSuccesses;ChapterStartEscortFailures=EscortFailures;ChapterStartReloadMultiplier=ReloadMultiplier;}
    }
    public static void ResetChapter()
    {BuiltHP=Score=Perfects=Attempts=0;Judgements.Clear();}
    public static bool TryRecordAt(int index,bool perfect)
    {
        while(Judgements.Count<=index)Judgements.Add(0);
        if(Judgements[index]!=0)return false;
        Judgements[index]=perfect?1:-1;Record(perfect);return true;
    }
    public static void Record(bool perfect){Attempts++;if(perfect){Perfects++;Score+=100;}}
}
