using UnityEngine;

// Three volleys: every living grenadier throws together. Health and projectiles belong to individuals.
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
        if (CampaignSession.Suspended || DebugConsole.SimulationPaused) return;
        if(director==null){Destroy(gameObject);return;}
        if(!StoryDirector.CombatRunning||director.Frozen)return;
        bool any=false;
        for(int i=0;i<members.Length;i++)
        {
            var go=members[i];if(go==null||go.GetComponent<EnemyHP>().HP<=0)continue;
            any=true;var move=go.GetComponent<EnemyMove>();
            float stop=director.SquadThrowX(lane)-i*.9f;
            go.GetComponent<Rigidbody2D>().velocity=round>=director.Script.squadImpactOffsets.Length||go.transform.position.x<stop?Vector2.right*move.speed:Vector2.zero;
        }
        if(!any){Destroy(gameObject);return;}
        var offsets=director.Script.squadImpactOffsets;
        if(round>=Mathf.Min(3,offsets.Length))return; // Survivors march onward; completing a volley never kills them.
        int impact=director.ImpactBeat(beat+offsets[round]);
        double launch=impact-3; // Three reaction beats for three independent grenades.
        if(!animationStarted&&director.Position>=launch-EnemyVisualAnimation.ThrowReleaseDelay/director.Period)
        {
            animationStarted=true;
            foreach(var shooter in members)if(shooter!=null&&shooter.GetComponent<EnemyHP>().HP>0)shooter.GetComponent<EnemyMove>().BeginWaveThrow();
        }
        if(director.Position<launch)return;
        int throwing=0;
        for(int i=0;i<members.Length;i++)
            if(members[i]!=null&&members[i].GetComponent<EnemyHP>().HP>0)
            {members[i].GetComponent<EnemyMove>().ThrowWaveBomb(director,lane,impact,tagName+"-volley"+(round+1)+"-member"+(i+1));throwing++;}
        CampaignTelemetry.Current?.Record("grenadier_volley",$"round={round+1};alive={throwing};impact={impact}");
        round++;animationStarted=false;
    }
}
