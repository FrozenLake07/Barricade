#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CampaignVerification
{
    [Serializable] class Report {public bool success;public int chapters,waves,totalChapterBeats,checks;public string error;}
    static Report report;
    static void Require(bool value,string message){report.checks++;if(!value)throw new InvalidOperationException(message);}
    [MenuItem("街垒/战役/打开黑屏开始场景")]
    public static void OpenTitle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
    }
    [MenuItem("街垒/战役/检查十章配置与开始界面")]
    public static void Check()
    {
        report=new Report();
        try
        {
            var table=AssetDatabase.LoadAssetAtPath<ChapterTable>("Assets/Resources/Campaign/ChapterTable.asset");
            Require(table!=null,"缺少ChapterTable。");table.Validate();
            report.chapters=table.chapters.Length;report.totalChapterBeats=table.chapters.Sum(c=>c.TotalBeats);
            report.waves=table.chapters.Sum(c=>c.waveA!=null?c.waveA.events.Length:c.waves.Length);
            foreach(var ch in table.chapters.Where(c=>c.defenseBeats>0))Require(ch.waveA!=null&&ch.waveB!=null,"缺少A/B波次脚本："+ch.number);
            Require(report.totalChapterBeats+2==1832,"战役拍数不符。");
            Require(Mathf.Approximately(table.perfectWindow,.5f),"应保留用户指定的0.5秒窗口。");
            var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Campaign/CampaignRoot.prefab");
            Require(root!=null&&root.GetComponent<StoryDirector>()!=null,"缺少CampaignRoot。");
            var director=root.GetComponent<StoryDirector>();
            Require(director.table==table&&director.font!=null&&director.screenPrefab!=null,"战役、字体或界面引用缺失。");
            Require(director.titleMusic!=null&&director.chapterMusic!=null&&director.endingPrefab!=null,"音乐或结尾引用缺失。");
            var screen=director.screenPrefab;
            Require(screen.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,"开始界面应使用Overlay Canvas。");
            foreach(var name in new[]{"NightMask","BlackScreen","MainText","Detail","ChapterHUD","NarrativeCaption"})
                Require(screen.transform.Find(name)!=null,"界面节点缺失："+name);
            var view=screen.GetComponent<StartScreenView>();
            Require(view!=null&&view.titlePage!=null&&view.prompt!=null&&view.subtitle!=null,"可编辑开始页引用缺失。");
            Require(!string.IsNullOrWhiteSpace(view.prompt.text),"开始提示不能为空，可在 TitlePage/Prompt 中编辑。");
            Require(GameFont.Silver==director.font,"全局字体应引用工程中的 Silver.ttf。");
            foreach(var text in screen.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                Require(text.font==GameFont.Silver,"界面文字未使用 Silver："+text.name);
            foreach(char ch in "按下任意键开始游戏")Require(director.font.HasCharacter(ch),"字体缺字："+ch);
            var build=EditorBuildSettings.scenes;
            Require(build.Length>=3&&build[0].enabled&&build[0].path=="Assets/Scenes/Title.unity","Title应是构建首场景。");
            Require(build.Any(s=>s.enabled&&s.path=="Assets/Scenes/1.unity")&&build.Any(s=>s.enabled&&s.path=="Assets/Scenes/2.unity"),"准备/防守场景未加入构建。");
            var boss=table.chapters[7];
            Require(boss.waveA.events.Count(w=>w.type==WaveEventType.Cannon&&w.beat==40)==1,"第8章应在第40拍部署一门火炮。");
            Require(boss.waveA.events.Where(w=>w.type==WaveEventType.Shell).Select(w=>w.beat).SequenceEqual(new[]{70,100,130,152}),"应配置四次按拍炮击。");
            Require(table.chapters[8].startingWait==.5f&&!table.chapters[8].accelerate,"第9章应锁定0.5秒等待。");
            Require(table.chapters[9].epilogueBeats==183,"尾声应为183拍。");
            Require(table.chapters[8].waveA.maxOnScreen==12,"第9章应允许12个作战单位。");
            Require(table.chapters[8].waveA.events.Count(e=>e.type==WaveEventType.Squad)==12,"第9章应有12支小队。");
            Require(table.chapters[8].waveA.events.Count(e=>e.type==WaveEventType.Shell)==6,"第9章应有6发炮弹。");
            Require(table.chordPerfectHP==15&&table.chordOrdinaryHP==5,"双键收益配置不符。");
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText("Library/CampaignVerification.json",JsonUtility.ToJson(report,true));
        if(!report.success)throw new InvalidOperationException(report.error);
        Debug.Log("[CampaignVerification] 十章数据与开始界面引用通过，共"+report.checks+"项。未代替Play模式通关测试。");
    }
}
#endif
