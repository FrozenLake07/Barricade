using UnityEngine;

// 与工程内16份WaveScript资产同源；用于缺失章节表时的默认配置。
public static class WaveDefaults
{
    public static WaveScript Create(int chapter,bool mirror=false)
    {
        if(chapter<3||chapter>9)return null;
        var result=ScriptableObject.CreateInstance<WaveScript>();
        result.chapter=chapter;result.variant=mirror?"B":"A";result.defenseBeats=CampaignRevisionRules.DefenseBeats(chapter);
        switch(chapter)
        {
            case 3: result.events=new WaveEvent[]
            {
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=9f,count=1,spacing=4,duration=0,tag="opening",lanes=new int[]{}},
                new WaveEvent{beat=8,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=7f,count=1,spacing=4,duration=0,tag="Soldier-8",lanes=new int[]{}},
                new WaveEvent{beat=20,alignTo=-1,type=WaveEventType.Squad,lane=2,advance=3f,count=1,spacing=4,duration=0,tag="first-bomb",lanes=new int[]{}},
                new WaveEvent{beat=34,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-34",lanes=new int[]{}},
                new WaveEvent{beat=46,alignTo=80,type=WaveEventType.Soldier,lane=3,advance=4f,count=1,spacing=4,duration=0,tag="dilemma-A",lanes=new int[]{3,1}},
                new WaveEvent{beat=58,alignTo=80,type=WaveEventType.Soldier,lane=2,advance=8f,count=1,spacing=4,duration=0,tag="dilemma-A",lanes=new int[]{}},
                new WaveEvent{beat=76,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-76",lanes=new int[]{2,3}},
                new WaveEvent{beat=92,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=5f,count=1,spacing=4,duration=0,tag="Soldier-92",lanes=new int[]{}},
                new WaveEvent{beat=104,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=3f,count=1,spacing=4,duration=0,tag="Soldier-104",lanes=new int[]{1,3}},
                new WaveEvent{beat=128,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=8f,count=1,spacing=4,duration=0,tag="Soldier-128",lanes=new int[]{}},
                new WaveEvent{beat=126,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-126",lanes=new int[]{}},
            };break;
            case 4: result.events=new WaveEvent[]
            {
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=8f,count=1,spacing=4,duration=0,tag="opening",lanes=new int[]{1,3}},
                new WaveEvent{beat=12,alignTo=40,type=WaveEventType.Soldier,lane=2,advance=5f,count=1,spacing=4,duration=0,tag="escort-threat-1",lanes=new int[]{}},
                new WaveEvent{beat=30,alignTo=-1,type=WaveEventType.Escort,lane=2,advance=0f,count=1,spacing=4,duration=20,tag="escort-1",lanes=new int[]{}},
                new WaveEvent{beat=49,alignTo=80,type=WaveEventType.Soldier,lane=1,advance=4f,count=1,spacing=4,duration=0,tag="escort-threat-2",lanes=new int[]{}},
                new WaveEvent{beat=70,alignTo=-1,type=WaveEventType.Escort,lane=1,advance=0f,count=1,spacing=4,duration=25,tag="escort-2",lanes=new int[]{}},
                new WaveEvent{beat=94,alignTo=125,type=WaveEventType.Soldier,lane=2,advance=4f,count=1,spacing=4,duration=0,tag="escort-threat-3",lanes=new int[]{}},
                new WaveEvent{beat=104,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="exam-bomb",lanes=new int[]{}},
                new WaveEvent{beat=110,alignTo=-1,type=WaveEventType.Escort,lane=2,advance=0f,count=1,spacing=4,duration=25,tag="exam-escort-3",lanes=new int[]{}},
                new WaveEvent{beat=118,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-118",lanes=new int[]{}},
                new WaveEvent{beat=130,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=7f,count=1,spacing=4,duration=0,tag="Soldier-130",lanes=new int[]{}},
            };break;
            case 5: result.events=new WaveEvent[]
            {
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=8f,count=1,spacing=4,duration=0,tag="opening",lanes=new int[]{}},
                new WaveEvent{beat=14,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-14",lanes=new int[]{}},
                new WaveEvent{beat=28,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=7f,count=1,spacing=4,duration=0,tag="Soldier-28",lanes=new int[]{}},
                new WaveEvent{beat=40,alignTo=-1,type=WaveEventType.Decoy,lane=2,advance=0f,count=1,spacing=4,duration=30,tag="javert-1",lanes=new int[]{}},
                new WaveEvent{beat=52,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=5f,count=1,spacing=4,duration=0,tag="Soldier-52",lanes=new int[]{}},
                new WaveEvent{beat=66,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="Squad-66",lanes=new int[]{}},
                new WaveEvent{beat=70,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=5f,count=1,spacing=4,duration=0,tag="Soldier-70",lanes=new int[]{}},
                new WaveEvent{beat=92,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=8f,count=1,spacing=4,duration=0,tag="Soldier-92",lanes=new int[]{}},
                new WaveEvent{beat=100,alignTo=-1,type=WaveEventType.Decoy,lane=1,advance=0f,count=1,spacing=4,duration=25,tag="javert-2",lanes=new int[]{}},
                new WaveEvent{beat=104,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=4f,count=1,spacing=4,duration=0,tag="Soldier-104",lanes=new int[]{}},
                new WaveEvent{beat=118,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-118",lanes=new int[]{}},
                new WaveEvent{beat=130,alignTo=-1,type=WaveEventType.Squad,lane=2,advance=2f,count=1,spacing=4,duration=0,tag="Squad-130",lanes=new int[]{}},
                new WaveEvent{beat=126,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=8f,count=1,spacing=4,duration=0,tag="Soldier-126",lanes=new int[]{}},
                new WaveEvent{beat=134,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=8f,count=1,spacing=4,duration=0,tag="Soldier-134",lanes=new int[]{}},
            };break;
            case 6: result.events=new WaveEvent[]
            {
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=6f,count=1,spacing=4,duration=0,tag="opening",lanes=new int[]{1,3}},
                new WaveEvent{beat=20,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=4f,count=1,spacing=4,duration=0,tag="Soldier-20",lanes=new int[]{}},
                new WaveEvent{beat=40,alignTo=-1,type=WaveEventType.Squad,lane=1,advance=2f,count=1,spacing=4,duration=0,tag="Squad-40",lanes=new int[]{}},
                new WaveEvent{beat=48,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=3f,count=1,spacing=4,duration=0,tag="Soldier-48",lanes=new int[]{2,3}},
                new WaveEvent{beat=70,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=5f,count=1,spacing=4,duration=0,tag="Soldier-70",lanes=new int[]{}},
                new WaveEvent{beat=88,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=2f,count=1,spacing=4,duration=0,tag="Squad-88",lanes=new int[]{}},
                new WaveEvent{beat=96,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=2f,count=1,spacing=4,duration=0,tag="Soldier-96",lanes=new int[]{1,2}},
                new WaveEvent{beat=120,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-120",lanes=new int[]{}},
                new WaveEvent{beat=130,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=8f,count=1,spacing=4,duration=0,tag="Soldier-130",lanes=new int[]{}},
            };break;
            case 7: result.events=new WaveEvent[]
            {
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=8f,count=1,spacing=4,duration=0,tag="opening",lanes=new int[]{1,2}},
                new WaveEvent{beat=18,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-18",lanes=new int[]{}},
                new WaveEvent{beat=34,alignTo=-1,type=WaveEventType.Squad,lane=2,advance=3f,count=1,spacing=4,duration=0,tag="Squad-34",lanes=new int[]{}},
                new WaveEvent{beat=46,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=5f,count=1,spacing=4,duration=0,tag="Soldier-46",lanes=new int[]{}},
                new WaveEvent{beat=62,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=4f,count=1,spacing=4,duration=0,tag="Soldier-62",lanes=new int[]{2,3}},
                new WaveEvent{beat=80,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-80",lanes=new int[]{}},
                new WaveEvent{beat=74,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-74",lanes=new int[]{}},
            };break;
            case 8: result.events=new WaveEvent[]
            {
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=8f,count=1,spacing=4,duration=0,tag="opening",lanes=new int[]{1,3}},
                new WaveEvent{beat=20,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-20",lanes=new int[]{}},
                new WaveEvent{beat=40,alignTo=-1,type=WaveEventType.Cannon,lane=2,advance=0f,count=1,spacing=4,duration=0,tag="deployment",lanes=new int[]{}},
                new WaveEvent{beat=70,alignTo=-1,type=WaveEventType.Shell,lane=2,advance=0f,count=1,spacing=4,duration=0,tag="cannon-1",lanes=new int[]{}},
                new WaveEvent{beat=76,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=5f,count=1,spacing=4,duration=0,tag="Soldier-76",lanes=new int[]{1,3}},
                new WaveEvent{beat=100,alignTo=-1,type=WaveEventType.Shell,lane=2,advance=0f,count=1,spacing=4,duration=0,tag="cannon-2",lanes=new int[]{}},
                new WaveEvent{beat=104,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="Squad-104",lanes=new int[]{}},
                new WaveEvent{beat=130,alignTo=-1,type=WaveEventType.Shell,lane=2,advance=0f,count=1,spacing=4,duration=0,tag="cannon-3",lanes=new int[]{}},
                new WaveEvent{beat=126,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=6f,count=1,spacing=4,duration=0,tag="Soldier-126",lanes=new int[]{1,2}},
                new WaveEvent{beat=152,alignTo=-1,type=WaveEventType.Shell,lane=2,advance=0f,count=1,spacing=4,duration=0,tag="finale-cannon-4",lanes=new int[]{}},
            };break;
            case 9: result.events=new WaveEvent[]
            {
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Cannon,lane=2,advance=0f,count=1,spacing=4,duration=0,tag="cannon-persist",lanes=new int[]{}},
                new WaveEvent{beat=0,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=9f,count=1,spacing=4,duration=0,tag="opening",lanes=new int[]{}},
                new WaveEvent{beat=8,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=9f,count=1,spacing=4,duration=0,tag="scatter-8",lanes=new int[]{}},
                new WaveEvent{beat=16,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=9f,count=1,spacing=4,duration=0,tag="scatter-16",lanes=new int[]{}},
                new WaveEvent{beat=24,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=9f,count=1,spacing=4,duration=0,tag="scatter-24",lanes=new int[]{}},
                new WaveEvent{beat=32,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=9f,count=1,spacing=4,duration=0,tag="scatter-32",lanes=new int[]{}},
                new WaveEvent{beat=40,alignTo=-1,type=WaveEventType.Squad,lane=1,advance=3f,count=1,spacing=4,duration=0,tag="saturation-40-40-1",lanes=new int[]{}},
                new WaveEvent{beat=40,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="saturation-40-40-3",lanes=new int[]{}},
                new WaveEvent{beat=41,alignTo=-1,type=WaveEventType.Squad,lane=1,advance=3f,count=1,spacing=4,duration=0,tag="saturation-40-41-1",lanes=new int[]{}},
                new WaveEvent{beat=41,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="saturation-40-41-3",lanes=new int[]{}},
                new WaveEvent{beat=42,alignTo=-1,type=WaveEventType.Shell,lane=1,advance=0f,count=1,spacing=4,duration=0,tag="saturation-shell-42",lanes=new int[]{}},
                new WaveEvent{beat=43,alignTo=-1,type=WaveEventType.Shell,lane=3,advance=0f,count=1,spacing=4,duration=0,tag="saturation-shell-43",lanes=new int[]{}},
                new WaveEvent{beat=48,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=6f,count=1,spacing=4,duration=0,tag="scatter-48",lanes=new int[]{}},
                new WaveEvent{beat=56,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=6f,count=1,spacing=4,duration=0,tag="scatter-56",lanes=new int[]{}},
                new WaveEvent{beat=64,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=6f,count=1,spacing=4,duration=0,tag="scatter-64",lanes=new int[]{}},
                new WaveEvent{beat=72,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=6f,count=1,spacing=4,duration=0,tag="scatter-72",lanes=new int[]{}},
                new WaveEvent{beat=80,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=6f,count=1,spacing=4,duration=0,tag="scatter-80",lanes=new int[]{}},
                new WaveEvent{beat=88,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=6f,count=1,spacing=4,duration=0,tag="scatter-88",lanes=new int[]{}},
                new WaveEvent{beat=90,alignTo=-1,type=WaveEventType.Squad,lane=1,advance=3f,count=1,spacing=4,duration=0,tag="saturation-90-90-1",lanes=new int[]{}},
                new WaveEvent{beat=90,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="saturation-90-90-3",lanes=new int[]{}},
                new WaveEvent{beat=91,alignTo=-1,type=WaveEventType.Squad,lane=1,advance=3f,count=1,spacing=4,duration=0,tag="saturation-90-91-1",lanes=new int[]{}},
                new WaveEvent{beat=91,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="saturation-90-91-3",lanes=new int[]{}},
                new WaveEvent{beat=92,alignTo=-1,type=WaveEventType.Shell,lane=1,advance=0f,count=1,spacing=4,duration=0,tag="saturation-shell-92",lanes=new int[]{}},
                new WaveEvent{beat=93,alignTo=-1,type=WaveEventType.Shell,lane=3,advance=0f,count=1,spacing=4,duration=0,tag="saturation-shell-93",lanes=new int[]{}},
                new WaveEvent{beat=98,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=5f,count=1,spacing=4,duration=0,tag="scatter-98",lanes=new int[]{}},
                new WaveEvent{beat=106,alignTo=-1,type=WaveEventType.Soldier,lane=2,advance=5f,count=1,spacing=4,duration=0,tag="scatter-106",lanes=new int[]{}},
                new WaveEvent{beat=114,alignTo=-1,type=WaveEventType.Soldier,lane=3,advance=5f,count=1,spacing=4,duration=0,tag="scatter-114",lanes=new int[]{}},
                new WaveEvent{beat=122,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=5f,count=1,spacing=4,duration=0,tag="scatter-122",lanes=new int[]{}},
                new WaveEvent{beat=130,alignTo=-1,type=WaveEventType.Squad,lane=1,advance=3f,count=1,spacing=4,duration=0,tag="saturation-130-130-1",lanes=new int[]{}},
                new WaveEvent{beat=130,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="saturation-130-130-3",lanes=new int[]{}},
                new WaveEvent{beat=131,alignTo=-1,type=WaveEventType.Squad,lane=1,advance=3f,count=1,spacing=4,duration=0,tag="saturation-130-131-1",lanes=new int[]{}},
                new WaveEvent{beat=131,alignTo=-1,type=WaveEventType.Squad,lane=3,advance=3f,count=1,spacing=4,duration=0,tag="saturation-130-131-3",lanes=new int[]{}},
                new WaveEvent{beat=133,alignTo=-1,type=WaveEventType.Shell,lane=1,advance=0f,count=1,spacing=4,duration=0,tag="saturation-shell-133",lanes=new int[]{}},
                new WaveEvent{beat=134,alignTo=-1,type=WaveEventType.Shell,lane=3,advance=0f,count=1,spacing=4,duration=0,tag="saturation-shell-134",lanes=new int[]{}},
                new WaveEvent{beat=138,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=9f,count=1,spacing=4,duration=0,tag="final-soldiers-138",lanes=new int[]{1,3}},
                new WaveEvent{beat=146,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=9f,count=1,spacing=4,duration=0,tag="final-soldiers-146",lanes=new int[]{1,3}},
                new WaveEvent{beat=154,alignTo=-1,type=WaveEventType.Soldier,lane=1,advance=9f,count=1,spacing=4,duration=0,tag="final-soldiers-154",lanes=new int[]{1,3}},
            };break;
        }
        var extended=new WaveEvent[result.events.Length+1];
        System.Array.Copy(result.events,extended,result.events.Length);
        extended[extended.Length-1]=new WaveEvent{beat=result.defenseBeats-24,alignTo=result.defenseBeats-10,
            type=WaveEventType.Soldier,lane=chapter%3+1,advance=9f,count=1,spacing=4,duration=0,
            tag="choice-time-redistributed",lanes=new int[0]};
        result.events=extended;
        if(chapter==9){result.maxOnScreen=12;result.laneCooldown=1;}
        if(chapter==8)result.requiredShellInterceptions=2;
        if(mirror)foreach(var e in result.events){e.lane=4-e.lane;for(int i=0;i<e.lanes.Length;i++)e.lanes[i]=4-e.lanes[i];}
        CombatPressureRules.Apply(result);
        return result;
    }
    public static ChapterWave[] Legacy(WaveScript source)
    {
        if(source==null)return new ChapterWave[0];
        var rows=new System.Collections.Generic.List<ChapterWave>();
        foreach(var e in source.events)
        {
            var kind=e.type==WaveEventType.Squad?ChapterWaveKind.Leader:e.type==WaveEventType.Shell?ChapterWaveKind.CannonShot:e.type==WaveEventType.Decoy?ChapterWaveKind.Spy:e.type==WaveEventType.Escort?ChapterWaveKind.Escort:e.type==WaveEventType.Cannon?ChapterWaveKind.Cannon:ChapterWaveKind.Soldier;
            foreach(int lane in e.lanes.Length>0?e.lanes:new[]{e.lane})rows.Add(new ChapterWave{beat=e.beat,kind=kind,lane=lane-1,count=e.type==WaveEventType.Squad?3:1});
        }
        return rows.ToArray();
    }
}
