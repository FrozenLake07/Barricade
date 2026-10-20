using System;
using System.Collections.Generic;

public enum CampaignPhase { Title, Transition, Preparation, Defense, Choice, Epilogue, LastChance, Results, Finished, EndCard, Calibration }
public enum JavertDecision { Unchosen, Released, Executed }
public static class RunState
{
    public static int Chapter=1, BuiltHP, Score, Perfects, Attempts, TotalHeartbeats, AmmoBonus;
    public static int EscortSuccesses, EscortFailures;
    public static float ReloadMultiplier=1;
    public static bool SpySpared=true;
    public static JavertDecision Decision;
    public static readonly List<int> Judgements=new List<int>();
    public static readonly List<string> Memories=new List<string>();
    public static int Mistakes=>Attempts-Perfects;
    public static void RegisterEscortResult(bool survived)
    {
        if(survived)EscortSuccesses++;else EscortFailures++;
        ReloadMultiplier=EscortFailures>0?Math.Min(2f,(float)Math.Pow(1.5,EscortFailures)):EscortSuccesses>=3?.8f:1;
    }
    public static void NewCampaign(){Chapter=1;TotalHeartbeats=0;Decision=JavertDecision.Unchosen;SpySpared=true;Memories.Clear();ResetChapter();}
    public static void ResetChapter()
    {BuiltHP=Score=Perfects=Attempts=AmmoBonus=EscortSuccesses=EscortFailures=0;ReloadMultiplier=1;Judgements.Clear();}
    public static bool TryRecordAt(int index,bool perfect)
    {
        while(Judgements.Count<=index)Judgements.Add(0);
        if(Judgements[index]!=0)return false;
        Judgements[index]=perfect?1:-1;Record(perfect);return true;
    }
    public static void Record(bool perfect){Attempts++;if(perfect){Perfects++;Score+=100;}}
}
