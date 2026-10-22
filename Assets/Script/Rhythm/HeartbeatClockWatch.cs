using System;

// Count observed running time, not the age of the application or scene.
public sealed class HeartbeatClockWatch
{
    float previous=float.NaN,stillSeconds;
    bool reported;
    public void Reset(){previous=float.NaN;stillSeconds=0;reported=false;}
    public bool Observe(float beat,float delta,bool running)
    {
        if(!running){Reset();return false;}
        if(float.IsNaN(previous)||Math.Abs(beat-previous)>.00001f)
        {previous=beat;stillSeconds=0;reported=false;return false;}
        if(delta>0&&!float.IsNaN(delta)&&!float.IsInfinity(delta))stillSeconds+=delta;
        if(stillSeconds<5||reported)return false;
        reported=true;return true;
    }
}
