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
    void CaptureCombat(string name)
    {
        var camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        target.Create();
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            target.Release();
            Destroy(target);
            Destroy(texture);
        }
    }

    IEnumerator MarkerTest()
    {
        RememberPreferences();
        yield return Frames(4);
        DebugConsole.Current?.SetOpen(false);
        report.coverage = "Actual combat scene and enemy/bomb prefabs; world-camera before/after capture; target selection, switching, reloading and stationary player checks. No pixel-art source images changed.";
        yield return story.DebugChapter(3, true);
        CampaignDebug.Value.freeze = true;
        ClearThreats();
        yield return Frames(3);
        var track = FindFirstObjectByType<CapsuleTrack>();
        var marker = FindFirstObjectByType<TargetMarker>();
        var shooter = FindFirstObjectByType<SBullet>();
        FindFirstObjectByType<TapSec>().CampaignSetAim("Down");
        var enemy = Instantiate(Resources.Load<GameObject>("enemy/Soldier"), new Vector3(0, CapsuleTrack.AttackY, 0), Quaternion.identity);
        enemy.GetComponent<EnemyMove>().waveControlled = true;
        yield return new WaitForSeconds(.3f);
        var contrast = marker.marker.GetComponent<TargetMarkerContrast>();
        var sprite = marker.marker.GetComponent<SpriteRenderer>();
        Check(shooter.CurrentTarget() == enemy.transform && marker.CurrentState == TargetMarker.MarkerState.Ready, "Reticle still selects the same live enemy as firing");
        Check(contrast != null && contrast.HighlightVisible, "High contrast corner frame appears for a selected target");
        contrast.enabled = false;
        yield return Frames(2);
        CaptureCombat("01_原准星");
        contrast.enabled = true;
        yield return Frames(2);
        CaptureCombat("02_增强准星");
        Check(Vector3.Distance(marker.marker.localScale, contrast.OriginalScale * 1.25f) < .001f, "Locked-target reticle is moderately enlarged");
        Check(Vector3.Distance(marker.marker.position, enemy.transform.position + marker.offset) < .001f, "Highlight does not change the target position");
        var frame = marker.marker.GetComponentsInChildren<SpriteRenderer>().First(r => r != sprite);
        Check(frame.sortingLayerID == sprite.sortingLayerID && frame.sortingOrder == sprite.sortingOrder - 1 && frame.sortingOrder > enemy.GetComponent<SpriteRenderer>().sortingOrder, "Sprite-following outline renders above enemies behind the central state sprite");
        Check(marker.marker.GetComponentInChildren<MeshRenderer>() == null && marker.marker.GetComponentsInChildren<SpriteRenderer>().Count(r => r != sprite) == 8, "Separate corner box is removed; only sprite silhouette copies remain");
        Check(frame.GetComponents<Collider2D>().Length == 0, "Visual frame has no collider or damage behavior");
        var playerPosition = track.transform.position;
        var reticlePosition = marker.marker.position;
        track.DebugLane(1);
        yield return null;
        Check(Vector3.Distance(marker.marker.position, reticlePosition) > .0001 && Vector3.Distance(track.transform.position, playerPosition) < .0001, "Switching still moves only the reticle smoothly");
        yield return new WaitForSeconds(.25f);
        Check(marker.CurrentState == TargetMarker.MarkerState.NoTarget && !contrast.HighlightVisible && marker.marker.localScale == contrast.OriginalScale, "No target uses the original idle size and hides the frame");
        track.DebugLane(2);
        yield return new WaitForSeconds(.25f);
        Check(contrast.HighlightVisible, "Target highlight returns when switching back");
        Destroy(enemy);
        yield return Frames(3);
        Check(!contrast.HighlightVisible, "Destroyed targets do not leave a stale highlight");
        var bomb = Bomb(new Vector3(-1, CapsuleTrack.AttackY, 0), Spawn.NewGroupId());
        FindFirstObjectByType<TapSec>().CampaignSetAim("Up");
        yield return new WaitForSeconds(.25f);
        Check(shooter.CurrentTarget() == bomb.transform && contrast.HighlightVisible, "Selected live grenade also receives the contrast frame");
        CaptureCombat("03_手榴弹准星");
        yield return story.DebugChapter(5, true);
        CampaignDebug.Value.freeze = true;
        ClearThreats();
        yield return Frames(3);
        marker = FindFirstObjectByType<TargetMarker>();
        shooter = FindFirstObjectByType<SBullet>();
        FindFirstObjectByType<TapSec>().CampaignSetAim("Down");
        enemy = Instantiate(Resources.Load<GameObject>("enemy/Leader"), new Vector3(0, CapsuleTrack.AttackY, 0), Quaternion.identity);
        enemy.GetComponent<EnemyMove>().waveControlled = true;
        yield return new WaitForSeconds(.25f);
        shooter.DebugAmmo(0);
        story.Clock.Reset(story.Clock.Wait, story.Clock.Window);
        shooter.BeginCampaignReload();
        yield return Frames(2);
        Check(marker.CurrentState == TargetMarker.MarkerState.Reloading && marker.marker.GetComponent<TargetMarkerContrast>().HighlightVisible, "Reload progress remains visible inside the target frame");
        CaptureCombat("04_换弹准星");
        var presentation = marker.marker.GetComponent<TargetMarkerContrast>();
        presentation.enabled = false;
        yield return Frames(2);
        Check(marker.marker.localScale == presentation.OriginalScale && !presentation.HighlightVisible, "Disabling the presentation restores scale and clears the frame");
        presentation.enabled = true;
    }

    IEnumerator LaneTest()
    {
        RememberPreferences();
        yield return Frames(4);
        DebugConsole.Current?.SetOpen(false);
        StartCoroutine(story.DebugChapter(3, true));
        yield return Until(() => story.Phase == CampaignPhase.Defense && WaveDirector.Current != null);
        yield return Frames(3);
        CampaignDebug.Value.freeze = true;
        ClearThreats();
        yield return Frames(2);
        var track = FindFirstObjectByType<CapsuleTrack>();
        var target = FindFirstObjectByType<TargetMarker>();
        Check(track != null && target != null && target.marker != null, "Actual defense player and reticle exist");
        Vector3 playerPosition = track.transform.position;
        foreach (int lane in new[]
        {
            1,
            3,
            2,
            1,
            2,
            3
        }

        )
        {
            Vector3 start = target.marker.position;
            track.DebugLane(lane);
            Check(CapsuleTrack.AttackIndex == lane - 1 && CapsuleTrack.IsSwitching, "Logical lane changes and shooting settles: " + lane);
            yield return null;
            Vector3 destination = new Vector3(track.transform.position.x - target.noTargetDistance, CapsuleTrack.AttackY, track.transform.position.z) + target.offset;
            Check(Vector3.Distance(target.marker.position, start) > 0.0001f && Vector3.Distance(target.marker.position, destination) > 0.0001f, "Reticle moves partway toward the new lane: " + lane);
            float end = Time.time + .25f;
            while (Time.time < end)
            {
                Check(Vector3.Distance(track.transform.position, playerPosition) < .0001f, "Player stays fixed during lane " + lane);
                yield return null;
            }

            Check(!CapsuleTrack.IsSwitching && Vector3.Distance(target.marker.position, destination) < .001f, "Reticle reaches lane and adjustment finishes: " + lane);
        }

        track.DebugLane(1);
        yield return null;
        Vector3 before = target.marker.position;
        track.DebugLane(3);
        Check(Vector3.Distance(target.marker.position, before) < .0001f, "Rapid reverse starts from current reticle position without teleporting");
        yield return new WaitForSeconds(.25f);
        Check(Vector3.Distance(track.transform.position, playerPosition) < .0001f && CapsuleTrack.AttackIndex == 2, "Rapid reverse leaves player fixed and final lane correct");
    }
}
#endif
