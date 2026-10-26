using System;
using System.Collections;
using UnityEngine;

public sealed partial class StoryDirector
{
    public bool FinalBarrageActive {get;private set;}
    public int FinalBarrageShots {get;private set;}
    public const float FinalBarrageInterval=.65f;
    IEnumerator FinalArtilleryBarrage()
    {
        FinalBarrageActive=true;FinalBarrageShots=0;StopCampaignMusic();
        ShowGameplayHint("火炮集中轰击！街垒即将失守。");
        telemetry.Record("final_barrage_started","health="+GameManager.WallHP);
        var cannon=WaveDirector.Current?.PrepareFinalBarrageCannon();
        try
        {
            if(cannon==null)throw new InvalidOperationException("终幕炮击缺少有效火炮资源。");
            while(FinalBarrageShots<4||GameManager.WallHP>0)
            {
                if(CampaignSession.Suspended||DebugConsole.SimulationPaused){yield return null;continue;}
                if(cannon.FireFinalBarrage())FinalBarrageShots++;
                yield return FinalBarrageDelay(FinalBarrageInterval);
            }
            yield return FinalBarrageDelay(.7f);
            telemetry.Record("final_barrage_completed","shots="+FinalBarrageShots+";health="+GameManager.WallHP);
        }
        finally{FinalBarrageActive=false;}
    }
    IEnumerator FinalBarrageDelay(float seconds)
    {
        float age=0;
        while(age<seconds)
        {
            if(!CampaignSession.Suspended&&!DebugConsole.SimulationPaused)age+=Time.deltaTime;
            yield return null;
        }
    }
}
