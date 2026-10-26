using System;
using System.Globalization;

// Pure sequencing: naming is an input gate, never a timed cinematic or a heartbeat budget.
public sealed class CinematicPlaybackState
{
    readonly double[] durations;
    readonly int nameGate;
    bool nameResolved;
    public int Index {get;private set;}
    public double Age {get;private set;}
    public bool AwaitingName {get;private set;}
    public bool Finished {get;private set;}
    public CinematicPlaybackState(double[] seconds,int gate=-1)
    {
        if(seconds==null||seconds.Length==0||gate< -1||gate>=seconds.Length-1)throw new ArgumentException("Invalid cinematic sequence.");
        durations=(double[])seconds.Clone();nameGate=gate;
        foreach(double duration in durations)if(double.IsNaN(duration)||double.IsInfinity(duration)||duration<=0)throw new ArgumentException("Invalid shot duration.");
    }
    public void Advance(double delta)
    {
        if(Finished||AwaitingName||double.IsNaN(delta)||double.IsInfinity(delta)||delta<=0)return;
        Age+=delta;
        while(Age>=durations[Index])
        {
            if(Index==nameGate&&!nameResolved){Age=durations[Index];AwaitingName=true;return;}
            Age-=durations[Index];
            if(++Index>=durations.Length){Index=durations.Length-1;Age=durations[Index];Finished=true;return;}
        }
    }
    public void ConfirmName()
    {
        if(!AwaitingName)return;
        nameResolved=true;AwaitingName=false;Age=0;Index++;
    }
    public void Skip()
    {
        if(Finished||AwaitingName)return;
        if(nameGate>=0&&!nameResolved){Index=nameGate;Age=durations[Index];AwaitingName=true;}
        else Finished=true;
    }
}

public static class ProtagonistNameRules
{
    public static bool TryNormalize(string input,out string name)
    {
        foreach(char character in input??"")
            if(char.IsControl(character)||char.GetUnicodeCategory(character)==UnicodeCategory.LineSeparator
                ||char.GetUnicodeCategory(character)==UnicodeCategory.ParagraphSeparator){name="";return false;}
        name=(input??"").Trim();
        if(name.Length==0)return false;
        for(int i=0;i<name.Length;i++)
        {
            if(char.IsControl(name[i])||char.GetUnicodeCategory(name[i])==UnicodeCategory.LineSeparator
                ||char.GetUnicodeCategory(name[i])==UnicodeCategory.ParagraphSeparator)return false;
            if(char.IsHighSurrogate(name[i])){if(i+1>=name.Length||!char.IsLowSurrogate(name[++i]))return false;}
            else if(char.IsLowSurrogate(name[i]))return false;
        }
        var elements=StringInfo.GetTextElementEnumerator(name);int count=0;
        while(elements.MoveNext())if(++count>12)return false;
        return count>0;
    }
    public static string Format(string template,string name)
    {
        if(string.IsNullOrEmpty(template))return "";
        if(template.Contains("{playerName}")&&string.IsNullOrEmpty(name))return "";
        return template.Replace("{playerName}",name??"");
    }
}
