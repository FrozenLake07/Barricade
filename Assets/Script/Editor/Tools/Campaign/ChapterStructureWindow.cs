#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChapterStructureWindow : EditorWindow
{
    const string AssetPath="Assets/Resources/Campaign/ChapterTable.asset";
    ChapterTable source,draft;SerializedObject serialized;Label status,totals;
    EventCallback<SerializedPropertyChangeEvent> changed;
    readonly List<WaveScript> temporaryWaves=new List<WaveScript>();
    [MenuItem("街垒/战役/编辑章节结构与内容")]
    public static void Open(){var w=GetWindow<ChapterStructureWindow>("章节结构与内容");w.minSize=new Vector2(650,600);w.Show();}
    [MenuItem("街垒/测试/打开试玩数据目录")]
    public static void OpenData(){Directory.CreateDirectory(CampaignTelemetry.RootPath);EditorUtility.RevealInFinder(CampaignTelemetry.RootPath);}
    void OnEnable(){EditorApplication.playModeStateChanged+=PlayModeChanged;}
    void OnDisable(){EditorApplication.playModeStateChanged-=PlayModeChanged;DisposeDraft();}
    void PlayModeChanged(PlayModeStateChange state){rootVisualElement.SetEnabled(state==PlayModeStateChange.EnteredEditMode);}
    void DisposeDraft(){rootVisualElement.Unbind();if(draft!=null)DestroyImmediate(draft);draft=null;serialized=null;}
    public void CreateGUI()
    {
        DisposeDraft();var root=rootVisualElement;root.Clear();
        root.style.paddingLeft=root.style.paddingRight=12;root.style.paddingTop=root.style.paddingBottom=10;
        source=AssetDatabase.LoadAssetAtPath<ChapterTable>(AssetPath);
        if(source==null){root.Add(new HelpBox("缺少章节配置："+AssetPath,HelpBoxMessageType.Error));return;}
        draft=Instantiate(source);draft.hideFlags=HideFlags.HideAndDontSave;serialized=new SerializedObject(draft);
        root.Add(new HelpBox("先修改草稿，再“校验并保存”。顺序为章首过场 → 建造/修补 → 防守 → 选择 → 章末；0拍关闭相应阶段。第一章使用四图，尾声在第十章。波次编辑在独立检查器中即时生效，请先保存/备份资产。",HelpBoxMessageType.Info));
        var scroll=new ScrollView();scroll.style.flexGrow=1;root.Add(scroll);
        var global=new Foldout{text="全局节奏、预算与试玩记录",value=true};scroll.Add(global);
        foreach(var pair in new[]{new[]{"playtestLabel","本轮测试标签"},new[]{"recordPlaytests","自动记录试玩数据"},new[]{"calibrateEveryNewGame","每次新游戏显示校音"},new[]{"targetBeatBudget","目标总拍数"},new[]{"enforceBeatBudget","强制总拍数等于目标"},new[]{"perfectWindow","Perfect窗口（秒）"},new[]{"constructionLimit","耐久上限"},new[]{"lastChancesPerChapter","每章抢救次数"},new[]{"chordMaxGap","双键最大间隔（秒）"},new[]{"chordPerfectHP","双键Perfect回复"},new[]{"chordOrdinaryHP","双键普通回复"}})Add(global,serialized.FindProperty(pair[0]),pair[1]);
        var chapters=serialized.FindProperty("chapters");
        for(int i=0;i<chapters.arraySize;i++){
            int index=i;var c=chapters.GetArrayElementAtIndex(i);
            var box=new Foldout{text="第"+(i+1)+"章 · "+draft.chapters[i].title,value=i==1};scroll.Add(box);
            Add(box,c.FindPropertyRelative("title"),"章节名字");
            Add(box,c.FindPropertyRelative("introduction"),"章首介绍（过场占位）");
            Add(box,c.FindPropertyRelative("entry"),"章首过场");
            if(i==0){Add(box,c.FindPropertyRelative("cinematicBeats"),"四图总拍数（保存时自动合计）");Add(box,serialized.FindProperty("firstChapterFrames"),"第一章四图 / 拍数 / 字幕 / 配音");Add(box,serialized.FindProperty("firstChapterCrossfadeSeconds"),"图片叠化秒数");}
            else if(i==9)Add(box,c.FindPropertyRelative("epilogueBeats"),"尾声拍数");
            else{
                Add(box,c.FindPropertyRelative("preparationBeats"),i==1?"建造拍数":"修补拍数");
                if(i!=1){Add(box,c.FindPropertyRelative("prepareToDefense"),"修补→防守过场");Add(box,c.FindPropertyRelative("defenseBeats"),"防守拍数");Add(box,c.FindPropertyRelative("combatDamageMultiplier"),"敌人对街垒伤害倍率");
                    if(i==6)Add(box,c.FindPropertyRelative("choiceBeats"),"选择超时拍数");
                    Add(box,c.FindPropertyRelative("waveA"),"A波次资产");Add(box,c.FindPropertyRelative("waveB"),"B波次资产");
                    var waveButtons=new VisualElement();waveButtons.style.flexDirection=FlexDirection.Row;box.Add(waveButtons);
                    waveButtons.Add(new Button(()=>EditWave(index,false)){text="编辑A波次"});waveButtons.Add(new Button(()=>EditWave(index,true)){text="编辑B波次"});
                }
            }
            Add(box,c.FindPropertyRelative("startingWait"),"起始等待（秒）");Add(box,c.FindPropertyRelative("accelerate"),"Perfect后加速");Add(box,c.FindPropertyRelative("exit"),"章末 / 失败过场");
        }
        var boundaries=new Foldout{text="战役结束过场",value=false};scroll.Add(boundaries);Add(boundaries,serialized.FindProperty("ending"),"结束过场");
        totals=new Label();totals.style.whiteSpace=WhiteSpace.Normal;root.Add(totals);
        status=new Label("草稿尚未写入工程。波次修改需在Inspector单独保存。");status.style.whiteSpace=WhiteSpace.Normal;root.Add(status);
        var buttons=new VisualElement();buttons.style.flexDirection=FlexDirection.Row;root.Add(buttons);
        buttons.Add(new Button(Save){text="校验并保存"});buttons.Add(new Button(CreateGUI){text="丢弃草稿并重新读取"});buttons.Add(new Button(ExportConfiguration){text="导出当前已保存配置"});buttons.Add(new Button(OpenData){text="打开试玩数据"});
        root.Bind(serialized);if(changed!=null)root.UnregisterCallback(changed);changed=_=>RefreshTotal();root.RegisterCallback(changed);RefreshTotal();
        root.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
    }
    static void Add(VisualElement parent,SerializedProperty property,string label){if(property!=null)parent.Add(new PropertyField(property.Copy(),label));}
    void RefreshTotal(){if(draft==null||totals==null)return;totals.text="章节合计："+draft.chapters.Sum(c=>c.TotalBeats)+"拍；目标："+draft.targetBeatBudget+"拍。每拍含等待与判定窗口，实际时长随输入变化。";}
    void EditWave(int chapter,bool b){serialized.ApplyModifiedProperties();var w=b?draft.chapters[chapter].waveB:draft.chapters[chapter].waveA;if(w!=null){Selection.activeObject=w;EditorGUIUtility.PingObject(w);}}
    void Save()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){status.text="请退出Play模式再保存。";return;}
        serialized.ApplyModifiedProperties();
        var validation=Instantiate(draft);validation.hideFlags=HideFlags.HideAndDontSave;temporaryWaves.Clear();
        try{
            foreach(var slot in validation.chapters.SelectMany(c=>new[]{c.entry,c.prepareToDefense,c.exit}).Concat(new[]{validation.ending}))
                if(slot?.cinematicPrefab!=null&&!EditorUtility.IsPersistent(slot.cinematicPrefab))throw new InvalidOperationException("过场必须引用Project内的Prefab资产，不能引用Hierarchy场景对象。");
            validation.chapters[0].cinematicBeats=validation.firstChapterFrames.Sum(f=>f.beats);
            foreach(var c in validation.chapters){
                if(c.defenseBeats==0){c.waveA=null;c.waveB=null;c.waves=new ChapterWave[0];continue;}
                c.waveA=CloneWave(c.waveA,c.defenseBeats);c.waveB=CloneWave(c.waveB,c.defenseBeats);c.waves=new ChapterWave[0];
            }
            validation.Validate();
            Undo.RecordObject(source,"修改章节结构与内容");draft.chapters[0].cinematicBeats=validation.chapters[0].cinematicBeats;
            var waveOwners=new Dictionary<WaveScript,int>();
            foreach(var c in draft.chapters)foreach(var wave in new[]{c.waveA,c.waveB})if(wave!=null&&c.defenseBeats>0){
                if(waveOwners.TryGetValue(wave,out int existing)&&existing!=c.defenseBeats)throw new InvalidOperationException("同一波次资产不能用于不同防守时长。");waveOwners[wave]=c.defenseBeats;
            }
            foreach(var pair in waveOwners){Undo.RecordObject(pair.Key,"同步防守拍数");pair.Key.defenseBeats=pair.Value;EditorUtility.SetDirty(pair.Key);}
            foreach(var c in draft.chapters){if(c.defenseBeats==0){c.waveA=null;c.waveB=null;}c.waves=new ChapterWave[0];}
            EditorUtility.CopySerializedManagedFieldsOnly(draft,source);EditorUtility.SetDirty(source);AssetDatabase.SaveAssets();serialized.Update();RefreshTotal();status.text="配置已校验并保存。开始新游戏，自动创建独立试玩记录。";
        }catch(Exception e){status.text="未保存："+e.Message;}
        finally{foreach(var w in temporaryWaves)DestroyImmediate(w);DestroyImmediate(validation);temporaryWaves.Clear();}
    }
    WaveScript CloneWave(WaveScript wave,int beats){if(wave==null)return null;var clone=Instantiate(wave);clone.hideFlags=HideFlags.HideAndDontSave;clone.defenseBeats=beats;temporaryWaves.Add(clone);return clone;}
    void ExportConfiguration()
    {
        try{source.Validate();string path=EditorUtility.SaveFilePanel("导出已保存的章节配置",Application.dataPath,"CampaignConfiguration","json");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,CampaignTelemetry.ConfigurationJson(source));}
        catch(Exception e){status.text="导出失败："+e.Message;}
    }
}
#endif
