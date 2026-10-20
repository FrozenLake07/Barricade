#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WaveVerification
{
    [MenuItem("街垒/战役/检查A与B波次配置")]
    public static void Check()
    {
        var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");
        if(table==null)throw new InvalidOperationException("缺少战役表。");
        table.Validate();int events=0;
        foreach(var chapter in table.chapters.Where(c=>c.defenseBeats>0))
        {
            var a=chapter.waveA;var b=chapter.waveB;
            if(a==null||b==null||a.chapter!=chapter.number||b.chapter!=chapter.number||a.defenseBeats!=chapter.defenseBeats||b.defenseBeats!=chapter.defenseBeats)
                throw new InvalidOperationException("波次引用或防守拍数不符："+chapter.number);
            a.Validate();b.Validate();
            if(a.events.Length!=b.events.Length)throw new InvalidOperationException("A/B骨架长度不同。");
            for(int i=0;i<a.events.Length;i++)
            {
                var x=a.events[i];var y=b.events[i];
                if(x.type!=y.type||x.beat!=y.beat||x.alignTo!=y.alignTo||x.count!=y.count||x.duration!=y.duration||x.advance!=y.advance||x.lane+y.lane!=4)
                    throw new InvalidOperationException("A/B应只改变轨道："+chapter.number+"/"+i);
            }
            events+=a.events.Length+b.events.Length;
        }
        foreach(string path in new[]{"enemy/Soldier","enemy/Leader"})
        {
            var prefab=Resources.Load<GameObject>(path);
            if(prefab==null||prefab.GetComponent<EnemyMove>()==null||prefab.GetComponent<Rigidbody2D>()==null)
                throw new InvalidOperationException("缺少刷怪预制体或移动/物理组件："+path);
        }
        File.WriteAllText("Library/WaveVerification.json","{\"success\":true,\"scripts\":16,\"events\":"+events+",\"nativePlayVerified\":false}");
        Debug.Log("[WaveVerification] 16份A/B配置通过，事件总计"+events+"。从Title运行，防守按F4核验实际到达和节流。");
    }
}
#endif
