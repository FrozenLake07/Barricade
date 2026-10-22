using System;
using UnityEngine;

[Serializable]
public sealed class CinematicShot
{
    public Sprite image;
    [Min(.1f)] public float seconds=4;
    [TextArea,Tooltip("留空不显示；支持 {playerName}。")] public string subtitle="";
    [Tooltip("此镜头开始时触发希望结局音乐；只在结局二生效，仅播放一次。")] public bool triggerHopeFinale;
    public AudioClip voiceOver;
    [Min(0)] public float voiceDelaySeconds;
    [Tooltip("镜头位移，以320×180逻辑像素计；位置取整，不连续缩放。")] public Vector2 panFrom,panTo;
    public float Duration=>Mathf.Max(seconds,voiceOver==null?seconds:voiceDelaySeconds+voiceOver.length+.15f);
}

[Serializable]
public sealed class CinematicRoute
{
    [Range(2,10)] public int chapter;
    [Tooltip("chapter-entry、chapter-exit、before-choice 或 ending。")] public string key="chapter-entry";
    [Tooltip("Unchosen表示通用路线；第七章章末使用Released或Executed。")] public JavertDecision decision;
    [Tooltip("None为通用；终章用Mourning与Hope区分两种结局。")] public CampaignEnding ending;
    public CinematicShot[] shots=new CinematicShot[0];
    [Tooltip("仅第二章章首命名；在此索引的镜头结束后停留，确认后进入下一镜。-1关闭。")] public int nameAfterShot=-1;
}

[CreateAssetMenu(menuName="街垒/过场图片与主角命名")]
public sealed class CampaignCinematics : ScriptableObject
{
    public bool enabled=true;
    [Range(0,2)] public float crossfadeSeconds=.6f;
    [Header("第一章前的黑屏字幕与章节报幕")]
    [TextArea(3,8),Tooltip("黑屏居中显示；由用户填写，留空时仅显示黑屏。")] public string openingSubtitle="";
    [Min(0),Tooltip("校音后、第一章报幕前的黑屏字幕秒数，0关闭。")] public float openingSubtitleSeconds=5;
    [Min(0),Tooltip("显示“第一章”及当前章节名字的秒数，0关闭。")] public float firstChapterAnnouncementSeconds=3;
    public bool openingAllowSkip=true;
    [Header("第二至十章的章首报幕")]
    [Min(0),Tooltip("各章分镜前黑屏居中显示章号和章节名字；重试与读档不重复，0关闭。")] public float chapterAnnouncementSeconds=3;
    public bool chapterAnnouncementAllowSkip=true;
    [Header("章节分镜路线")]
    public CinematicRoute[] routes=new CinematicRoute[0];
    [Header("命名操作标签，可直接改；叙事台词填写各镜头的subtitle")]
    public string namePrompt="姓名",namePlaceholder="",confirmLabel="确认",continueUnnamedLabel="暂不命名";
    public string invalidNameMessage="请输入1至12个字符，不含换行或控制字符。";
    public string skipLabel="空格：跳过画面";
    [TextArea,Tooltip("确认后下一镜的可选回应，支持 {playerName}；留空不显示。")] public string nameResponseTemplate="";
    public AudioClip nameResponseVoice;

    public CinematicRoute Find(int chapter,string key,JavertDecision decision,CampaignEnding ending=CampaignEnding.None)
    {
        if(!enabled||routes==null)return null;
        CinematicRoute fallback=null;int best=-1;
        foreach(var route in routes)
        {
            if(route==null||route.chapter!=chapter||route.key!=key)continue;
            if(route.ending!=CampaignEnding.None&&route.ending!=ending)continue;
            if(route.decision!=JavertDecision.Unchosen&&route.decision!=decision)continue;
            int score=(route.ending!=CampaignEnding.None?2:0)+(route.decision!=JavertDecision.Unchosen?1:0);
            if(score>best){best=score;fallback=route;}
        }
        return fallback;
    }
    public void Validate()
    {
        if(!Finite(openingSubtitleSeconds)||!Finite(firstChapterAnnouncementSeconds)||!Finite(chapterAnnouncementSeconds)
            ||openingSubtitleSeconds<0||firstChapterAnnouncementSeconds<0||chapterAnnouncementSeconds<0)
            throw new InvalidOperationException("开场黑屏或章节报幕时长无效。");
        if(float.IsNaN(crossfadeSeconds)||float.IsInfinity(crossfadeSeconds)||crossfadeSeconds<0||crossfadeSeconds>2)
            throw new InvalidOperationException("过场叠化时间无效。");
        var keys=new System.Collections.Generic.HashSet<string>();
        foreach(var route in routes??new CinematicRoute[0])
        {
            if(route==null||route.chapter<2||route.chapter>10||route.shots==null||route.shots.Length==0
                ||(route.key!="chapter-entry"&&route.key!="chapter-exit"&&route.key!="before-choice"&&route.key!="ending"))
                throw new InvalidOperationException("过场路线或镜头缺失。");
            if(!keys.Add(route.chapter+":"+route.key+":"+route.decision+":"+route.ending))throw new InvalidOperationException("重复的过场路线。");
            if(route.nameAfterShot< -1||route.nameAfterShot>=route.shots.Length-1
                ||(route.nameAfterShot>=0&&(route.chapter!=2||route.key!="chapter-entry")))
                throw new InvalidOperationException("命名节点必须位于第二章章首且保留后续镜头。");
            foreach(var shot in route.shots)
                if(shot==null||shot.image==null||float.IsNaN(shot.seconds)||float.IsInfinity(shot.seconds)||shot.seconds<.1f
                    ||float.IsNaN(shot.voiceDelaySeconds)||float.IsInfinity(shot.voiceDelaySeconds)||shot.voiceDelaySeconds<0
                    ||!Finite(shot.panFrom.x)||!Finite(shot.panFrom.y)||!Finite(shot.panTo.x)||!Finite(shot.panTo.y)
                    ||Mathf.Abs(shot.panFrom.x)>2||Mathf.Abs(shot.panFrom.y)>2||Mathf.Abs(shot.panTo.x)>2||Mathf.Abs(shot.panTo.y)>2)
                    throw new InvalidOperationException("过场镜头图片、时长或位移无效；位移范围为±2逻辑像素。");
        }
    }
    static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
