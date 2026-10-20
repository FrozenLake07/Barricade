using UnityEngine;

// 三人同帧入场，每人一次投弹，总计三枚，命中拍为+10/+15/+20。
public sealed class WaveSquad : MonoBehaviour
{
    WaveDirector director;
    GameObject[] members;
    float[] start;
    int lane,beat,round;
    string tagName;
    bool animationStarted;
    public void Initialize(WaveDirector owner,GameObject[] soldiers,int track,int spawnBeat,string label)
    {
        director=owner;members=soldiers;lane=track;beat=spawnBeat;tagName=label;
        start=new float[members.Length];for(int i=0;i<members.Length;i++)start[i]=members[i].transform.position.x;
    }
    void Update()
    {
        if (CampaignSession.Suspended) return;
        if(director==null){Destroy(gameObject);return;}
        if(!StoryDirector.CombatRunning||director.Frozen)return;
        bool any=false;
        for(int i=0;i<members.Length;i++)
        {
            var go=members[i];if(go==null||go.GetComponent<EnemyHP>().HP<=0)continue;
            any=true;var move=go.GetComponent<EnemyMove>();
            go.GetComponent<Rigidbody2D>().velocity=go.transform.position.x-start[i]<move.leaderAdvanceDistance?Vector2.right*move.speed:Vector2.zero;
        }
        if(!any){Destroy(gameObject);return;}
        var offsets=director.Script.squadImpactOffsets;
        if(round>=Mathf.Min(3,offsets.Length))
        {
            foreach(var go in members)if(go!=null)Destroy(go);
            Destroy(gameObject);return;
        }
        int impact=director.ImpactBeat(beat+offsets[round]);
        double launch=impact-2;
        var shooter=round<members.Length?members[round]:null;
        if(!animationStarted&&director.Position>=launch-EnemyVisualAnimation.ThrowReleaseDelay/director.Period)
        {animationStarted=true;if(shooter!=null)shooter.GetComponent<EnemyMove>().BeginWaveThrow();}
        if(director.Position<launch)return;
        if(shooter!=null&&shooter.GetComponent<EnemyHP>().HP>0)shooter.GetComponent<EnemyMove>().ThrowWaveBomb(director,lane,impact,tagName+"-"+(round+1));
        round++;animationStarted=false;
    }
    void OnDestroy(){if(members!=null)foreach(var go in members)if(go!=null)Destroy(go);}
}
