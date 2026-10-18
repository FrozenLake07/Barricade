using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 每个部件仍采样原图的像素；不重新绘制脸、帽子和衣服。
[DisallowMultipleComponent]
public sealed class GrenadierOriginalRig : MonoBehaviour
{
    public enum ActionKind { Stand, Walk, Fire, Throw }
    public Texture2D sourceTexture, grenadeTexture;
    public TextAsset rigData;

    [Serializable] public class Cell { public int x, y; }
    [Serializable] public class Part { public string name; public float px, py; public int order; public Cell[] cells; }
    [Serializable] public class PartPose { public string name; public float x, y, angle; }
    [Serializable] public class Pose { public PartPose[] parts; public bool grenade, flash; public float grenadeX, grenadeY, flashX, flashY; }
    [Serializable] public class Motion { public string name; public float fps; public Pose[] frames; }
    [Serializable] public class Definition { public float ppu; public int width, height, grid; public Part[] parts; public Motion[] motions; }

    Definition definition;
    readonly Dictionary<string, Transform> pieces = new Dictionary<string, Transform>();
    readonly List<Sprite> generatedSprites = new List<Sprite>();
    readonly List<GameObject> generatedObjects = new List<GameObject>();
    SpriteRenderer originalRenderer, grenadeRenderer, flashRenderer, flashCore;
    SortingGroup ownedSortingGroup;
    bool originalEnabled, hiddenOriginal;
    Vector3 visualOffset;
    public bool IsReady => definition != null && pieces.Count > 0;

    public void Initialize(Texture2D source, Texture2D grenade, TextAsset data, Vector3 offset)
    {
        ClearRig();
        sourceTexture = source;
        grenadeTexture = grenade;
        rigData = data;
        visualOffset = offset;
        enabled = true;
        if (!IsReady) BuildRig();
    }

    void OnEnable()
    {
        if (Application.isPlaying && sourceTexture != null && rigData != null && !IsReady) BuildRig();
    }

