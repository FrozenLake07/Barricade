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
    public int number, preparationBeats, defenseBeats, choiceBeats, epilogueBeats, cinematicBeats;
    [Tooltip("章节显示名字。可通过菜单：街垒/战役/编辑章节名字，集中修改。")]
    public string title;
    [TextArea] public string introduction;
    public float startingWait = .85f;
    public bool accelerate = true;
    [Min(.1f),Tooltip("本章敌人对街垒的伤害倍率；不改变敌人血量或玩家伤害。默认3～7章加强。") ]
    public float combatDamageMultiplier=1f;
    public ChapterLine[] lines = new ChapterLine[0];
    [Tooltip("兼容索引，正式运行使用下方WaveScript")]
    public ChapterWave[] waves = new ChapterWave[0];
    public WaveScript waveA, waveB;
    public ChapterTransition entry = new ChapterTransition();
    public ChapterTransition prepareToDefense = new ChapterTransition { placeholderSeconds = 4f };
    public ChapterTransition exit = new ChapterTransition { placeholderSeconds = 2f };
    public int TotalBeats => preparationBeats + defenseBeats + choiceBeats + epilogueBeats + cinematicBeats;
}

[CreateAssetMenu(menuName = "街垒/十章战役配置")]
public sealed class ChapterTable : ScriptableObject
{
    public ChapterDefinition[] chapters;
    public string playtestLabel="combat-v2-escort-carry-20261021";
    public bool recordPlaytests=true;
    public bool calibrateEveryNewGame=true;
    public int targetBeatBudget=1830;
    public bool enforceBeatBudget;

    public ChapterFrame[] firstChapterFrames = new ChapterFrame[0];
    [Range(.1f,2f)] public float firstChapterCrossfadeSeconds=.6f;
    public int constructionLimit = 600;
    public int lastChancesPerChapter=2;
    public float chordMaxGap=.12f;
    public int chordPerfectHP=15,chordOrdinaryHP=5;
    public float perfectWindow = .5f;
    public ChapterTransition opening = new ChapterTransition { placeholderSeconds = .85f };
    public ChapterTransition ending = new ChapterTransition { placeholderSeconds = 4f };

    public static ChapterDefinition[] CreateDefaults()
    {
        string[] titles = { "葬礼", "筑垒", "第一次进攻", "加弗罗什", "沙威", "夜袭", "瓦让", "火炮", "最后总攻", "终章" };
        string[] intro = { "六月五日。拉马克将军的灵车经过这里。", "把马车翻过来。把桶搬上来。", "第一排枪之后，街垒还在。",
            "他跑出去了。没有人命令他。", "一个穿制服的人被认了出来。", "有人把火把扔了过来。",
            "处决间谍的枪，递到了他手里。", "他们把炮推上来了。", "这是最后的斗争。", "街垒陷落。马里乌斯活了下来。" };
        var chapters = new ChapterDefinition[10];
        for (int i=0;i<10;i++)
        {
            int n=i+1;
            var c = new ChapterDefinition { number=n,title=titles[i],introduction=intro[i],preparationBeats=CampaignRevisionRules.PreparationBeats(n),
                defenseBeats=CampaignRevisionRules.DefenseBeats(n),choiceBeats=n==7?7:0,epilogueBeats=n==10?183:0,cinematicBeats=n==1?32:0,
                combatDamageMultiplier=CampaignRevisionRules.CombatDamageMultiplier(n),startingWait=n>=9?.5f:.85f,accelerate=n!=1&&n!=9&&n!=10 };
            c.waveA=WaveDefaults.Create(n);c.waveB=WaveDefaults.Create(n,true);
            c.waves=WaveDefaults.Legacy(c.waveA);
            c.lines=new ChapterLine[0];
            chapters[i]=c;
        }
        return chapters;
    }

