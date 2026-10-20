#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 只读检查资源及场景引用，不保存或替换用户当前场景。
public static class CannonPrefabVerification
{
    const string Root = "Assets/Resources/CannonAnimations";
    [Serializable] class Report
    {
        public bool success;
        public int checks, spriteSamples;
        public string error, utc = DateTime.UtcNow.ToString("O");
    }
    static Report report;
    static void Require(bool value, string message)
    {
        report.checks++;
        if (!value) throw new InvalidOperationException(message);
    }

    [MenuItem("街垒/检查火炮预制体")]
    public static void Check()
    {
        report = new Report();
        GameObject sample = null;
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "请停止运行后检查。");
            var cannon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/enemy/Cannon.prefab");
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/arrow/CannonShell.prefab");
            Require(cannon != null && shell != null, "火炮或炮弹Prefab没有导入。");
            var driver = cannon.GetComponent<Cannon>();
            var projectile = shell.GetComponent<CannonShell>();
            Require(driver != null && projectile != null, "缺少火炮或炮弹脚本。");
            Require(driver.shellPrefab == projectile && driver.muzzle != null && driver.visualAnimator != null,
                "火炮的炮弹、枪口或Animator引用缺失。");
            Require(driver.targetWall == null, "Prefab不应保存场景Wall引用。");
            Require(Mathf.Approximately(driver.fireInterval, 30f) && Mathf.Approximately(driver.firstShotDelay, 30f)
                && Mathf.Approximately(driver.wallDamageFraction, .25f), "火炮策划参数不匹配。");
            Require(Mathf.Approximately(driver.releaseDelay, 2f / 12f) && driver.shellSpeed == 80f,
                "开炮火光时机或炮弹速度不匹配。");
            Require(Mathf.Approximately(driver.fireBodyScale.x, 55f / 41f)
                && Mathf.Approximately(driver.fireBodyScale.y, 36f / 28f), "开炮本体尺寸没有对齐行走。");
            Require(Mathf.Approximately(driver.deploymentVisibleFraction, .8f), "部署应在大部分炮车入画时停止。");
            Require(cannon.GetComponent<EnemyHP>() == null && shell.GetComponent<BombHP>() == null,
                "火炮不应挤占普通兵配额，炮弹不应参与手榴弹连锁。");
            var body = shell.GetComponent<Rigidbody2D>();
            var collider = shell.GetComponent<CircleCollider2D>();
            Require(body != null && collider != null && collider.isTrigger && body.gravityScale == 0f
                && body.collisionDetectionMode == CollisionDetectionMode2D.Continuous, "炮弹物理配置不符。");
            Require(projectile.HP == 100 && CannonShell.CalculateWallDamage(1000, .25f) == 250,
                "炮弹血量或百分比伤害不符。");
            var controller = driver.visualAnimator.runtimeAnimatorController as AnimatorController;
            Require(controller != null, "火炮AnimatorController缺失。");
            var stateNames = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            Require(new[] { "Idle", "Move", "Fire" }.All(stateNames.Contains), "火炮状态机不完整。");
            sample = new GameObject("CannonClipSample") { hideFlags = HideFlags.HideAndDontSave };
            var renderer = sample.AddComponent<SpriteRenderer>();
            foreach (var action in new[] { "Move", "Fire", "Fly" })
            {
                float ppu = action == "Fly" ? 64f : 32f;
                string clipName = action == "Fly" ? "CannonShell_Fly" : "Cannon_" + action;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Animator/Clips/" + clipName + ".anim");
                Require(clip != null && Mathf.Approximately(clip.frameRate, 12f)
                    && Mathf.Abs(clip.length - 1f) < .002f, "片段时长应为1秒/12fps：" + clipName);
                Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == (action != "Fire"),
                    "循环设置错误：" + clipName);
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Require(bindings.Length == 1, "精灵曲线数量错误：" + clipName);
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
                Require(keys.Length == 12, "实际帧数应为12：" + clipName);
                for (int i = 1; i <= 12; i++)
                {
                    string path = Root + "/Frames/" + action + "/frame_" + i.ToString("00") + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Require(importer != null && sprite != null && sprite.rect.width == 64 && sprite.rect.height == 64
                        && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                        && importer.textureCompression == TextureImporterCompression.Uncompressed
                        && Mathf.Approximately(importer.spritePixelsPerUnit, ppu), "贴图导入不符：" + path);
                    clip.SampleAnimation(sample, (i - 1f + .1f) / 12f);
                    Require(renderer.sprite == sprite, "原生动画采样帧不匹配：" + path);
                    report.spriteSamples++;
                }
            }
            var originalScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/2.unity");
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/2.unity", OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Select(t => t.gameObject).ToArray();
                var walls = objects.Where(g => g.CompareTag(GameManager.WallTag)).ToArray();
                Require(walls.Length == 1 && walls[0].name == "Barricade", "只有街垒应具有Wall标签。");
                Require(Mathf.Approximately(walls[0].transform.localScale.x, -.85f)
                    && Mathf.Approximately(walls[0].transform.localScale.y, .85f), "新版街垒应等比缩放并沿X轴镜像。");
                var polygon = walls[0].GetComponent<PolygonCollider2D>();
                Require(polygon != null && polygon.enabled && !polygon.isTrigger && polygon.pathCount == 1
                    && polygon.GetPath(0).Length == 24, "街垒主体轮廓碰撞体不完整。");
                var spawns = objects.Select(g => g.GetComponent<Spawn>()).Where(s => s != null).ToArray();
                Require(spawns.Length == 3 && spawns.All(s => s.target == walls[0].transform), "三个生成点应瞄准街垒。");
                var legacy = objects.FirstOrDefault(g => g.name == "Wall");
                Require(legacy == null || legacy.GetComponents<Collider2D>().All(c => !c.enabled), "旧Wall碰撞体仍在生效。");
            }
            finally
            {
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            report.success = true;
        }
        catch (Exception e) { report.error = e.ToString(); Debug.LogException(e); }
        finally
        {
            if (sample != null) UnityEngine.Object.DestroyImmediate(sample);
            File.WriteAllText("Library/CannonPrefabVerification.json", JsonUtility.ToJson(report, true));
        }
        if (!report.success) throw new InvalidOperationException(report.error);
        Debug.Log("[CannonPrefabVerification] 火炮资源核验通过：" + report.checks + "项，36帧原生采样。");
    }
}
#endif