    void BuildRig()
    {
        if (sourceTexture == null || rigData == null)
        {
            Debug.LogWarning("[GrenadierOriginalRig] 缺少原图或动作数据，保留基础外观。");
            return;
        }
        try
        {
            Definition data = JsonUtility.FromJson<Definition>(rigData.text);
            if (data == null || data.ppu <= 0f || data.grid <= 0 || data.width != sourceTexture.width
                || data.height != sourceTexture.height || data.parts == null || data.parts.Length == 0
                || data.motions == null || data.motions.Length == 0)
                throw new InvalidOperationException("原图和动作数据不匹配");
            foreach (Part part in data.parts)
                if (part == null || string.IsNullOrEmpty(part.name) || part.cells == null
                    || part.cells.Length == 0 || part.cells.Length > 16383)
                    throw new InvalidOperationException("无效的角色部件");
            foreach (Motion motion in data.motions)
                if (motion == null || motion.fps <= 0f || motion.frames == null || motion.frames.Length == 0)
                    throw new InvalidOperationException("无效的动作帧");
            definition = data;
            originalRenderer = GetComponent<SpriteRenderer>();
            originalEnabled = originalRenderer != null && originalRenderer.enabled;
            int sorting = originalRenderer != null ? originalRenderer.sortingOrder : 4;
            // 将一个角色整体排序，防止不同敌人的胳膊和身体互相穿插。
            if (ownedSortingGroup != null) ownedSortingGroup.enabled = true;
            else if (GetComponent<SortingGroup>() == null)
            {
                ownedSortingGroup = gameObject.AddComponent<SortingGroup>();
                ownedSortingGroup.sortingOrder = sorting;
                if (originalRenderer != null) ownedSortingGroup.sortingLayerID = originalRenderer.sortingLayerID;
            }
            foreach (Part part in definition.parts)
            {
                Sprite sprite = Sprite.Create(sourceTexture, new Rect(0, 0, data.width, data.height),
                    new Vector2(part.px / data.width, 1f - part.py / data.height), data.ppu, 0, SpriteMeshType.FullRect);
                generatedSprites.Add(sprite);
                CreatePartGeometry(data, part, out Vector2[] vertices, out ushort[] triangles);
                sprite.OverrideGeometry(vertices, triangles);
                // 此 API 在部分失败情况下只记 Unity Error、不抛 C# 异常，须确认实际结果。
                if (sprite.vertices.Length != vertices.Length || sprite.triangles.Length != triangles.Length)
                    throw new InvalidOperationException("部件网格未成功应用：" + part.name);
                pieces.Add(part.name, MakeRenderer(part.name, sprite, part.order).transform);
            }
            if (grenadeTexture != null)
            {
                Sprite sprite = Sprite.Create(grenadeTexture, new Rect(0, 0, grenadeTexture.width, grenadeTexture.height),
                    new Vector2(37f / 64f, .5f), 256f, 0, SpriteMeshType.FullRect);
                generatedSprites.Add(sprite);
                grenadeRenderer = MakeRenderer("HeldGrenade", sprite, 5);
            }
            Sprite square = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), 1f);
            generatedSprites.Add(square);
            flashRenderer = MakeRenderer("MuzzleFlash", square, 6);
            flashRenderer.color = new Color(.85f, .57f, .16f, 1f);
            flashRenderer.transform.localScale = new Vector3(.018f, .007f, 1f);
            flashCore = MakeRenderer("MuzzleFlashCore", square, 7);
            flashCore.color = new Color(.94f, .88f, .71f, 1f);
            flashCore.transform.localScale = new Vector3(.008f, .004f, 1f);
            ApplyInitialPose();
            if (originalRenderer != null) { originalRenderer.enabled = false; hiddenOriginal = true; }
        }
        catch (Exception error)
        {
            ClearRig();
            Debug.LogWarning("[GrenadierOriginalRig] 动作初始化失败，保留基础外观：" + error.Message);
        }
    }

    // OverrideGeometry 接收 Sprite Rect 内的像素坐标；Pivot 和 PPU 由 Unity 自动处理。
    // https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Sprite.OverrideGeometry.html
    public static void CreatePartGeometry(Definition data, Part part, out Vector2[] vertices, out ushort[] triangles)
    {
        if (data == null || data.width <= 0 || data.height <= 0 || data.grid <= 0
            || part == null || part.cells == null || part.cells.Length == 0 || part.cells.Length > 16383)
            throw new InvalidOperationException("无效的部件网格数据");
        vertices = new Vector2[part.cells.Length * 4];
        triangles = new ushort[part.cells.Length * 6];
        for (int i = 0; i < part.cells.Length; i++)
        {
            Cell c = part.cells[i];
            if (c == null || c.x < 0 || c.y < 0 || c.x > data.width - data.grid || c.y > data.height - data.grid)
                throw new InvalidOperationException("部件像素超出原图：" + part.name + "，第" + i + "格");
            int v = i * 4, t = i * 6;
            float left = c.x, right = c.x + data.grid;
            float top = data.height - c.y, bottom = top - data.grid;
            vertices[v] = new Vector2(left, bottom);
            vertices[v + 1] = new Vector2(right, bottom);
            vertices[v + 2] = new Vector2(right, top);
            vertices[v + 3] = new Vector2(left, top);
            triangles[t] = (ushort)v; triangles[t + 1] = (ushort)(v + 1); triangles[t + 2] = (ushort)(v + 2);
            triangles[t + 3] = (ushort)v; triangles[t + 4] = (ushort)(v + 2); triangles[t + 5] = (ushort)(v + 3);
        }
    }

    SpriteRenderer MakeRenderer(string partName, Sprite sprite, int sorting)
    {
        GameObject child = new GameObject("Rig_" + partName);
        generatedObjects.Add(child);
        child.transform.SetParent(transform, false);
        var render = child.AddComponent<SpriteRenderer>();
        render.sprite = sprite;
        render.sortingOrder = sorting;
        if (originalRenderer != null)
        {
            render.sortingLayerID = originalRenderer.sortingLayerID;
            render.sharedMaterial = originalRenderer.sharedMaterial;
            render.color = originalRenderer.color;
        }
        return render;
    }

    // 这里只建立初始姿势；运行中的位置、旋转和显隐交给 .anim 曲线。
    void ApplyInitialPose()
    {
        foreach (Part part in definition.parts)
        {
            pieces[part.name].localPosition = Position(part.px, part.py);
            pieces[part.name].localRotation = Quaternion.identity;
        }
        if (grenadeRenderer != null) grenadeRenderer.enabled = false;
        flashRenderer.enabled = flashCore.enabled = false;
    }

    Vector3 Position(float x, float y) => visualOffset + new Vector3(
        (x - definition.width * .5f) / definition.ppu, (definition.height * .5f - y) / definition.ppu, 0f);

    void OnDisable() => ClearRig();

    void ClearRig()
    {
        if (hiddenOriginal && originalRenderer != null) originalRenderer.enabled = originalEnabled;
        hiddenOriginal = false;
        foreach (GameObject child in generatedObjects)
            if (child != null)
            {
                child.SetActive(false);
                // 同帧重新启用时，先改名，避免 Animator 绑定到尚未销毁的旧部件。
                child.name = "RetiredRig_" + child.GetInstanceID();
                Release(child);
            }
        foreach (Sprite sprite in generatedSprites) if (sprite != null) Release(sprite);
        // 同帧重新启用时复用该组，避免延迟销毁留下一个失效的排序组件。
        if (ownedSortingGroup != null) ownedSortingGroup.enabled = false;
        generatedObjects.Clear(); generatedSprites.Clear(); pieces.Clear(); definition = null;
        grenadeRenderer = flashRenderer = flashCore = null;
    }

    void OnDestroy()
    {
        if (ownedSortingGroup != null) Release(ownedSortingGroup);
    }

    static void Release(UnityEngine.Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
