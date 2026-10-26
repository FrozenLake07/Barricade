#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator PersistenceTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        report.coverage="In-memory capture, JSON roundtrip and actual scene/world restoration using the existing save schema. User save slots are not written.";
        yield return story.DebugChapter(5,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(3);
        var lane=FindFirstObjectByType<CapsuleTrack>();lane.DebugLane(1);yield return new WaitForSeconds(.25f);
        var enemy=MovingEnemy(false,true);enemy.name="RefactorSaveEnemy";
        var threat=enemy.AddComponent<WaveThreat>();threat.Initialize(WaveDirector.Current,WaveEventType.Soldier,1,WaveDirector.Current.Position+20,"refactor-save");
        enemy.GetComponent<EnemyHP>().TakeDamage(10);yield return Frames(2);
        GameManager.WallHP=347;
        var saved=story.CaptureSavedGame();
        Check(saved.version==2&&saved.scene==CampaignSceneNames.Defense&&saved.chapter==5,"Existing save format and numeric scene identity are unchanged");
        Check(saved.world.components.Any(c=>c.type==typeof(EnemyMove).FullName)&&saved.world.components.Any(c=>c.type==typeof(CampaignReactionVisual).FullName),"Existing type names remain in captured world data");
        string identity=saved.configurationId;float cursor=saved.musicTime;
        var clone=JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(saved));clone.Validate();
        yield return story.RestoreSavedGame(clone);yield return Frames(3);
        Check(story.Phase==CampaignPhase.Defense&&RunState.Chapter==5&&GameManager.WallHP==347,"Actual restore reloads the defense scene, chapter and saved wall health");
        Check(CapsuleTrack.AttackIndex==0,"Saved player attack lane is restored");
        var restored=FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).Single(e=>e.HP==90);
        Check(restored.GetComponent<CampaignReactionVisual>().IsHitPlaying&&restored.GetComponent<Rigidbody2D>().velocity.sqrMagnitude<.000001f,"Saved nonlethal reaction and stopped body survive scene/world restoration");
        Check(restored.GetComponent<WaveThreat>()!=null,"Scheduled threat component survives restoration");
        var music=Get<AudioSource>(story,"audioSource");
        Check(music.clip==story.table.chapters[4].openingMusic&&Math.Abs(music.time-cursor)<.2f,"Saved music identity and cursor are restored");
        Check(story.CaptureSavedGame().configurationId==identity,"Configuration fingerprint stays compatible after the refactor");
    }
}
#endif
