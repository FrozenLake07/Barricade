#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator ChapterNineFinaleTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        yield return story.DebugChapter(9,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(3);
        var music=Get<AudioSource>(story,"audioSource");
        Check(!music.isPlaying&&music.clip==null&&!SfxPlayer.Suppressed,"Chapter-nine console entry mutes background music without suppressing SFX");
        foreach(string id in new[]{"SFX-PLY-01","SFX-ENM-01","SFX-BMB-04","SFX-ART-02","SFX-ART-04","SFX-PLY-03","ui_cancel"})
        {
            int emitted=SfxPlayer.EmittedCount;SfxPlayer.Play(id,.8f);
            Check(SfxPlayer.EmittedCount==emitted+1&&FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.clip==SfxPlayer.ClipFor(id)&&a.isPlaying&&a.volume>0),"Actual chapter-nine audio source plays "+id);
        }
        var loopNode=new GameObject("ChapterNineFuseTest");var loop=loopNode.AddComponent<SelectedSoundLoop>();loop.Set("threat_grenade_fuse_loop",true,.4f);
        Check(loop.Active,"Chapter-nine looping fuse sound is audible through normal selected sound routing");loop.Set("threat_grenade_fuse_loop",false,0);Destroy(loopNode);
        story.PlayTransitionCue(SfxPlayer.ClipFor("SFX-PLY-03"));Check(Get<AudioSource>(story,"transitionAudio").isPlaying,"Chapter-nine non-music transition cues can play");
        var shell=Instantiate(Resources.Load<CannonShell>("arrow/CannonShell"),new Vector3(-100,0,0),Quaternion.identity);shell.Launch(Vector2.right,1,.25f,0);shell.TakeDamage(100);
        Check(!shell.IsAlive,"Ordinary chapter-nine shells remain interceptable");yield return Frames(2);
        for(int scenario=0;scenario<2;scenario++)
        {
            yield return story.DebugChapter(9,true);CampaignDebug.Value.freeze=true;CampaignDebug.Value.god=true;ClearThreats();yield return Frames(3);
            GameManager.WallHP=scenario==0?GameManager.WallMaxHP:25;
            var wave=WaveDirector.Current;foreach(var item in Get<IList>(wave,"pending"))item.GetType().GetField("done").SetValue(item,true);
            int shots=0,impacts=0;var health=new List<int>{GameManager.WallHP};
            Action<string> heard=id=>{if(id=="SFX-ART-02")shots++;if(id=="SFX-ART-04")impacts++;};SfxPlayer.Played+=heard;
            try
            {
                story.Clock.DebugComplete(story.Chapter.defenseBeats);
                yield return Until(()=>story.FinalBarrageActive);Check(!story.CanSave,"Scripted finale cannot be captured as a partial ordinary battle");
                Check(!music.isPlaying,"Background music remains silent during final barrage");
                if(scenario==0)
                {
                    CampaignSession.Current.Pause();int oldShots=story.FinalBarrageShots,oldHP=GameManager.WallHP;yield return Frames(30);
                    Check(story.FinalBarrageShots==oldShots&&GameManager.WallHP==oldHP,"Pause freezes finale firing and wall damage");CampaignSession.Current.Resume();yield return Until(()=>!CampaignSession.Suspended);
                }
                float deadline=Time.unscaledTime+18;bool checkedShell=false,captured=false;
                while(RunState.Chapter==9&&Time.unscaledTime<deadline)
                {
                    if(health[health.Count-1]!=GameManager.WallHP)health.Add(GameManager.WallHP);
                    var final=FindObjectsByType<CannonShell>(FindObjectsSortMode.None).FirstOrDefault(s=>s.IsFinalBarrage&&s.IsAlive);
                    if(final!=null&&!checkedShell)
                    {
                        int hp=final.HP;final.TakeDamage(1000);Check(final.IsAlive&&final.HP==hp,"Scripted ending shell cannot be intercepted");checkedShell=true;
                    }
                    if(story.FinalBarrageShots>=2&&!captured){CaptureCombat("章末连续炮击_"+scenario);captured=true;}
                    yield return null;
                }
                Check(RunState.Chapter==10&&RunState.Ending==CampaignEnding.Hope,"Final barrage enters hope ending after breaking wall, scenario "+scenario);
                Check(story.FinalBarrageShots>=4&&shots>=4&&impacts>=4,"At least four actual cannon launches and wall-impact sounds occur, scenario "+scenario);
                Check(health.Contains(0),"Wall reaches zero before ending entry, scenario "+scenario);
                if(scenario==0)Check(health.Count>=5&&health[0]==600&&health.Contains(450)&&health.Contains(300)&&health.Contains(150),"Full-health wall falls in four visible 25-percent impact steps despite debug invulnerability");
                Check(music.isPlaying&&music.clip==story.finaleMusic,"Hope music starts on ending entry after silent battle");
            }
            finally{SfxPlayer.Played-=heard;}
        }
        CampaignDebug.Value.god=false;
        yield return story.DebugChapter(8,true);CampaignDebug.Value.freeze=true;var earlier=WaveDirector.Current;earlier.DebugSpawn(WaveEventType.Cannon,2,1,-1);
        var normalCannon=FindFirstObjectByType<Cannon>();story.Clock.DebugComplete(story.Chapter.defenseBeats);earlier.Tick();
        Check(!story.FinalBarrageActive&&normalCannon!=null&&normalCannon.IsRetreating,"Chapter eight retains its ordinary retreat and does not start forced finale");
        report.coverage="Actual advancing Unity Play: chapter-nine BGM silence, seven one-shot SFX sources, fuse loop and transition cue, normal shell interception, full/low wall scripted barrage, pause, four physical impacts, ending music and chapter-eight retreat. Listener muted for automated run; no user save slots written; preferences restored.";
    }
}
#endif
