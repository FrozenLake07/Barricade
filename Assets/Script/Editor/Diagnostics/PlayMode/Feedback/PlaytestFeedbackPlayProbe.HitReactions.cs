#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed partial class PlaytestFeedbackPlayProbe
{
    GameObject MovingEnemy(bool leader, bool wave, float x = 0)
    {
        var go = Instantiate(Resources.Load<GameObject>(leader ? "enemy/Leader" : "enemy/Soldier"), new Vector3(x, CapsuleTrack.AttackY, 0), Quaternion.identity);
        var move = go.GetComponent<EnemyMove>();
        move.waveControlled = wave;
        move.speed = .5f;
        move.settleTime = 999;
        return go;
    }

    IEnumerator CheckHitPause(GameObject enemy, string label)
    {
        var body = enemy.GetComponent<Rigidbody2D>();
        var hp = enemy.GetComponent<EnemyHP>();
        var reaction = enemy.GetComponent<CampaignReactionVisual>();
        yield return new WaitForSeconds(.1f);
        float before = body.position.x;
        yield return new WaitForSeconds(.1f);
        Check(body.position.x > before + .01f, label + " moves before impact");
        hp.TakeDamage(10);
        Vector2 stopped = body.position;
        Check(reaction.IsHitPlaying && body.velocity.sqrMagnitude < .000001f && hp.HP == 90, label + " stops immediately on a real nonlethal hit");
        while (reaction.IsHitPlaying)
        {
            yield return new WaitForFixedUpdate();
            if (reaction.IsHitPlaying)
                Check(Vector2.Distance(body.position, stopped) < .005f, label + " remains still throughout the hit animation");
        }

        yield return new WaitForSeconds(.12f);
        Check(body.position.x > stopped.x + .01f, label + " resumes advancing after the hit animation");
    }

    IEnumerator HitStopTest()
    {
        yield return MarkerTest();
        report.coverage = "Actual reticle presentation and real nonlethal enemy hit animations in physics: ordinary/wave soldiers, scheduled arrival, legacy/wave grenadier squads, repeated hits, pause and lethal transition. No full campaign playthrough.";
        yield return story.DebugChapter(3, true);
        CampaignDebug.Value.freeze = false;
        ClearThreats();
        WaveDirector.Current.enabled = false;
        yield return Frames(3);
        var soldier = MovingEnemy(false, false);
        yield return CheckHitPause(soldier, "Ordinary soldier");
        Destroy(soldier);
        yield return Frames(2);
        soldier = MovingEnemy(false, true);
        yield return CheckHitPause(soldier, "Wave soldier");
        var reaction = soldier.GetComponent<CampaignReactionVisual>();
        var hp = soldier.GetComponent<EnemyHP>();
        var body = soldier.GetComponent<Rigidbody2D>();
        hp.TakeDamage(10);
        yield return new WaitForSeconds(.3f);
        hp.TakeDamage(10);
        Check(reaction.IsHitPlaying && Get<float>(reaction, "elapsed") < .03f, "Repeated nonlethal hits restart the hit animation and keep movement paused");
        var stopped = body.position;
        DebugConsole.Current.SetOpen(true);
        yield return Frames(3);
        float age = Get<float>(reaction, "elapsed");
        yield return Frames(5);
        Check(Mathf.Abs(Get<float>(reaction, "elapsed") - age) < .001f && Vector2.Distance(stopped, body.position) < .005f, "Pause freezes both stagger animation and position");
        DebugConsole.Current.SetOpen(false);
        yield return Frames(2);
        hp.TakeDamage(1000);
        Check(hp.HP == 0 && reaction.IsDying && !body.simulated && !soldier.GetComponent<EnemyMove>().enabled, "Lethal hit during stagger changes to death and cannot resume movement");
        Destroy(soldier);
        yield return Frames(2);
        soldier = MovingEnemy(false, true);
        yield return Frames(3);
        var threat = soldier.AddComponent<WaveThreat>();
        double deadline = WaveDirector.Current.Position + .1;
        threat.Initialize(WaveDirector.Current, WaveEventType.Soldier, 2, deadline, "hit-stop-arrival-check");
        reaction = soldier.GetComponent<CampaignReactionVisual>();
        body = soldier.GetComponent<Rigidbody2D>();
        soldier.GetComponent<EnemyHP>().TakeDamage(10);
        stopped = body.position;
        while (reaction.IsHitPlaying)
        {
            yield return new WaitForFixedUpdate();
            Check(soldier != null && !threat.Handled, "Scheduled soldier never resolves a wall hit during its hit animation");
            if (reaction.IsHitPlaying)
                Check(Vector2.Distance(stopped, body.position) < .005f, "Scheduled movement cannot override stagger");
        }

        Check(Get<double>(threat, "arrival") > deadline + .2, "Scheduled arrival is delayed by the stagger instead of catching up immediately");
        Destroy(soldier);
        yield return Frames(2);
        var target = new GameObject("HitStopLegacyWall");
        target.transform.position = new Vector3(10, CapsuleTrack.AttackY, 0);
        int group = Spawn.NewGroupId();
        var leaders = new[]
        {
            MovingEnemy(true, false, -3),
            MovingEnemy(true, false, -4.5f)
        };
        foreach (var leader in leaders)
        {
            var move = leader.GetComponent<EnemyMove>();
            move.leaderAdvanceDistance = 999;
            move.ConfigureSpawn(group, target.transform);
        }

        yield return CheckHitPause(leaders[0], "Legacy grenadier squad member");
        foreach (var leader in leaders)
            Destroy(leader);
        Destroy(target);
        yield return Frames(2);
        float x = WaveDirector.Current.SquadThrowX(2) - 2;
        leaders = new[]
        {
            MovingEnemy(true, true, x),
            MovingEnemy(true, true, x - 1.5f)
        };
        var squad = new GameObject("HitStopWaveSquad").AddComponent<WaveSquad>();
        squad.Initialize(WaveDirector.Current, leaders, 2, 100000, "hit-stop-squad");
        yield return Frames(3);
        body = leaders[1].GetComponent<Rigidbody2D>();
        float healthyStart = body.position.x;
        leaders[0].GetComponent<EnemyHP>().TakeDamage(10);
        reaction = leaders[0].GetComponent<CampaignReactionVisual>();
        leaders[0].GetComponent<EnemyMove>().BeginWaveThrow();
        Check(reaction.IsHitPlaying, "Grenade preparation cannot replace an unfinished hit reaction");
        stopped = leaders[0].GetComponent<Rigidbody2D>().position;
        yield return new WaitForSeconds(.25f);
        Check(Vector2.Distance(stopped, leaders[0].GetComponent<Rigidbody2D>().position) < .005f && body.position.x > healthyStart + .02f, "Wave squad stops only the struck member while the healthy member continues");
        yield return Until(() => !reaction.IsHitPlaying);
        yield return new WaitForSeconds(.12f);
        Check(leaders[0].GetComponent<Rigidbody2D>().position.x > stopped.x + .01f, "Struck wave grenadier resumes after its hit animation");
        foreach (var leader in leaders)
            Destroy(leader);
        Destroy(squad.gameObject);
    }
}
#endif
