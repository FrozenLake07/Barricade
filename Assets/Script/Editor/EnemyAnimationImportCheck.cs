#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor diagnostics only read existing gameplay objects. No MonoBehaviour is
// attached from an Editor assembly and no sprite geometry is changed here.
[InitializeOnLoad]
public static class EnemyAnimationImportCheck
{
    static readonly string ResultPath = Path.Combine(Path.GetTempPath(), "StreetBarricadeEnemyAnimationImportCheck.json");
    [Serializable] sealed class Report
    {
        public bool success, pending, nativeGeometryChecked;
        public string stage, unityVersion, utc, error, note;
        public int checks, verticesChecked;
    }
    static int checks;
    static bool nativeProbeQueued, nativeValidationRun;
    static double nextPoll, firstUnready = -1d;

    static EnemyAnimationImportCheck()
    {
        EditorApplication.delayCall += RunWhenReady;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                nativeProbeQueued = nativeValidationRun = false;
                QueueNativeValidation(false);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                StopPolling();
                nativeValidationRun = false;
                EditorApplication.delayCall += RunWhenReady;
            }
        };
    }

    static void RunWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunWhenReady;
            return;
        }
        if (Application.isPlaying) QueueNativeValidation(false);
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) ValidateAssetsOnly();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Near(Vector2 a, Vector2 b, float tolerance = .0001f) =>
        Finite(a.x) && Finite(a.y) && Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance;

    static Report NewReport(string stage) => new Report
    {
        stage = stage, unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O")
    };

    static void SaveRuntimeReport(Report report)
    {
        try { File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true)); }
        catch (Exception error) { Debug.LogWarning("[EnemyAnimationImportCheck] 无法写入检查报告：" + error.Message); }
    }

    static void StopPolling()
    {
        EditorApplication.update -= PollLiveGrenadier;
        nativeProbeQueued = false;
        firstUnready = -1d;
    }

    static void QueueNativeValidation(bool force)
    {
        if (!Application.isPlaying || nativeProbeQueued || (!force && nativeValidationRun)) return;
        nativeProbeQueued = true;
        nativeValidationRun = false;
        nextPoll = 0d;
        firstUnready = -1d;
        EditorApplication.update -= PollLiveGrenadier;
        EditorApplication.update += PollLiveGrenadier;
        var report = NewReport("RuntimePending");
        report.pending = true;
        report.note = "等待场景中首个掷弹兵生成，再读取实际网格；不会创建或修改游戏物体。";
        SaveRuntimeReport(report);
        Debug.Log("[EnemyAnimationImportCheck] 掷弹兵生成后会自动检查实际网格。");
    }

    static void PollLiveGrenadier()
    {
        if (!Application.isPlaying) { StopPolling(); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + .25d;
        try
        {
            GrenadierOriginalRig[] rigs = UnityEngine.Object.FindObjectsByType<GrenadierOriginalRig>(FindObjectsSortMode.None);
            GrenadierOriginalRig ready = Array.Find(rigs, rig => rig != null && rig.isActiveAndEnabled && rig.IsReady);
            if (ready == null)
            {
                bool foundActive = Array.Exists(rigs, rig => rig != null && rig.isActiveAndEnabled);
                if (!foundActive) { firstUnready = -1d; return; }
                if (firstUnready < 0d) { firstUnready = EditorApplication.timeSinceStartup; return; }
                if (EditorApplication.timeSinceStartup - firstUnready < 2d) return;
                throw new InvalidOperationException("已生成掷弹兵，但动画网格在两秒后仍未完成初始化。请检查之前的GrenadierOriginalRig报错。");
            }
            checks = 0;
            int vertices = ValidateLiveGeometry(ready);
            var report = NewReport("RuntimeLiveReadOnly");
            report.success = report.nativeGeometryChecked = true;
            report.checks = checks;
            report.verticesChecked = vertices;
            report.note = "已读取实际掷弹兵的原生顶点、UV和索引；未改变动画、角色或场景。";
            SaveRuntimeReport(report);
            nativeValidationRun = true;
            StopPolling();
            Debug.Log("[EnemyAnimationImportCheck] 实际掷弹兵网格检查通过：" + checks + "项，" + vertices + "个顶点。");
        }
        catch (Exception error)
        {
            var report = NewReport("RuntimeLiveReadOnly");
            report.error = error.ToString();
            report.checks = checks;
            SaveRuntimeReport(report);
            nativeValidationRun = true;
            StopPolling();
            Debug.LogError("[EnemyAnimationImportCheck] " + report.error);
        }
    }

    static int ValidateLiveGeometry(GrenadierOriginalRig rig)
    {
        Require(rig != null && rig.IsReady && rig.sourceTexture != null && rig.rigData != null, "掷弹兵原图或拆分数据缺失");
        var definition = JsonUtility.FromJson<GrenadierOriginalRig.Definition>(rig.rigData.text);
        Require(definition != null && definition.parts != null && definition.parts.Length == 7 && definition.ppu > 0f,
            "实际掷弹兵的部件数据无效");
        int totalVertices = 0;
        foreach (var part in definition.parts)
        {
            Transform piece = rig.transform.Find("Rig_" + part.name);
            SpriteRenderer renderer = piece != null ? piece.GetComponent<SpriteRenderer>() : null;
            Require(renderer != null && renderer.sprite != null, part.name + " 部件不存在或缺少精灵");
            Sprite sprite = renderer.sprite;
            Vector2[] vertices = sprite.vertices, uv = sprite.uv;
            ushort[] triangles = sprite.triangles;
            Require(sprite.texture == rig.sourceTexture && vertices.Length == part.cells.Length * 4
                && uv.Length == vertices.Length && triangles.Length == part.cells.Length * 6, part.name + " 网格数量或原图引用不匹配");
            Require(Mathf.Abs(sprite.pixelsPerUnit - definition.ppu) <= .0001f
                && Near(sprite.pivot, new Vector2(part.px, definition.height - part.py)), part.name + " PPU或Pivot不匹配");
            bool coordinatesMatch = true, uvMatch = true, indexMatch = true;
            for (int i = 0; i < part.cells.Length; i++)
            {
                var cell = part.cells[i];
                Vector2[] expected = {
                    new Vector2(cell.x, definition.height - cell.y - definition.grid),
                    new Vector2(cell.x + definition.grid, definition.height - cell.y - definition.grid),
                    new Vector2(cell.x + definition.grid, definition.height - cell.y),
                    new Vector2(cell.x, definition.height - cell.y)
                };
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = i * 4 + corner;
                    coordinatesMatch &= Near(vertices[index] * sprite.pixelsPerUnit + sprite.pivot, expected[corner]);
                    uvMatch &= Near(uv[index], new Vector2(expected[corner].x / rig.sourceTexture.width,
                        expected[corner].y / rig.sourceTexture.height), .000001f);
                }
                int v = i * 4, t = i * 6;
                int[] expectedIndices = { v, v + 1, v + 2, v, v + 2, v + 3 };
                for (int j = 0; j < 6; j++) indexMatch &= triangles[t + j] == expectedIndices[j];
            }
            Require(coordinatesMatch, part.name + " 实际网格坐标与原图像素位置不一致");
            Require(uvMatch, part.name + " 实际网格UV没有采样原图对应位置");
            Require(indexMatch, part.name + " 实际网格索引与原图格连接不一致");
            totalVertices += vertices.Length;
        }
        return totalVertices;
    }

    static void ValidateAssetsOnly()
    {
        checks = 0;
        var report = new Report { stage = "AssetsOnly", unityVersion = Application.unityVersion, utc = DateTime.UtcNow.ToString("O") };
        try
        {
            foreach (string mode in new[] { "Walk", "Fire" })
            {
                string asset = "Assets/Resources/EnemyAnimations/Soldier_" + mode + ".png";
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(asset).OfType<Sprite>().ToArray();
                Require(frames.Length == 8, mode + " 应导入8帧");
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                Require(importer != null && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                    && importer.textureCompression == TextureImporterCompression.Uncompressed, mode + " 像素导入设置不正确");
                Require(Resources.LoadAll<Sprite>("EnemyAnimations/Soldier_" + mode).Length == 8, mode + " Resources读取不完整");
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/EnemyAnimations/Clips/Soldier_" + mode + ".anim");
                Require(clip != null, mode + " 原生动画片段缺失");
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First(b => b.propertyName == "m_Sprite");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Require(keys.Length >= 8 && keys.All(k => k.value != null && frames.Contains(k.value as Sprite)), mode + " 动画片段有空帧引用");
            }
            Texture2D source = Resources.Load<Texture2D>("EnemyAnimations/Grenadier_Original");
            Texture2D grenade = Resources.Load<Texture2D>("EnemyAnimations/Grenade_Original");
            TextAsset data = Resources.Load<TextAsset>("EnemyAnimations/GrenadierRig");
            Require(source != null && grenade != null && data != null, "掷弹兵资源缺失");
            var definition = JsonUtility.FromJson<GrenadierOriginalRig.Definition>(data.text);
            Require(definition != null && definition.width == source.width && definition.height == source.height
                && definition.ppu > 0f && definition.parts != null && definition.parts.Length == 7, "掷弹兵图片/部件数据不匹配");
            foreach (var part in definition.parts)
            {
                GrenadierOriginalRig.CreatePartGeometry(definition, part, out Vector2[] vertices, out ushort[] triangles);
                Require(vertices.All(v => Finite(v.x) && Finite(v.y) && v.x >= 0f && v.x <= definition.width
                    && v.y >= 0f && v.y <= definition.height), part.name + " 输入像素坐标越界");
                Require(triangles.Length == part.cells.Length * 6 && triangles.All(t => t < vertices.Length), part.name + " 索引无效");
            }
            report.success = true;
        }
        catch (Exception error) { report.error = error.ToString(); }
        report.checks = checks;
        string assetReportPath = Path.Combine(Path.GetTempPath(), "StreetBarricadeEnemyAnimationAssetsCheck.json");
        try { File.WriteAllText(assetReportPath, JsonUtility.ToJson(report, true)); }
        catch (Exception error) { Debug.LogWarning("[EnemyAnimationImportCheck] 无法写入资源检查报告：" + error.Message); }
        if (report.success)
            Debug.Log("[EnemyAnimationImportCheck] 资源和像素坐标检查通过；进入游戏后，首次掷弹兵生成时检查其实际网格。");
        else Debug.LogError("[EnemyAnimationImportCheck] " + report.error);
    }

    [MenuItem("街垒/检查敌人动画导入")]
    public static void Validate()
    {
        if (Application.isPlaying) QueueNativeValidation(true);
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) ValidateAssetsOnly();
    }
}
#endif
