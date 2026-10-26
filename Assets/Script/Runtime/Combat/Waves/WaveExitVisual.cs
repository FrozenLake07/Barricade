using UnityEngine;

// 复用现有精灵作停步、转身和淡出；不凭空删除仍存活的敌人。
public sealed class WaveExitVisual : MonoBehaviour
{
    float elapsed,duration;
    SpriteRenderer[] renderers;
    Color[] colors;
    public void Begin(float seconds)
    {
        duration=Mathf.Max(.2f,seconds);
        var move=GetComponent<EnemyMove>();if(move!=null)move.enabled=false;
        var cannon=GetComponent<Cannon>();if(cannon!=null)cannon.enabled=false;
        foreach(var body in GetComponentsInChildren<Rigidbody2D>()){body.velocity=Vector2.zero;body.simulated=false;}
        foreach(var c in GetComponentsInChildren<Collider2D>())c.enabled=false;
        foreach(var animator in GetComponentsInChildren<Animator>())animator.enabled=false;
        var driver=GetComponent<EnemyVisualAnimation>();if(driver!=null)driver.enabled=false;
        transform.localScale=new Vector3(-transform.localScale.x,transform.localScale.y,transform.localScale.z);
        renderers=GetComponentsInChildren<SpriteRenderer>(true);colors=new Color[renderers.Length];
        for(int i=0;i<colors.Length;i++)colors[i]=renderers[i].color;
    }
    void Update()
    {
        if (CampaignSession.Suspended) return;
        elapsed+=Time.deltaTime;
        for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)
        {Color c=elapsed<.08f?Color.white:colors[i];c.a*=1-Mathf.Clamp01(elapsed/duration);renderers[i].color=c;}
        if(elapsed>=duration)Destroy(gameObject);
    }
}
