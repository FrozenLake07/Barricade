#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only diagnosis, available from the menu without background captures.
public static class HeroScene2VisualDiagnosis
{
    [Serializable] sealed class Report
    {
        public bool success, playing;
        public string utc = DateTime.UtcNow.ToString("O"), error, motion, sprite, shader;
        public int checkedSprites, opaquePixelsOutsideMesh, textureWidth, textureHeight;
        public int cameraWidth, cameraHeight, qualityAA;
        public bool cameraHDR, cameraMSAA;
        public float orthographicSize, screenPixelsPerArtPixel;
        public Vector3 actorScale, position, viewportMin, viewportMax, opaqueViewportMin, opaqueViewportMax;
        public string gameViewTarget, gameViewZoom, cameraCapture;
        public List<string> intersectingRenderers = new List<string>();
    }
    const string ReportPath = "Library/HeroScene2VisualDiagnosis.json";
    [MenuItem("街垒/动画/诊断场景2主角显示")]
    public static void Diagnose()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/2.unity");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[HeroScene2VisualDiagnosis] 请先打开场景2。");
            return;
        }
        Run(scene);
    }

    static void Run(Scene scene)
    {
        var report = new Report { playing = EditorApplication.isPlaying };
        try
        {
            HeroActor actor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HeroActor>(true)).FirstOrDefault();
            if (actor == null || actor.visual == null || actor.visual.sprite == null) throw new InvalidOperationException("Scene2 hero not ready");
            SpriteRenderer visual = actor.visual;
            Sprite sprite = visual.sprite;
            report.sprite = AssetDatabase.GetAssetPath(sprite);
            report.motion = actor.CurrentMotion.ToString();
            report.position = visual.transform.position; report.actorScale = visual.transform.lossyScale;
            report.shader = visual.sharedMaterial != null ? visual.sharedMaterial.shader.name : "<no material>";
            report.textureWidth = sprite.texture.width; report.textureHeight = sprite.texture.height;
            foreach (HeroAnimationLibrary.Entry entry in actor.library.actions)
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(entry.clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                foreach (var key in keys) { CheckMesh((Sprite)key.value, report); report.checkedSprites++; }
            }
            foreach (SpriteRenderer other in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true)))
                if (other != visual && other.enabled && other.gameObject.activeInHierarchy && other.bounds.Intersects(visual.bounds))
                    report.intersectingRenderers.Add(other.name + " layer=" + other.sortingLayerName + " order=" + other.sortingOrder);

            Camera camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.enabled);
            if (camera == null) throw new InvalidOperationException("Scene2 camera missing");
            report.cameraWidth = camera.pixelWidth; report.cameraHeight = camera.pixelHeight;
            report.cameraHDR = camera.allowHDR; report.cameraMSAA = camera.allowMSAA;
            report.qualityAA = QualitySettings.antiAliasing; report.orthographicSize = camera.orthographicSize;
            report.screenPixelsPerArtPixel = camera.pixelHeight / (2f * camera.orthographicSize * sprite.pixelsPerUnit) * visual.transform.lossyScale.y;
            report.viewportMin = camera.WorldToViewportPoint(visual.bounds.min);
            report.viewportMax = camera.WorldToViewportPoint(visual.bounds.max);
            ReadOpaqueViewport(camera, visual, report);
            ReadGameView(report);
            report.cameraCapture = CaptureCamera(camera);
            if (EditorApplication.isPlaying) ScreenCapture.CaptureScreenshot("Library/HeroScene2GameCapture.png");
            report.success = true;
            Debug.Log("[HeroScene2VisualDiagnosis] Scene2 visual report: " + ReportPath);
        }
        catch (Exception error) { report.error = error.ToString(); Debug.LogError("[HeroScene2VisualDiagnosis] " + error); }
        finally { File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); }
    }

    static void CheckMesh(Sprite sprite, Report report)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            Color32[] colors = texture.GetPixels32();
            Vector2[] vertices = sprite.vertices; ushort[] indices = sprite.triangles;
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (colors[y * texture.width + x].a == 0) continue;
                    Vector2 point = (new Vector2(x + .5f, y + .5f) - sprite.pivot) / sprite.pixelsPerUnit;
                    bool inside = false;
                    for (int t = 0; t < indices.Length; t += 3)
                        if (Inside(point, vertices[indices[t]], vertices[indices[t + 1]], vertices[indices[t + 2]])) { inside = true; break; }
                    if (!inside) report.opaquePixelsOutsideMesh++;
                }
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static void ReadOpaqueViewport(Camera camera, SpriteRenderer visual, Report report)
    {
        Sprite sprite = visual.sprite;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            Color32[] pixels = texture.GetPixels32();
            Vector3 minimum = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maximum = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a == 0) continue;
                    Vector3 local = (new Vector3(x + .5f, y + .5f, 0f) - (Vector3)sprite.pivot) / sprite.pixelsPerUnit;
                    if (visual.flipX) local.x = -local.x;
                    if (visual.flipY) local.y = -local.y;
                    Vector3 viewport = camera.WorldToViewportPoint(visual.transform.TransformPoint(local));
                    minimum = Vector3.Min(minimum, viewport); maximum = Vector3.Max(maximum, viewport);
                }
            report.opaqueViewportMin = minimum; report.opaqueViewportMax = maximum;
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float x = Cross(b - a, p - a), y = Cross(c - b, p - b), z = Cross(a - c, p - c);
        return !((x < -.00001f || y < -.00001f || z < -.00001f) && (x > .00001f || y > .00001f || z > .00001f));
    }
    static void ReadGameView(Report report)
    {
        try
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            UnityEngine.Object view = Resources.FindObjectsOfTypeAll(type).FirstOrDefault();
            if (view == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            report.gameViewTarget = type.GetProperty("targetSize", flags)?.GetValue(view, null)?.ToString();
            object zoom = type.GetField("m_ZoomArea", flags)?.GetValue(view);
            if (zoom != null) report.gameViewZoom = zoom.GetType().GetProperty("scale", flags)?.GetValue(zoom, null)?.ToString();
        }
        catch (Exception error) { report.gameViewZoom = error.Message; }
    }
    static string CaptureCamera(Camera camera)
    {
        int width = Mathf.Max(1, camera.pixelWidth), height = Mathf.Max(1, camera.pixelHeight);
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        const string path = "Library/HeroScene2CameraCapture.png";
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.Apply();
            File.WriteAllBytes(path, result.EncodeToPNG()); return path;
        }
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(result);
        }
    }
}
#endif
