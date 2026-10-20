using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable] public class ChapterTransition
{
    public GameObject cinematicPrefab;
    public PlayableAsset timeline;
    public AudioClip audio;
    [Min(0f)] public float placeholderSeconds = 10f;
    public bool allowSkip = true;
    public bool waitForSignal;
    public float signalTimeoutSeconds=60;
    [TextArea] public string caption;
}
[Serializable] public class ChapterLine { public int beat; [TextArea] public string text; }
public enum ChapterWaveKind { Soldier, Leader, Dilemma, Escort, Spy, Cannon, CannonShot }
[Serializable] public class ChapterWave
{
    public int beat, lane = 1, count = 1;
    public ChapterWaveKind kind;
}
[Serializable] public class ChapterDefinition
{
    public int number, preparationBeats, defenseBeats, choiceBeats, epilogueBeats;
    public string title;
    [TextArea] public string introduction;
    public float startingWait = .85f;
    public bool accelerate = true;
    public ChapterLine[] lines = new ChapterLine[0];
    [Tooltip("兼容索引，正式运行使用下方WaveScript")]
    public ChapterWave[] waves = new ChapterWave[0];
    public WaveScript waveA, waveB;
    public ChapterTransition entry = new ChapterTransition();
    public ChapterTransition prepareToDefense = new ChapterTransition { placeholderSeconds = 4f };
    public ChapterTransition exit = new ChapterTransition { placeholderSeconds = 2f };
    public int TotalBeats => preparationBeats + defenseBeats + choiceBeats + epilogueBeats;
}

[CreateAssetMenu(menuName = "街垒/十章战役配置")]
public sealed class ChapterTable : ScriptableObject
{
    public ChapterDefinition[] chapters;
    public int constructionLimit = 600;
    public int lastChancesPerChapter=2;
    public float chordMaxGap=.12f;
    public int chordPerfectHP=15,chordOrdinaryHP=5;
    public float perfectWindow = .5f;
    public ChapterTransition opening = new ChapterTransition { placeholderSeconds = .85f };
    public ChapterTransition ending = new ChapterTransition { placeholderSeconds = 4f };

    public static ChapterDefinition[] CreateDefaults()
    {
        string[] titles = { "葬礼", "筑垒", "第一次进攻", "加弗罗什", "沙威", "夜袭", "瓦让", "火炮", "最后总攻", "街垒之后" };
        int[] preparation = {183,120,20,20,20,20,0,20,20,0};
        int[] defense = {0,63,163,163,163,163,120,163,163,0};
        string[] intro = { "六月五日。拉马克将军的灵车经过这里。", "把马车翻过来。把桶搬上来。", "第一排枪之后，街垒还在。",
            "他跑出去了。没有人命令他。", "一个穿制服的人被认了出来。", "有人把火把扔了过来。",
            "处决间谍的枪，递到了他手里。", "他们把炮推上来了。", "这是最后的斗争。", "街垒陷落。马里乌斯活了下来。" };
        var chapters = new ChapterDefinition[10];
        for (int i=0;i<10;i++)
        {
            int n=i+1;
            var c = new ChapterDefinition { number=n,title=titles[i],introduction=intro[i],preparationBeats=preparation[i],
                defenseBeats=defense[i],choiceBeats=n==7?63:0,epilogueBeats=n==10?183:0,
                startingWait=n>=9?.5f:.85f,accelerate=n!=1&&n!=9&&n!=10 };
            c.waveA=WaveDefaults.Create(n);c.waveB=WaveDefaults.Create(n,true);
            c.waves=WaveDefaults.Legacy(c.waveA);
            c.lines=n==1 ? new[]{new ChapterLine{beat=1,text=intro[0]},new ChapterLine{beat=61,text="有人爬上了马车。有人开始唱歌。"},new ChapterLine{beat=121,text="到今晚为止，这条街不归国王管。"}}
                : new[]{new ChapterLine{beat=1,text=intro[i]}};
            chapters[i]=c;
        }
        return chapters;
    }

    public void Validate()
    {
        if(chapters==null||chapters.Length!=10)throw new InvalidOperationException("战役必须配置十章。");
        for(int i=0;i<chapters.Length;i++)
        {
            var c=chapters[i];
            if(c==null||c.number!=i+1||c.TotalBeats!=183)throw new InvalidOperationException("每章必须按顺序配置183拍。");
            if(c.waveA!=null)c.waveA.Validate();
            if(c.waveB!=null)c.waveB.Validate();
            foreach(var w in c.waves)if(w.beat<0||w.beat>c.defenseBeats||w.lane<0||w.lane>2||w.count<1)
                throw new InvalidOperationException("章节波次越界："+c.number);
        }
    }
}
