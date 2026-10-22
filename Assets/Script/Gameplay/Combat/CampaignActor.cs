using UnityEngine;

// 护送对象自身不带EnemyHP，玩家弹丸无法选中或伤害它；只检查同轨士兵。
public sealed class CampaignActor : MonoBehaviour
{
    public bool escort;
    public int lane;
    public float duration=8f;
    float elapsed,startX;
    bool resolved;
    public bool IsResolved => resolved;
    WaveDirector wave;
    float waveBorn;
    int waveDuration;
    public static CampaignActor ActiveEscort;
    public void ConfigureWave(WaveDirector owner,float born,int durationBeats)
    {wave=owner;waveBorn=born;waveDuration=durationBeats;if(escort)ActiveEscort=this;}
    void Start(){startX=transform.position.x;if(escort)ActiveEscort=this;}
    void Update()
    {
        if(resolved||!StoryDirector.CombatRunning||DebugConsole.SimulationPaused)return;
        if(wave!=null)
        {
            if(wave.Frozen)return;
            float progress=Mathf.Clamp01(((float)wave.Position-waveBorn)/Mathf.Max(1,waveDuration));
            if(!escort){if(progress>=1)Destroy(gameObject);return;}
            elapsed=progress*duration;
        }
        else {if(!escort)return;elapsed+=Time.deltaTime;}
        transform.position=new Vector3(startX-8f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(elapsed/duration)),transform.position.y,0);
        // 也覆盖碰撞体生于重叠区、快速走位等情况，不依赖单次Enter回调。
        foreach(var hp in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))
            if(IsThreat(hp)&&Mathf.Abs(hp.transform.position.x-transform.position.x)<.6f){ResolveEscort(false);return;}
        if(elapsed>=duration)ResolveEscort(true);
    }
    bool IsThreat(EnemyHP hp)
    {
        if(hp==null||hp.HP<=0||Mathf.Abs(hp.transform.position.y-transform.position.y)>1.25f)return false;
        var move=hp.GetComponent<EnemyMove>();
        return move!=null&&move.role==EnemyMove.Role.Soldier;
    }
    void ResolveEscort(bool survived)
    {
        if(resolved)return;resolved=true;
        bool capacityAwarded=RunState.RegisterEscortResult(survived);
        if(survived)
        {
            if(capacityAwarded)
            {
                CampaignTelemetry.Current?.Record("ammo_capacity_reward","complete_child_rescue;capacity="+(3+RunState.AmmoBonus));
                StoryDirector.Current?.ShowGameplayHint("完整护送成功 · 携弹上限 +1（现为 "+(3+RunState.AmmoBonus)+" 发）");
            }
            else StoryDirector.Current?.ShowGameplayHint("孩子本次已安全返回。");
            Destroy(gameObject);
        }
        else
        {
            GetComponent<SpriteRenderer>().color=Color.white;SfxPlayer.Play("SFX-ENM-02",.45f);
            StoryDirector.Current?.ShowGameplayHint("本次护送未完成，完整救援未达成。");
            Destroy(gameObject,.15f);
        }
    }
    public void HitByPlayer()
    {
        if(escort||resolved)return;
        resolved=true;var hp=GetComponent<EnemyHP>();if(hp!=null)hp.HP=0;RunState.SpySpared=false;RunState.Score+=500;StoryDirector.Current?.ShowGameplayHint("+500");Destroy(gameObject,.1f);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if(!escort||resolved||!StoryDirector.CombatRunning)return;
        if(IsThreat(other.GetComponent<EnemyHP>()))ResolveEscort(false);
        // 玩家子弹、敌弹、手榴弹和炮弹均不结算护送伤害，也不吞掉这些弹丸。
    }
    void OnDestroy(){if(ActiveEscort==this)ActiveEscort=null;}
}
