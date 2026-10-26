using System;

// Independent of the campaign clock: these three cycles never raise scoring/wave events.
public sealed class ResumeCountIn
{
    public const int Beats = 3;
    public double Period {get;private set;}
    public double Elapsed {get;private set;}
    public bool Finished => Elapsed >= Period * Beats;
    public int Remaining => Finished ? 0 : Beats - Math.Min(Beats-1,(int)(Elapsed/Period));
    public float Phase => Finished ? 0f : (float)((Elapsed%Period)/Period);
    public void Reset(double period)
    {Period=double.IsNaN(period)||double.IsInfinity(period)?1.35:Math.Max(.1,period);Elapsed=0;}
    public void Advance(double delta)
    {if(!double.IsNaN(delta)&&!double.IsInfinity(delta)&&delta>0)Elapsed=Math.Min(Period*Beats,Elapsed+delta);}
}
