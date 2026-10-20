#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 配置第二幕美术及街垒实体判定，轨道、角色和音频参数保持现状。
public static class Scene2EnvironmentSetup
{
    const string Folder = "Assets/Environment/Act2";
    const string Output = "Library/Scene2EnvironmentSetup";
    [Serializable] class Report
    {
        public bool success;
        public string pipeline, error, scene = "Assets/Scenes/2.unity";
        public float pixelsPerUnit;
        public string[] layers;
        public bool wallCollisionMigrated, trackPositionsPreserved;
    }
    static string DetectPipeline()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null) return "BuiltIn";
        return pipeline.GetType().FullName;
    }

    [MenuItem("街垒/场景/配置第二幕街景")]
    public static void Apply()
    {
        var report = new Report();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在停止运行后配置场景。");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            report.pipeline = DetectPipeline();
            var scene = EditorSceneManager.OpenScene(report.scene, OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) throw new InvalidOperationException("场景2需要正交主摄像机。");
            var wall = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Wall");
            var tracks = new[] { "S1", "S2", "S3" }.Select(n => GameObject.Find(n).transform).ToArray();
            var positions = tracks.Select(t => t.position).ToArray();
            report.pixelsPerUnit = 941f / (camera.orthographicSize * 2f);
            var names = new[] { "Cobblestone", "Buildings", "Barricade" };
            var sprites = names.Select(n => Import(Folder + "/" + n + ".png", report.pixelsPerUnit)).ToArray();
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Act2Environment");
            if (root == null) root = new GameObject("Act2Environment");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            int[] orders = { -30, -20, 2 };
            for (int i = 0; i < names.Length; i++)
            {
                var child = root.transform.Find(names[i]);
                var go = child == null ? new GameObject(names[i]) : child.gameObject;
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
                // 新建筑图顶部有54像素透明留白，上提52像素使街边高度接近旧版。
                if (names[i] == "Buildings") go.transform.position += Vector3.up * (52f / report.pixelsPerUnit);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var renderer = go.GetComponent<SpriteRenderer>() ?? go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.color = Color.white;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = orders[i];
                go.SetActive(true);
            }
            var barricade = root.transform.Find("Barricade").gameObject;
            // PNG含大量左侧透明留白；绕实体轮廓中心镜像，保持街垒在画面右侧。
            var barricadeSprite = barricade.GetComponent<SpriteRenderer>().sprite;
            float bodyCenterX = ((BarricadePixelOutline.Min(v => v.x) + BarricadePixelOutline.Max(v => v.x)) * .5f
                - barricadeSprite.pivot.x) / barricadeSprite.pixelsPerUnit;
            const float artScale = .85f;
            float preservedCenterX = camera.transform.position.x + (1240.5f - barricadeSprite.pivot.x) / barricadeSprite.pixelsPerUnit;
            float groundY = camera.transform.position.y - camera.orthographicSize + 2f / barricadeSprite.pixelsPerUnit;
            float footLocalY = (barricadeSprite.rect.height - 930f - barricadeSprite.pivot.y) / barricadeSprite.pixelsPerUnit;
            barricade.transform.position = new Vector3(preservedCenterX + bodyCenterX * artScale,
                groundY - footLocalY * artScale, 0f);
            barricade.transform.localScale = new Vector3(-artScale, artScale, artScale);
            ApplyWallCollision(barricade, wall);
            var healthVisual=barricade.GetComponent<BarricadeHealthVisual>() ?? barricade.AddComponent<BarricadeHealthVisual>();
            var first=Import(Folder+"/Barricade_Stage1.png",report.pixelsPerUnit,new Vector2(0.4883373206f,0.4893730074f));
            var second=Import(Folder+"/Barricade_Stage2.png",report.pixelsPerUnit,new Vector2(0.4856373429f,0.4893730074f));
            healthVisual.Configure(first,second,barricadeSprite);
            report.wallCollisionMigrated = barricade.CompareTag(GameManager.WallTag)
                && barricade.GetComponent<PolygonCollider2D>() != null
                && (wall == null || !wall.CompareTag(GameManager.WallTag));
            report.trackPositionsPreserved = tracks.Select((t, i) => t.position == positions[i]).All(v => v);
            if (!report.wallCollisionMigrated || !report.trackPositionsPreserved)
                throw new InvalidOperationException("街垒判定迁移失败或轨道发生意外改动。");
            report.layers = names.Select((n, i) => n + ": sortingOrder=" + orders[i]).ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("场景保存失败。");
            AssetDatabase.SaveAssets();
            Capture(camera);
            report.success = true;
        }
        catch (Exception e) { report.error = e.ToString(); Debug.LogException(e); }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/配置核验.json", JsonUtility.ToJson(report, true));
        if (!report.success) throw new InvalidOperationException(report.error);
        Debug.Log("[Scene2EnvironmentSetup] 第二幕三层街景配置完成。");
    }

    // 沿石块、木板主体近似取轮廓，不把旗帜和透明画布当作实体墙。
    public static readonly Vector2[] BarricadePixelOutline = {
        new Vector2(1086,145), new Vector2(1176,115), new Vector2(1220,242),
        new Vector2(1356,350), new Vector2(1376,500), new Vector2(1408,650),
        new Vector2(1469,800), new Vector2(1527,880), new Vector2(1535,900),
        new Vector2(1510,930), new Vector2(1072,930), new Vector2(1001,920),
        new Vector2(993,900), new Vector2(1045,850), new Vector2(1003,800),
        new Vector2(1015,700), new Vector2(1038,650), new Vector2(1022,600),
        new Vector2(993,550), new Vector2(1013,450), new Vector2(1017,350),
        new Vector2(1024,260), new Vector2(1038,220), new Vector2(1045,180)
    };

    public static void ApplyWallCollision(GameObject barricade, GameObject oldWall)
    {
        var sprite = barricade.GetComponent<SpriteRenderer>().sprite;
        barricade.tag = GameManager.WallTag;
        var polygon = barricade.GetComponent<PolygonCollider2D>() ?? barricade.AddComponent<PolygonCollider2D>();
        polygon.pathCount = 1;
        polygon.SetPath(0, BarricadePixelOutline.Select(v => new Vector2(
            (v.x - sprite.pivot.x) / sprite.pixelsPerUnit,
            (sprite.rect.height - v.y - sprite.pivot.y) / sprite.pixelsPerUnit)).ToArray());
        polygon.isTrigger = false;
        polygon.enabled = true;
        if (oldWall != null)
        {
            oldWall.tag = "Untagged";
            foreach (var collider in oldWall.GetComponents<Collider2D>()) collider.enabled = false;
            var renderer = oldWall.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = false;
            oldWall.SetActive(false);
        }
        foreach (var spawn in UnityEngine.Object.FindObjectsByType<Spawn>(FindObjectsSortMode.None))
            if (spawn.gameObject.scene == barricade.scene) spawn.target = barricade.transform;
    }

    static Sprite Import(string path, float ppu, Vector2? pivot = null)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.spritePivot = pivot ?? new Vector2(.5f, .5f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot ?? new Vector2(.5f,.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("无法导入：" + path);
        return sprite;
    }

    static void Capture(Camera camera)
    {
        var targetBefore = camera.targetTexture;
        var activeBefore = RenderTexture.active;
        float aspectBefore = camera.aspect;
        var hero = GameObject.Find("Capsule");
        var heroBefore = hero.transform.position;
        var rt = RenderTexture.GetTemporary(1672, 941, 24);
        Texture2D image = null;
        try
        {
            // 预览按实际开局的中轨站位采样，预览完还原，不把运行时位置写回场景。
            var p = heroBefore; p.y = GameObject.Find("S2").transform.position.y; hero.transform.position = p;
            foreach (var animator in hero.GetComponentsInChildren<Animator>()) { animator.Rebind(); animator.Update(0f); }
            camera.aspect = 1672f / 941f;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image = new Texture2D(1672, 941, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1672, 941), 0, 0);
            image.Apply();
            Directory.CreateDirectory(Output);
            File.WriteAllBytes(Output + "/第二幕场景预览.png", image.EncodeToPNG());
        }
        finally
        {
            hero.transform.position = heroBefore;
            camera.targetTexture = targetBefore;
            camera.aspect = aspectBefore;
            RenderTexture.active = activeBefore;
            RenderTexture.ReleaseTemporary(rt);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
#endif
