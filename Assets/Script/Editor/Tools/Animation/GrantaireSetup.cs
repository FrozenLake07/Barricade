#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 从桌面素材生成「格朗泰尔」角色：切片、动画、控制器、预制体。
// 菜单：街垒/角色/创建格朗泰尔
public static class GrantaireSetup
{
    const string SourceFolder = @"C:\Users\ASUS\Desktop\杂项\Art\格朗泰尔";
    const string SourceDrink = "喝酒.png";
    const string SourceWalk = "走路.png";
    const string SourcePortrait = "格朗泰尔.png";

    const string ArtRoot = "Assets/Art/Characters/Grantaire";
    const string ResRoot = "Assets/Resources/Characters/Grantaire";
    const string ClipRoot = ResRoot + "/Animations";
    const string ControllerPath = ResRoot + "/Animator/Grantaire.controller";
    const string PrefabPath = ResRoot + "/Prefabs/Grantaire.prefab";
    const string ReportPath = "Library/GrantaireSetupResult.json";

    const int Cell = 64;
    const float Fps = 12f;
    const float Ppu = 32f;

    [Serializable] sealed class Report
    {
        public bool success;
        public string utc = DateTime.UtcNow.ToString("O");
        public string error;
        public string[] files = new string[0];
        public int drinkFrames, walkFrames;
    }

    [MenuItem("街垒/角色/创建格朗泰尔")]
    public static void Build()
    {
        var report = new Report();
        try
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Characters");
            EnsureFolder(ArtRoot);
            EnsureFolder("Assets/Resources/Characters");
            EnsureFolder(ResRoot);
            EnsureFolder(ClipRoot);
            EnsureFolder(ResRoot + "/Animator");
            EnsureFolder(ResRoot + "/Prefabs");

            string drinkPath = CopyIn(SourceDrink, ArtRoot + "/Grantaire_Drink.png");
            string walkPath = CopyIn(SourceWalk, ArtRoot + "/Grantaire_Walk.png");
            string portraitPath = CopyIn(SourcePortrait, ArtRoot + "/Grantaire_Portrait.png");

            Slice(drinkPath, 4, 6, "Grantaire_Drink");
            Slice(walkPath, 4, 3, "Grantaire_Walk");
            ConfigureSingle(portraitPath);

            var drink = BuildClip("Grantaire_Drink", drinkPath, "Grantaire_Drink", 24, true);
            var walk = BuildClip("Grantaire_Walk", walkPath, "Grantaire_Walk", 12, true);
            var controller = BuildController(drink, walk);
            BuildPrefab(drinkPath, controller);

            report.success = true;
            report.drinkFrames = 24;
            report.walkFrames = 12;
            report.files = new[] { drinkPath, walkPath, portraitPath, ControllerPath, PrefabPath };
            Debug.Log("[GrantaireSetup] 完成：" + string.Join(", ", report.files));
        }
        catch (Exception e)
        {
            report.success = false;
            report.error = e.ToString();
            Debug.LogError("[GrantaireSetup] 失败：" + e.Message);
        }
        finally
        {
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static string CopyIn(string fileName, string target)
    {
        string src = Path.Combine(SourceFolder, fileName);
        if (!File.Exists(src)) throw new FileNotFoundException("找不到素材：" + src);
        File.Copy(src, target, true);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
        return target;
    }

#pragma warning disable 0618
    static void Slice(string path, int cols, int rows, string prefix)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("无法读取导入设置：" + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = Ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;

        var metas = new List<SpriteMetaData>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name = prefix + "_" + (row * cols + col).ToString("00"),
                    rect = new Rect(col * Cell, (rows - 1 - row) * Cell, Cell, Cell),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(.5f, .5f)
                });
            }
        }
        importer.spritesheet = metas.ToArray();
        importer.SaveAndReimport();
    }
#pragma warning restore 0618

    static void ConfigureSingle(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static Sprite[] LoadFrames(string path, string prefix, int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string spritePath = path + "/" + prefix + "_" + i.ToString("00");
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (frames[i] == null) throw new InvalidOperationException("切片缺失：" + spritePath);
        }
        return frames;
    }

    static AnimationClip BuildClip(string name, string path, string prefix, int count, bool loop)
    {
        var frames = LoadFrames(path, prefix, count);
        var clip = new AnimationClip { name = name, frameRate = Fps };
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };
        var keys = new ObjectReferenceKeyframe[count];
        for (int i = 0; i < count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string assetPath = ClipRoot + "/" + name + ".anim";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(clip, assetPath);
        return clip;
    }

    static AnimatorController BuildController(AnimationClip drink, AnimationClip walk)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Walk", AnimatorControllerParameterType.Bool);

        var machine = controller.layers[0].stateMachine;
        var drinkState = machine.AddState("Drink");
        drinkState.motion = drink;
        var walkState = machine.AddState("Walk");
        walkState.motion = walk;
        machine.defaultState = drinkState;

        var toWalk = drinkState.AddTransition(walkState);
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "Walk");
        toWalk.hasExitTime = false;
        toWalk.duration = .1f;

        var toDrink = walkState.AddTransition(drinkState);
        toDrink.AddCondition(AnimatorConditionMode.IfNot, 0, "Walk");
        toDrink.hasExitTime = false;
        toDrink.duration = .1f;

        return controller;
    }

    static void BuildPrefab(string drinkPath, AnimatorController controller)
    {
        var frames = LoadFrames(drinkPath, "Grantaire_Drink", 24);
        var go = new GameObject("Grantaire");
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.sortingOrder = 10;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(.6f, 1.5f);
        box.offset = new Vector2(0, -.1f);

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
    }
}
#endif
