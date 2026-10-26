using System;
using UnityEngine;

// 仅alignTo与编排弹道修正到达；普通士兵仍按原始speed走，不按估算拍长硬编码。
public sealed class WaveThreat : MonoBehaviour
{
    WaveDirector director;
    WaveEventType type;
    Rigidbody2D body;
    Vector2 origin;
    double born, arrival;
    double lastPosition;
    bool motionClockReady;
    int lane;
    string tagName;
    bool handled;
    public bool IsPending => !handled&&isActiveAndEnabled;
    public bool Handled => handled;
    public string Description
    {
        get
        {
            double at=arrival>=0?arrival:director.Position+Math.Max(0,director.WallX(lane)-transform.position.x)/Math.Max(.01,GetComponent<EnemyMove>()?.speed??.5f)/director.Period;
            return $"{tagName} 轨{lane} → {at:F1}"+(at<director.Position-.5?" !到期未结算":"");
        }
    }
    public void Initialize(WaveDirector owner,WaveEventType kind,int track,double target,string label)
    {
        director=owner;type=kind;lane=track;arrival=target;tagName=label;
        body=GetComponent<Rigidbody2D>();origin=transform.position;born=owner.Position;lastPosition=born;motionClockReady=true;
        if(arrival>=0&&body!=null)body.gravityScale=0;
    }
    public void RecordImpact()
    {
        if(handled)return;handled=true;
        if(director!=null)director.Arrival(tagName,lane,arrival);
    }
    void FixedUpdate()
    {
        if(handled||director==null||body==null)return;
        double now=director.Position,delta=motionClockReady?System.Math.Max(0,now-lastPosition):0;lastPosition=now;motionClockReady=true;
        var enemy=GetComponent<EnemyHP>();if(enemy!=null&&enemy.HP<=0){body.velocity=Vector2.zero;return;}
        if(!StoryDirector.CombatRunning){body.velocity=Vector2.zero;return;}
        if(director.Frozen){body.velocity=Vector2.zero;return;}
        if(type==WaveEventType.Soldier&&GetComponent<EnemyMove>()?.HitPaused==true)
        {
            if(arrival>=0)arrival+=delta;
            body.velocity=Vector2.zero;return;
        }
        if(arrival<0)return;
        if(now>=arrival)
        {
            if(type==WaveEventType.Soldier)GetComponent<WallDamage>()?.ResolveWallHit();
            else if(type==WaveEventType.Squad)GetComponent<BombHP>()?.Explode(true);
            else if(type==WaveEventType.Shell)GetComponent<CannonShell>()?.ResolveWaveImpact();
            return;
        }
        float x=director.WallX(lane)-.12f;
        float y=type==WaveEventType.Soldier?origin.y:director.LaneY(lane);
        if(type==WaveEventType.Soldier)
        {
            if(GetComponent<EnemyMove>()?.HoldingFire==true){body.velocity=Vector2.zero;return;}
            double remaining=WaveTiming.SecondsUntil(now,arrival,director.Period);
            body.velocity=new Vector2(Mathf.Max(0,(x-body.position.x)/(float)Math.Max(Time.fixedDeltaTime,remaining)),0);
        }
        else
        {
            float progress=Mathf.Clamp01((float)((now-born)/Math.Max(.01,arrival-born)));
            // 炮弹直飞；手榴弹保留可识别的低抛弧线。
            Vector2 point=Vector2.Lerp(origin,new Vector2(x,y),progress);
            if(type==WaveEventType.Squad)point.y+=Mathf.Sin(progress*Mathf.PI)*1.8f;
            body.velocity=(point-body.position)/Mathf.Max(.001f,Time.fixedDeltaTime);
            if(type==WaveEventType.Shell&&body.velocity.sqrMagnitude>.01f)transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(body.velocity.y,body.velocity.x)*Mathf.Rad2Deg);
        }
    }
    void OnDestroy()
    {
        if(!handled&&director!=null&&StoryDirector.CombatRunning)director.Log($"{director.Position:F1} 已拦截/消除 {tagName}");
    }
}
