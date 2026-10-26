#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator WeaponJamTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        foreach(double multiplier in new[]{1d,.8d,1.5d,2d})
        {
            var normal=new CampaignMagazine();normal.SetAmmo(2,3);normal.Reload(false,false,3,10.25,0,-1,multiplier);
            var jam=new CampaignMagazine();Check(jam.Jam(3,10.25,multiplier),"Full magazine can jam at multiplier "+multiplier);
            Check(Math.Abs(jam.Remaining(10.25,0,1)/normal.Remaining(10.25,0,1)-2)<.000001,"Jam lasts exactly twice normal reload at multiplier "+multiplier);
            Check(!jam.Reload(true,false,3,10.5,0,-1,multiplier)&&!jam.Jam(3,11,multiplier),"Perfect reload and repeated jam cannot bypass or restart recovery");
            jam.Tick(10.25+4*multiplier-.001,0,3);Check(jam.IsJammed&&!jam.CanFire(99,3,false),"Jammed gun cannot fire before full recovery");
            jam.Tick(10.25+4*multiplier,0,3);Check(!jam.IsJammed&&!jam.IsReloading&&jam.Ammo(3,false)==3,"Recovery completes exactly at deadline and fills magazine");
        }
        var empty=new CampaignMagazine();empty.SetAmmo(0,3);Check(!empty.Jam(3,0),"Empty magazine cannot jam");
        var loading=new CampaignMagazine();loading.SetAmmo(2,3);loading.Reload(false,false,3,0,0,-1);
        Check(loading.Jam(3,1)&&Math.Abs(loading.Remaining(1,0,1)-4)<.000001,"Conflicting buttons during partial reload force a fresh jam recovery");
        yield return story.DebugChapter(5,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(3);
        var gun=FindFirstObjectByType<SBullet>();gun.DebugAmmo(gun.CampaignCapacity);
        Check(!gun.TryJam(false,true)&&!gun.TryJam(true,false)&&!gun.IsReloading,"Only simultaneous pressed edges trigger a jam");
        story.Clock.Reset(.85f,.5f);story.Clock.Advance(.9f);int shots=Get<int>(gun,"latestShotId");
        Check(story.Clock.IsPerfect&&gun.TryJam(true,true)&&gun.IsJammed&&gun.IsReloading,"Actual loaded gun jams in Perfect window rather than instant-reloading or shooting");
        Check(Get<int>(gun,"latestShotId")==shots&&!gun.CanFire,"Actual jam does not launch a shot and prevents firing");
        double deadline=Get<double>(Get<CampaignMagazine>(gun,"magazine"),"reloadBeat");
        gun.BeginCampaignReload();gun.OnPerfectHit(shots);gun.TryJam(true,true);
        Check(gun.IsJammed&&Get<double>(Get<CampaignMagazine>(gun,"magazine"),"reloadBeat")==deadline,"Reload key, old Perfect hit and repeated conflict do not cancel or extend actual jam");
        yield return Frames(2);var hud=FindFirstObjectByType<AmmoHudView>();Check(hud.status.text.Contains("卡壳")&&hud.effect.text.Contains("×2"),"Actual HUD displays jam and doubled reload duration");
        CaptureUi(1920,1080,"卡壳反馈");
        CampaignSession.Current.Pause();float remaining=gun.ReloadRemaining;yield return Frames(30);
        Check(Math.Abs(gun.ReloadRemaining-remaining)<.001,"Pause freezes jam recovery");CampaignSession.Current.Resume();yield return Until(()=>!CampaignSession.Suspended);
        var saved=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(story.CaptureSavedGame()));float before=gun.ReloadRemaining;saved.Validate();
        yield return story.RestoreSavedGame(saved);yield return Frames(2);gun=FindFirstObjectByType<SBullet>();
        Check(gun.IsJammed&&gun.IsReloading&&Math.Abs(gun.ReloadRemaining-before)<.3f,"In-memory save/JSON restore preserves actual jam and remaining duration");
        story.Clock.DebugComplete(story.Clock.Completed+5);yield return Frames(2);
        Check(!gun.IsJammed&&!gun.IsReloading&&gun.CampaignAmmo==gun.CampaignCapacity,"Actual recovery unlocks gun and refills magazine");
        gun.DebugAmmo(0);Check(!gun.TryJam(true,true),"Actual empty gun does not jam");
        yield return story.DebugChapter(3,true);CampaignDebug.Value.freeze=true;gun=FindFirstObjectByType<SBullet>();
        Check(!gun.AmmoSystemEnabled&&!gun.TryJam(true,true),"Chapters without the ammunition system do not jam");
        report.coverage="Pure magazine boundary checks plus actual advancing Unity Play gun/HUD, priority in Perfect window, delayed Perfect hit, pause and in-memory save restoration. Input edge flags are supplied directly to the method used by CombatInput; no hardware key injection. User save slots untouched; preferences restored.";
    }
}
#endif
