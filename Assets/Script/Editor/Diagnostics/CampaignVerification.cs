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
            Require(!table.enforceBeatBudget||report.totalChapterBeats==table.targetBeatBudget,"战役拍数不符。");
            Require(table.perfectWindow>0,"Perfect窗口必须为正数。");
            var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Campaign/CampaignRoot.prefab");
            Require(root!=null&&root.GetComponent<StoryDirector>()!=null,"缺少CampaignRoot。");
            var director=root.GetComponent<StoryDirector>();
            Require(director.table==table&&director.font!=null&&director.screenPrefab!=null,"战役、字体或界面引用缺失。");
            Require(director.titleMusic==null&&director.chapterMusic!=null&&director.chapterMusic.name=="Do You Hear The People Sing"&&director.endingPrefab!=null,"标题必须静音，游戏应使用完整People Sing录音，且结尾引用完整。");
            Require(director.mourningMusic!=null&&director.finaleMusic!=null,"两条终章音乐缺失。");
            var cinema=director.cinematics;
            foreach(var ending in new[]{CampaignEnding.Mourning,CampaignEnding.Hope})
                foreach(string key in new[]{"chapter-entry","ending"})
                    Require(cinema!=null&&cinema.Find(10,key,JavertDecision.Unchosen,ending)!=null,"终章分镜分支缺失："+ending+"/"+key);
            var screen=director.screenPrefab;
            Require(screen.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,"开始界面应使用Overlay Canvas。");
            foreach(var name in new[]{"NightMask","BlackScreen","MainText","Detail","ChapterHUD","NarrativeCaption"})
                Require(screen.transform.Find(name)!=null,"界面节点缺失："+name);
            var view=screen.GetComponent<StartScreenView>();
            Require(view!=null&&view.titlePage!=null&&view.prompt!=null&&view.subtitle!=null,"可编辑开始页引用缺失。");
            Require(!string.IsNullOrWhiteSpace(view.prompt.text),"开始提示不能为空，可在 TitlePage/Prompt 中编辑。");
            // Prefabs retain the original Silver fallback; CampaignUI.Bind and StoryDirector.Awake
            // resolve the configured BarricadeUI derivative at runtime.
            var configured=GameFont.Silver;var original=AssetDatabase.LoadAssetAtPath<Font>("Assets/TTF/Silver.ttf");
            Require(configured!=null&&configured.HasCharacter('垒'),"GameFont缺少补字版Silver或街垒字形。");
            Require(director.font==configured||director.font==original,"战役字体必须为Silver或配置的补字版。");
            foreach(var text in screen.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                Require(text.font==configured||text.font==original,"界面文字未使用Silver字体族："+text.name);
            if(Application.isPlaying&&StoryDirector.Current!=null)
                Require(StoryDirector.Current.font==configured,"实际运行字体没有采用GameFont配置。");
            foreach(char ch in "按下任意键开始游戏街垒")Require(configured.HasCharacter(ch),"字体缺字："+ch);
            var build=EditorBuildSettings.scenes;
            Require(build.Length>=3&&build[0].enabled&&build[0].path=="Assets/Scenes/Title.unity","Title应是构建首场景。");
            Require(build.Any(s=>s.enabled&&s.path=="Assets/Scenes/1.unity")&&build.Any(s=>s.enabled&&s.path=="Assets/Scenes/2.unity"),"准备/防守场景未加入构建。");
            Require(table.chapters[1].defenseBeats==0,"第二章应仅建造。");
            Require(table.chapters[0].cinematicBeats==table.firstChapterFrames.Sum(f=>f.beats),"第一章四图总拍数不符。");
            Require(table.chapters.All(c=>c.lines.Length==0),"仍有章中叙事文本。");
            Require(table.chordPerfectHP>=0&&table.chordOrdinaryHP>=0,"双键收益配置不符。");
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        File.WriteAllText("Library/CampaignVerification.json",JsonUtility.ToJson(report,true));
        if(!report.success)throw new InvalidOperationException(report.error);
        Debug.Log("[CampaignVerification] 十章数据与开始界面引用通过，共"+report.checks+"项。未代替Play模式通关测试。");
    }
}
#endif
