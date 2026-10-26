using System;

// 纯时间换算，使用完整周期（等待+Perfect窗口），不依赖帧率。
public static class WaveTiming
{
    public static double Position(int completed,double elapsed,double wait,double window)
    {
        double period=Math.Max(.01,wait+window);
        // n拍定义为第n次判定窗口打开；防守开始为0，首个窗口在wait秒。
        return completed==0&&elapsed<wait?elapsed/Math.Max(.01,wait):completed+1+(elapsed-wait)/period;
    }
    public static double SecondsUntil(double now,double target,double period)
        =>Math.Max(0,(target-now)*Math.Max(.01,period));
    public static double SpawnAt(double arrival,double distance,double speed,double period)
        =>Math.Max(0,arrival-Math.Max(0,distance)/Math.Max(.01,speed)/Math.Max(.01,period));
    public static bool Due(double now,int beat,int alignTo,double distance,double speed,double period)
        =>now+1e-6 >= (alignTo>=0?SpawnAt(alignTo,distance,speed,period):beat);
    public static int EndTarget(int target,int defenseBeats,bool waitForThreats)
        =>waitForThreats?target:Math.Min(defenseBeats,target);
}
