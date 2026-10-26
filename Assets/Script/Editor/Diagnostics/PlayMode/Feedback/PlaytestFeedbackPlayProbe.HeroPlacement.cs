#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator HeroPlacementTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        report.coverage="Actual defense spawn, fixed authored hero perch during lane switching and both aim animations, linked muzzle transforms, all three barricade health art stages, old saved lane-snapped position migration, other entity transform restoration and fresh chapter reload. User save slots not written.";
        yield return story.DebugChapter(5,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(3);
        var actor=FindFirstObjectByType<HeroCombatDriver>().actor;var lanes=actor.GetComponent<CapsuleTrack>();
        var expected=new Vector3(5.09375f,-1.3125f,0);
        Check(Vector3.Distance(actor.transform.position,expected)<.0001f,"Defense startup preserves authored barricade perch instead of snapping to attack lane");
        Check(actor.visual.sortingOrder==6,"Hero visual sorting order is preserved");
        Check(lanes.s1.position.y==-4.5f&&lanes.s2.position.y==-2&&lanes.s3.position.y==.5f,"All enemy/attack lane heights are preserved");
        lanes.DebugLane(1);yield return Frames(2);Check(Vector3.Distance(actor.transform.position,expected)<.0001f&&CapsuleTrack.AttackY==-4.5f,"Lower lane changes aim without moving the hero");
        lanes.DebugLane(3);yield return Frames(2);Check(Vector3.Distance(actor.transform.position,expected)<.0001f&&CapsuleTrack.AttackY==.5f,"Upper lane changes aim without moving the hero");
        actor.PlayStory(HeroMotion.AimGround);yield return Frames(3);CaptureCombat("战斗站位_地面瞄准");
        var shooter=FindFirstObjectByType<SBullet>();
        Check(shooter.groundMuzzle==actor.groundMuzzle&&shooter.upMuzzle==actor.upMuzzle&&actor.groundMuzzle.IsChildOf(actor.visual.transform)&&actor.upMuzzle.IsChildOf(actor.visual.transform),"Both actual firing origins remain attached to the repositioned visual");
        Check(Vector3.Distance(actor.transform.position,expected)<.0001f,"Ground aim keeps the same perch");
        actor.PlayStory(HeroMotion.AimUp);yield return Frames(3);CaptureCombat("战斗站位_空中瞄准");
        Check(Vector3.Distance(actor.transform.position,expected)<.0001f,"Air aim keeps the same perch");
        var wall=FindFirstObjectByType<BarricadeHealthVisual>();
        foreach(int hp in new[]{600,300,100})
        {
            GameManager.WallHP=hp;wall.RefreshNow();yield return Frames(2);CaptureCombat("街垒耐久_"+hp);
            Check(Vector3.Distance(actor.transform.position,expected)<.0001f&&wall.GetComponent<SpriteRenderer>().sortingOrder==2,"Perch and barricade sorting remain stable at health "+hp);
        }
        GameManager.WallHP=450;actor.ResumeGameplay();lanes.DebugLane(2);yield return Frames(3);
        var enemy=MovingEnemy(false,true);enemy.transform.position=new Vector3(-3,-2,0);enemy.GetComponent<EnemyHP>().TakeDamage(10);yield return Frames(2);
        var saved=story.CaptureSavedGame();string actorPath=new CampaignSaveWorld().Reference(actor.gameObject);
        saved.world.nodes.Single(n=>n.path==actorPath).position=new Vector3(6,-2,0);
        var restored=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(saved));yield return story.RestoreSavedGame(restored);yield return Frames(3);
        actor=FindFirstObjectByType<HeroCombatDriver>().actor;
        Check(Vector3.Distance(actor.transform.position,expected)<.0001f,"Older saved lane-snapped position no longer overrides the new scene perch");
        Check(GameManager.WallHP==450&&CapsuleTrack.AttackIndex==1,"Saved wall health and attack lane still restore");
        var restoredEnemy=FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).Single(e=>e.HP==90);
        Check(Vector2.Distance(restoredEnemy.transform.position,new Vector2(-3,-2))<.01f,"Other combat entities still restore their saved positions");
        CaptureCombat("读档后站位");
        yield return story.DebugChapter(8,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(3);
        Check(Vector3.Distance(FindFirstObjectByType<HeroCombatDriver>().actor.transform.position,expected)<.0001f,"Fresh later chapter uses the same centered perch");
    }
}
#endif
