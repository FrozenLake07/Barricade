using System;

public enum ConstructionResult { None, Ordinary, Perfect, Wrong, Miss, Grace }

// 输入时间与按键边沿由调用方提供，测试可直接覆盖跨帧双键与窗口结束。
public sealed class ConstructionJudgement
{
    public int First {get;private set;}
    public int Second {get;private set;}
    public bool Chord=>Second>=0;
    public bool Resolved {get;private set;}
    bool firstSeen,secondSeen,firstPerfect,secondPerfect;
    double firstTime,secondTime;
    double maxGap;
    public const double PromptGraceSeconds=.18;
    bool hasPrompt;
    int previousMask;
    double promptGraceUntil=double.NegativeInfinity;
    public void Reset(int first,int second,double gap,double changedAt=double.NegativeInfinity)
    {
        previousMask=hasPrompt?(1<<First)|(Second>=0?1<<Second:0):0;
        promptGraceUntil=changedAt+PromptGraceSeconds;hasPrompt=true;
        First=first;Second=second;maxGap=gap;Resolved=firstSeen=secondSeen=false;
    }
    public ConstructionResult Press(int key,double time,bool perfectWindow,bool allowPromptGrace=true)
    {
        if(Resolved)return ConstructionResult.None;
        if(key!=First&&key!=Second)
        {
            // A protected old-prompt edge produces feedback without scoring or consuming this prompt.
            if(allowPromptGrace&&time<=promptGraceUntil&&(previousMask&(1<<key))!=0)return ConstructionResult.Grace;
            Resolved=true;return ConstructionResult.Wrong;
        }
        if(key==First&&!firstSeen){firstSeen=true;firstTime=time;firstPerfect=perfectWindow;}
        if(key==Second&&!secondSeen){secondSeen=true;secondTime=time;secondPerfect=perfectWindow;}
        if(!firstSeen||(Chord&&!secondSeen))return ConstructionResult.None;
        Resolved=true;
        return firstPerfect&&(!Chord||(secondPerfect&&Math.Abs(firstTime-secondTime)<=maxGap+1e-7))?ConstructionResult.Perfect:ConstructionResult.Ordinary;
    }
    public ConstructionResult Close()
    {
        if(Resolved)return ConstructionResult.None;
        Resolved=true;
        return (firstSeen&&firstPerfect)||(secondSeen&&secondPerfect)?ConstructionResult.Wrong:ConstructionResult.Miss;
    }
    public static bool IsChord(int chapter,int beat)
        =>chapter==2?beat>=49&&(beat-49)%12==0:beat%8==0;
    public static void Prompt(int chapter,int beat,out int first,out int second)
    {
        first=(beat*7+chapter)%4;second=-1;
        if(!IsChord(chapter,beat))return;
        int index=(chapter+(chapter==2?(beat-49)/12:beat/8))%4;
        first=index/2;second=2+index%2;
    }
    public static double RewardScale(int chapter,int beats,int limit,int chordPerfect)
    {
        // Construction and repair always use the same per-input reward.
        return 1;
    }
}
