#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator NightmareTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        GameOptions.Difficulty=GameDifficulty.Hard;
        yield return story.DebugChapter(3,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(3);
        var menu=FindFirstObjectByType<CampaignMenuView>();var settings=GameSettingsView.Current;
        CampaignSession.Current.Pause();yield return Frames(2);
        var pauseSetting=menu.pausePage.transform.Find("Settings").GetComponent<Button>();
        Check(menu.pausePage.activeSelf,"Actual pause page opens");
        Check(pauseSetting.GetComponent<RectTransform>().anchorMin.y>menu.pauseQuit.GetComponent<RectTransform>().anchorMax.y,"Settings is above Quit in actual pause layout");
        CaptureUi(1280,720,"暂停菜单.png");pauseSetting.onClick.Invoke();yield return Frames(2);
        Check(GameSettingsView.IsOpen,"Pause settings button opens settings");
        var page=Get<GameObject>(settings,"page");page.transform.Find("ControlsPage/DifficultyNext").GetComponent<Button>().onClick.Invoke();
        Check(GameOptions.Difficulty==GameDifficulty.Nightmare&&PlayerPrefs.GetInt("Barricade.Difficulty")==3,"Nightmare is selectable and saved as fourth difficulty");
        Check(page.transform.Find("ControlsPage/Difficulty").GetComponent<Text>().text.Contains("噩梦"),"Nightmare label is rendered by settings");
        CaptureUi(1280,720,"噩梦设置.png");
        page.transform.Find("ControlsPage/DifficultyNext").GetComponent<Button>().onClick.Invoke();Check(GameOptions.Difficulty==GameDifficulty.Easy,"Difficulty wraps Nightmare to Easy");
        page.transform.Find("ControlsPage/DifficultyPrev").GetComponent<Button>().onClick.Invoke();Check(GameOptions.Difficulty==GameDifficulty.Nightmare,"Previous cycles Easy back to Nightmare");
        settings.Close();Check(CampaignSession.Suspended&&Time.timeScale==0,"Closing settings preserves pause freeze");
        CampaignSession.Current.Resume();yield return Until(()=>!CampaignSession.Suspended);
        var judge=new ConstructionJudgement();judge.Reset(0,-1,.12,0);judge.Reset(1,-1,.12,1);
        Check(judge.Press(0,1.05,false)==ConstructionResult.Grace,"Other difficulties retain old-prompt grace");
        judge.Reset(0,-1,.12,2);judge.Reset(1,-1,.12,3);
        Check(judge.Press(0,3.05,false,GameOptions.PromptGraceEnabled)==ConstructionResult.Wrong,"Nightmare rejects old prompt even within grace interval");
        judge.Reset(0,2,.12,4);Check(judge.Press(0,4.01,true,false)==ConstructionResult.None&&judge.Press(2,4.10,true,false)==ConstructionResult.Perfect,"Nightmare preserves legitimate two-key Perfect timing");
        GameManager.WallHP=100;yield return story.DebugPhase("prep");yield return Frames(3);
        var tap=FindFirstObjectByType<Tap>();var construction=Get<ConstructionJudgement>(story,"construction");
        foreach(var d in new[]{GameDifficulty.Hard,GameDifficulty.Nightmare})
        {
            GameOptions.Difficulty=d;GameManager.WallHP=100;Set(story,"builtHealth",0f);RunState.BuiltHP=0;construction.Reset(0,-1,.12);
            typeof(StoryDirector).GetMethod("ApplyConstructionResult",Private).Invoke(story,new object[]{tap,ConstructionResult.Ordinary});
            Check(GameManager.WallHP==(d==GameDifficulty.Nightmare?100:105),"Actual ordinary construction reward: "+d);
            construction.Reset(0,-1,.12);typeof(StoryDirector).GetMethod("ApplyConstructionResult",Private).Invoke(story,new object[]{tap,ConstructionResult.Perfect});
            Check(GameManager.WallHP==(d==GameDifficulty.Nightmare?110:115),"Actual Perfect reward remains ten: "+d);
        }
        for(int chapter=3;chapter<=9;chapter++)
        {
            GameOptions.Difficulty=GameDifficulty.Hard;yield return story.DebugChapter(chapter,true);CampaignDebug.Value.freeze=true;
            var wave=WaveDirector.Current;wave.enabled=false;ClearThreats();yield return Frames(3);
            foreach(GameDifficulty d in Enum.GetValues(typeof(GameDifficulty)))
            {
                GameOptions.Difficulty=d;Set(wave,"reducedIndex",0);int total=0;
                for(int i=0;i<100;i++)total+=(int)typeof(WaveDirector).GetMethod("SoldierCopies",Private).Invoke(wave,new object[]{"regular"});
                Check(total==GameOptions.DensityPercent(chapter),"Chapter "+chapter+" "+d+" actual density "+total+"/100");
            }
            GameOptions.Difficulty=GameDifficulty.Nightmare;Set(wave,"reducedIndex",0);
            wave.DebugSpawn(WaveEventType.Soldier,1,4,-1);
            Check(wave.ActiveCount()==5,"Four original soldier slots spawn five live soldiers in chapter "+chapter);
            Check(FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).All(e=>e.GetComponent<WaveThreat>()!=null),"All multiplied soldiers retain wave movement in chapter "+chapter);
            ClearThreats();yield return Frames(3);
            var plan=Get<IList>(wave,"pending");foreach(var item in plan)item.GetType().GetField("done").SetValue(item,true);
            story.Clock.DebugComplete(GameDifficultyRules.ExtraSquadBeat(wave.Script));Set(wave,"eventsThisBeat",0);Get<System.Collections.Generic.Dictionary<int,int>>(wave,"laneLast").Clear();
            CampaignDebug.Value.freeze=false;typeof(WaveDirector).GetMethod("TryNightmareSquad",Private).Invoke(wave,null);CampaignDebug.Value.freeze=true;
            Check(wave.NightmareSquadSpawned&&wave.ActiveCount()==3,"Chapter "+chapter+" spawns exactly one additional three-person grenadier squad");
            var progress=JsonUtility.FromJson<WaveDirector.Progress>(JsonUtility.ToJson(wave.CaptureProgress()));wave.RestoreProgress(progress);
            CampaignDebug.Value.freeze=false;typeof(WaveDirector).GetMethod("TryNightmareSquad",Private).Invoke(wave,null);CampaignDebug.Value.freeze=true;
            Check(progress.nightmareSquadSpawned&&wave.ActiveCount()==3,"Chapter "+chapter+" restored progress does not duplicate bonus squad");
            ClearThreats();yield return Frames(2);
            foreach(var script in new[]{story.Chapter.waveA,story.Chapter.waveB})Check(GameDifficultyRules.ExtraSquadWindowClear(script,GameDifficultyRules.ExtraSquadBeat(script)),"Chapter "+chapter+" "+script.variant+" bonus avoids authored escort window");
        }
        report.coverage="Actual Play: pause/settings buttons, four difficulty choices, construction rewards, density and extra grenadier spawning in chapters 3-9, bonus progress JSON restore. User save slots untouched; preferences restored.";
    }
}
#endif