    public void Validate()
    {
        if(chapters==null||chapters.Length!=10)throw new InvalidOperationException("战役必须配置十章。");
        int totalBeats=0;
        for(int i=0;i<chapters.Length;i++)
        {
            var c=chapters[i];
            if(c==null||c.number!=i+1||c.TotalBeats<1)throw new InvalidOperationException("章节顺序或拍数无效。");
            if(string.IsNullOrWhiteSpace(c.title)||c.preparationBeats<0||c.defenseBeats<0||c.choiceBeats<0||c.epilogueBeats<0||c.cinematicBeats<0
                ||float.IsNaN(c.startingWait)||float.IsInfinity(c.startingWait)||c.startingWait<.05f)
                throw new InvalidOperationException("章节名字、拍数或节奏无效："+c.number);
            if((i!=0&&c.cinematicBeats>0)||(i!=9&&c.epilogueBeats>0)||(i!=6&&c.choiceBeats>0))
                throw new InvalidOperationException("四图只用于第1章，选择只用于第7章，尾声只用于第10章。");
            if(float.IsNaN(c.combatDamageMultiplier)||float.IsInfinity(c.combatDamageMultiplier)||c.combatDamageMultiplier<.1f)throw new InvalidOperationException("战斗伤害倍率无效："+c.number);
            if(c.choiceBeats>0&&c.defenseBeats==0)throw new InvalidOperationException("选择阶段需要前置防守。");
            if(c.defenseBeats>0&&(c.defenseBeats<5||c.waveA==null||c.waveB==null))throw new InvalidOperationException("防守至少5拍且需要A/B波次："+c.number);
            if(c.waveA!=null&&c.waveA.chapter!=c.number||c.waveB!=null&&c.waveB.chapter!=c.number)throw new InvalidOperationException("波次所属章节不符。");
            ValidateTransition(c.entry);ValidateTransition(c.prepareToDefense);ValidateTransition(c.exit);
            if(c.waveA!=null&&c.waveA.defenseBeats!=c.defenseBeats)throw new InvalidOperationException("A波次防守拍数不符。");
            if(c.waveB!=null&&c.waveB.defenseBeats!=c.defenseBeats)throw new InvalidOperationException("B波次防守拍数不符。");
            totalBeats+=c.TotalBeats;
            if(c.waveA!=null)c.waveA.Validate();
            if(c.waveB!=null)c.waveB.Validate();
            foreach(var w in c.waves)if(w.beat<0||w.beat>c.defenseBeats||w.lane<0||w.lane>2||w.count<1)
                throw new InvalidOperationException("章节波次越界："+c.number);
        }
        if(targetBeatBudget<1||constructionLimit<1||lastChancesPerChapter<0||perfectWindow<=0||float.IsNaN(perfectWindow)||float.IsInfinity(perfectWindow)||chordMaxGap<=0||float.IsNaN(chordMaxGap)||float.IsInfinity(chordMaxGap)||chordPerfectHP<0||chordOrdinaryHP<0||firstChapterCrossfadeSeconds<0||float.IsNaN(firstChapterCrossfadeSeconds)||float.IsInfinity(firstChapterCrossfadeSeconds))
            throw new InvalidOperationException("全局节奏、耐久或预算无效。");
        if(enforceBeatBudget&&totalBeats!=targetBeatBudget)throw new InvalidOperationException("章节总拍数不等于目标预算："+targetBeatBudget);
        ValidateTransition(ending);
        var construction=chapters[1];
        if(construction.defenseBeats!=0||construction.waveA!=null||construction.waveB!=null||construction.waves.Length!=0)
            throw new InvalidOperationException("第二章仅建造，不应配置防守或波次。");
        var first=chapters[0];
        if(first.cinematicBeats<1||first.preparationBeats!=0||first.defenseBeats!=0)throw new InvalidOperationException("第一章必须是纯四图演出。");
        if(firstChapterFrames==null||firstChapterFrames.Length!=4)throw new InvalidOperationException("第一章需要四张图片。");
        var last=chapters[9];
        if(last.epilogueBeats<1||last.preparationBeats!=0||last.defenseBeats!=0)throw new InvalidOperationException("第十章必须是尾声。");
        int filmBeats=0;
        foreach(var frame in firstChapterFrames)
        {
            if(frame==null||frame.image==null||frame.beats<1)throw new InvalidOperationException("第一章画面或时长缺失。");
            if(float.IsNaN(frame.voiceDelaySeconds)||float.IsInfinity(frame.voiceDelaySeconds)||frame.voiceDelaySeconds<0||frame.voiceDelaySeconds>=frame.beats*(first.startingWait+perfectWindow))throw new InvalidOperationException("配音延迟超出镜头。");
            filmBeats+=frame.beats;
        }
        if(filmBeats!=first.cinematicBeats)throw new InvalidOperationException("四图拍数合计不符。");
    }
    static void ValidateTransition(ChapterTransition t)
    {
        if(t==null||float.IsNaN(t.placeholderSeconds)||float.IsInfinity(t.placeholderSeconds)||t.placeholderSeconds<0||float.IsNaN(t.signalTimeoutSeconds)||float.IsInfinity(t.signalTimeoutSeconds)||t.signalTimeoutSeconds<=0)
            throw new InvalidOperationException("过场配置或超时时间无效。");
    }

}
