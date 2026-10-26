using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BarricadeHealthVisual : MonoBehaviour
{
    [Header("生命从低到高的三阶段")]
    public Sprite firstStage,secondStage,thirdStage;
    [Range(0,100)] public int secondStageAtPercent=33;
    [Range(0,100)] public int thirdStageAtPercent=66;
    SpriteRenderer target;
    int shown=-1;

    void Awake()
    {
        target=GetComponent<SpriteRenderer>();
        if(thirdStage==null)thirdStage=target.sprite;
    }
    void OnEnable(){if(Application.isPlaying)RefreshNow();}
    void LateUpdate(){if(Application.isPlaying)RefreshNow();}
    void OnValidate(){if(Application.isPlaying)RefreshNow();}

    // 整数交叉相乘，避免198/600在float阈值附近误落到33%以下。
    public static int SelectStage(int hp,int maximum,int secondPercent=33,int thirdPercent=66)
    {
        if(maximum<=0)return 0;
        long value=(long)hp*100;
        if(value>=(long)maximum*thirdPercent)return 2;
        return value>=(long)maximum*secondPercent?1:0;
    }
    public void Configure(Sprite first,Sprite second,Sprite third)
    {
        firstStage=first;secondStage=second;thirdStage=third;shown=-1;
        target=GetComponent<SpriteRenderer>();
        if(Application.isPlaying)RefreshNow();else target.sprite=thirdStage;
    }
    [ContextMenu("刷新街垒生命阶段")]
    public void RefreshNow()
    {
        if(target==null)target=GetComponent<SpriteRenderer>();
        if(target==null)return;
        int stage=SelectStage(GameManager.WallHP,GameManager.WallMaxHP,secondStageAtPercent,thirdStageAtPercent);
        Sprite sprite=stage==0?firstStage:stage==1?secondStage:thirdStage;
        if(sprite==null)sprite=thirdStage;
        if(sprite!=null&&(stage!=shown||target.sprite!=sprite))target.sprite=sprite;
        shown=stage;
    }
}
