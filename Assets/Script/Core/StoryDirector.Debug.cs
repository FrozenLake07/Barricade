using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class StoryDirector
{
    public bool DebugBusy=>flowBusy||CampaignSession.Restoring;
    public IEnumerator DebugChapter(int number,bool hard=false,bool retry=false)
    {
        if(number<1||number>table.chapters.Length)throw new ArgumentException("章节范围1—10。");
        if(hard&&table.chapters[number-1].defenseBeats<=0)throw new ArgumentException("本章没有防守段；第二章保留纯建造规则。");
        if(CampaignSession.Suspended)throw new InvalidOperationException("请先关闭暂停菜单并完成倒数。");
        StopAllCoroutines();foreach(var d in FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None))d.Stop();
        cutscene.playableAsset=null;transitionAudio.Stop();ClearWorld();flowBusy=false;
        if(telemetry.DirectoryPath==null)telemetry.StartRun("debug_console");
        if(number<10){RunState.Ending=CampaignEnding.None;RunState.HopeMusicStarted=false;RunState.EndingSourceChapter=0;}
        if(!hard){yield return BeginChapter(number,retry);yield break;}
        RunState.Chapter=number;RunState.EnterChapter(retry);RunState.ResetChapter();
        lastChancesUsed=0;restRemaining=0;FrozenUntilBeat=0;phaseOffset=Chapter.preparationBeats;builtHealth=0;failureReason=null;failed=false;
        GameManager.WallMaxHP=table.constructionLimit;
        GameManager.WallHP=CampaignRevisionRules.EnterChapterHealth(GameManager.WallHP,GameManager.WallMaxHP,retry,RunState.ChapterStartHP);
        // Skipping all construction from Title must not fail before the following batch can set HP.
        if(GameManager.WallHP<=0)GameManager.WallHP=GameManager.WallMaxHP;
        if(!retry)RunState.ChapterStartHP=GameManager.WallHP;
        telemetry.BeginChapter("debug_hard");flowBusy=true;
        if(number==9)StopCampaignMusic();else PlayMusic(chapterMusic);
        yield return LoadScene("2");Phase=CampaignPhase.Defense;ResetClock();waves=new CampaignWaves(this);ui.Gameplay(number==6?.62f:0);RhythmManager.StopMusic();flowBusy=false;
    }
    public IEnumerator DebugPhase(string name)
    {
        if(CampaignSession.Suspended)throw new InvalidOperationException("请先恢复暂停菜单。");
        if(name=="defense"){yield return DebugChapter(RunState.Chapter,true);yield break;}
        if(name=="title"){StopAllCoroutines();ClearWorld();yield return ReturnToTitle();yield break;}
        if(name!="prep"&&name!="choice"&&name!="epilogue"&&name!="results")throw new ArgumentException("可用阶段：title/prep/defense/choice/epilogue/results。");
        if(name=="prep"&&Chapter.preparationBeats<=0||name=="choice"&&Chapter.choiceBeats<=0||name=="epilogue"&&Chapter.epilogueBeats<=0)
            throw new ArgumentException("本章未配置这个阶段；请先 ch 到相应章节。");
        StopAllCoroutines();ClearWorld();flowBusy=true;
        if(name=="results"){failed=false;ShowResults();yield break;}
        yield return LoadScene(name=="prep"||name=="epilogue"?"1":"2");
        if(name=="choice")StartChoice();
        else{Phase=name=="prep"?CampaignPhase.Preparation:CampaignPhase.Epilogue;phaseOffset=0;ResetClock();ui.Gameplay(0);}
        flowBusy=false;
    }
    public void DebugFail(){if(flowBusy)throw new InvalidOperationException("请先结束过场。");FailChapter();}
    public IEnumerator DebugNext()
    {
        if(flowBusy)throw new InvalidOperationException("请先结束过场。");
        if(RunState.Chapter>=10){yield return FinishCampaign();yield break;}
        waves?.Complete();telemetry.EndChapter("debug_completed");yield return DebugChapter(Math.Min(10,RunState.Chapter+1));
    }
    public void DebugLastChance(){if(Phase!=CampaignPhase.Defense||flowBusy)throw new InvalidOperationException("仅防守阶段可触发。");BeginLastChance();}
    public void DebugClear(){if(flowBusy)throw new InvalidOperationException("请先结束过场。");ClearWorld();if(Phase==CampaignPhase.Defense)waves=new CampaignWaves(this);}
    public void DebugSkip()
    {
        signalComplete=true;cinematicView.DebugSkip();
        if(Phase==CampaignPhase.Cinematic)Clock.DebugComplete(Chapter.cinematicBeats);
        if(Phase==CampaignPhase.Calibration)OpeningCalibration.DebugSkip=true;
    }
    public void DebugMusic(string track)
    {
        if(track=="none"||track=="off"){StopCampaignMusic();return;}
        var clip=track=="title"?titleMusic:track=="people"?chapterMusic:null;
        if(clip==null)throw new ArgumentException("未配置此音乐；当前标题音乐按设计留空。");
        audioSource.clip=null;PlayMusic(clip);
    }
    public void DebugHud(bool show)
    {
        CampaignDebug.Value.hideHud=!show;
        foreach(var text in new[]{ui.Hud,ui.Caption})text.enabled=show;
    }
}
