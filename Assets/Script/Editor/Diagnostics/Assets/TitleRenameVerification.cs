#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TitleRenameVerification
{
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TitleRename.request"));
    [Serializable] sealed class Report {public bool success,sceneSaved,prefabSaved;public string title,error;public bool allGlyphs,oldLogoHidden;public string productName,titleFont,titleArtwork;}
    static TitleRenameVerification(){EditorApplication.update+=Poll;}
    static void Apply(StartScreenView view)
    {
        ParisUITheme.Apply(view);var page=view.titlePage.transform;
        var title=page.Find("GameTitle").GetComponent<UnityEngine.UI.Text>();
        bool artwork=ParisUI.Style.titleArtwork!=null;
        if(title.text!=ParisUI.GameTitle||title.enabled==artwork)throw new Exception("标题未正确应用。");
        foreach(char c in ParisUI.GameTitle)if(!title.font.HasCharacter(c))throw new Exception("标题字体缺字："+c);
        var image=page.Find("GameTitleMark").GetComponent<UnityEngine.UI.Image>();
        if(image.enabled!=artwork||image.sprite!=ParisUI.Style.titleArtwork)throw new Exception("新字标引用或可见性错误。");
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string firstPreview=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/FirstLaunchCoverPreview.request"));
        if(File.Exists(firstPreview))
        {
            string directory=File.ReadAllText(firstPreview).Trim();File.Delete(firstPreview);PreviewFirstCover(directory);return;
        }
        string firstCover=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/FirstLaunchCoverSetup.request"));
        if(File.Exists(firstCover))
        {
            string directory=File.ReadAllText(firstCover).Trim();File.Delete(firstCover);
            try{SetupFirstLaunchCover();File.WriteAllText(Path.Combine(directory,"首次封面配置.txt"),"success");}
            catch(Exception e){File.WriteAllText(Path.Combine(directory,"首次封面配置.txt"),e.ToString());Debug.LogException(e);}return;
        }
        string art=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TitleArtworkSetup.request"));
        if(File.Exists(art))
        {
            string directory=File.ReadAllText(art).Trim();File.Delete(art);
            try{SetupTitleArtwork();File.WriteAllText(Path.Combine(directory,"字标配置.txt"),"success");}
            catch(Exception e){File.WriteAllText(Path.Combine(directory,"字标配置.txt"),e.ToString());Debug.LogException(e);}return;
        }
        string font=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TitleFontSetup.request"));
        if(File.Exists(font))
        {
            string directory=File.ReadAllText(font).Trim();File.Delete(font);
            try{SetupTitleFont();File.WriteAllText(Path.Combine(directory,"标题字体配置.txt"),"success");}
            catch(Exception e){File.WriteAllText(Path.Combine(directory,"标题字体配置.txt"),e.ToString());Debug.LogException(e);}return;
        }
        string covers=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TitleCoverSetup.request"));
        if(File.Exists(covers))
        {
            string directory=File.ReadAllText(covers).Trim();File.Delete(covers);
            try{SetupCovers();File.WriteAllText(Path.Combine(directory,"封面资源配置.txt"),"success");}
            catch(Exception e){File.WriteAllText(Path.Combine(directory,"封面资源配置.txt"),e.ToString());Debug.LogException(e);}return;
        }
        if(!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);var report=new Report{title=ParisUI.GameTitle,productName=PlayerSettings.productName};
        try
        {
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(active.isDirty)throw new Exception("当前场景有未保存编辑；没有覆盖场景或预制体。");
            string path="Assets/UI/Campaign/StartScreen.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try{Apply(root.GetComponent<StartScreenView>());PrefabUtility.SaveAsPrefabAsset(root,path);report.prefabSaved=true;}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            string scenePath="Assets/Scenes/Title.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                foreach(var view in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StartScreenView>(true)))Apply(view);
                EditorSceneManager.MarkSceneDirty(scene);report.sceneSaved=EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            AssetDatabase.SaveAssets();report.titleFont=ParisUI.Style.titleFont?.name;report.titleArtwork=ParisUI.Style.titleArtwork?.name;report.success=report.allGlyphs=report.oldLogoHidden=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"标题修改.json"),JsonUtility.ToJson(report,true));
    }
    [Serializable] sealed class FirstCoverReport
    {
        public bool success,preferenceRestored,editingScenePreserved;public string error;
        public int checks;public string[] tested;
        public string coverage="隔离预制体逻辑验证：调用实际ShowTitle/ApplyTitleCover，保存读取PlayerPrefs，销毁重建标题视图；未进入Play，未修改当前脏场景。";
    }
    static void PreviewFirstCover(string output)
    {
        var report=new FirstCoverReport();var checks=new System.Collections.Generic.List<string>();
        var editing=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=editing.isDirty;
        bool had=PlayerPrefs.HasKey(StartScreenView.FirstLaunchCoverSeenKey);int original=PlayerPrefs.GetInt(StartScreenView.FirstLaunchCoverSeenKey,0);
        GameObject root=null;
        Action<bool,string> require=(ok,message)=>{report.checks++;checks.Add(message);if(!ok)throw new Exception(message);};
        try
        {
            PlayerPrefs.DeleteKey(StartScreenView.FirstLaunchCoverSeenKey);PlayerPrefs.Save();
            root=PrefabUtility.LoadPrefabContents("Assets/UI/Campaign/StartScreen.prefab");
            var view=root.GetComponent<StartScreenView>();ParisUITheme.Apply(view);view.ShowTitle();
            var style=ParisUI.Style;var background=view.titlePage.transform.Find("Background").GetComponent<UnityEngine.UI.Image>();
            require(style.firstLaunchCover!=null&&!style.titleCovers.Contains(style.firstLaunchCover),"首次图片独立于三图随机池");
            require(style.firstLaunchCover.rect.width==1672&&style.firstLaunchCover.rect.height==941,"保持原图尺寸");
            require(view.TitleCoverIndex==StartScreenView.FirstLaunchCoverIndex&&background.sprite==style.firstLaunchCover,"首次ShowTitle显示新图");
            require(PlayerPrefs.GetInt(StartScreenView.FirstLaunchCoverSeenKey,0)==1,"已展示记录写入本机PlayerPrefs");
            ParisUITheme.Apply(view);require(background.sprite==style.firstLaunchCover,"刷新主题不会提前替换首次图片");
            require(view.titlePage.transform.Find("GameTitleMark").GetComponent<RectTransform>().anchorMin.x>.5f,"标题在右上天空区域");
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(style.firstLaunchCover));
            require(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"像素图导入设置正确");
            var random=JsonUtility.ToJson(UnityEngine.Random.state);view.ShowTitle();
            require(view.TitleCoverIndex>=0&&view.TitleCoverIndex<3&&background.sprite!=style.firstLaunchCover,"第二次ShowTitle不再显示首次图");
            int previous=view.TitleCoverIndex;
            for(int i=0;i<12;i++){view.ShowTitle();require(view.TitleCoverIndex>=0&&view.TitleCoverIndex<3&&view.TitleCoverIndex!=previous,"后续抽图有效且不连续重复："+i);previous=view.TitleCoverIndex;}
            require(random==JsonUtility.ToJson(UnityEngine.Random.state),"封面选择不消耗战斗随机数");
            PrefabUtility.UnloadPrefabContents(root);root=null;
            typeof(StartScreenView).GetMethod("ResetCoverHistory",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
            root=PrefabUtility.LoadPrefabContents("Assets/UI/Campaign/StartScreen.prefab");view=root.GetComponent<StartScreenView>();ParisUITheme.Apply(view);view.ShowTitle();
            require(view.TitleCoverIndex>=0&&view.TitleCoverIndex<3,"销毁并重建标题视图及重置会话仍不显示首次图");
            require(PlayerPrefs.GetInt(StartScreenView.FirstLaunchCoverSeenKey,0)==1,"重建后已展示记录仍存在");
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            if(root!=null)PrefabUtility.UnloadPrefabContents(root);
            if(had)PlayerPrefs.SetInt(StartScreenView.FirstLaunchCoverSeenKey,original);else PlayerPrefs.DeleteKey(StartScreenView.FirstLaunchCoverSeenKey);PlayerPrefs.Save();
            report.preferenceRestored=PlayerPrefs.HasKey(StartScreenView.FirstLaunchCoverSeenKey)==had&&PlayerPrefs.GetInt(StartScreenView.FirstLaunchCoverSeenKey,0)==original;
            report.editingScenePreserved=UnityEngine.SceneManagement.SceneManager.GetActiveScene()==editing&&editing.isDirty==dirty;
            report.success=report.success&&report.preferenceRestored&&report.editingScenePreserved;report.tested=checks.ToArray();
            File.WriteAllText(Path.Combine(output,"首次封面隔离验证.json"),JsonUtility.ToJson(report,true));
        }
    }
    static void SetupFirstLaunchCover()
    {
        const string path="Assets/Resources/UI/Paris1832/Covers/FirstLaunchFuneral.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=2048;importer.filterMode=FilterMode.Point;
        importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite==null||sprite.rect.width!=1672||sprite.rect.height!=941)throw new Exception("首次封面尺寸不符");
        var style=Resources.Load<ParisUIStyle>("UI/Paris1832/Style");Undo.RecordObject(style,"接入首次进入封面");
        style.firstLaunchCover=sprite;EditorUtility.SetDirty(style);AssetDatabase.SaveAssetIfDirty(style);
    }
    static void SetupCovers()
    {
        var sprites=new System.Collections.Generic.List<Sprite>();
        foreach(char letter in "ABC")
        {
            string path="Assets/Resources/UI/Paris1832/Covers/Cover"+letter+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite==null||sprite.rect.width!=320||sprite.rect.height!=180)throw new Exception("封面导入错误："+path);
            sprites.Add(sprite);
        }
        var style=Resources.Load<ParisUIStyle>("UI/Paris1832/Style");Undo.RecordObject(style,"接入三版随机封面");
        style.titleCovers=sprites.ToArray();style.titleBackground=sprites[0];EditorUtility.SetDirty(style);AssetDatabase.SaveAssetIfDirty(style);
    }
    static void SetupTitleArtwork()
    {
        const string path="Assets/Resources/UI/Paris1832/TitleHeartbeat-OriginalStyle.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=4096;importer.filterMode=FilterMode.Point;
        importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;
        importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        var style=Resources.Load<ParisUIStyle>("UI/Paris1832/Style");Undo.RecordObject(style,"沿用街垒原字形更换标题");
        style.titleArtwork=sprite;EditorUtility.SetDirty(style);AssetDatabase.SaveAssetIfDirty(style);
    }
    static void SetupTitleFont()
    {
        const string path="Assets/UI/Fonts/Title/NotoSerifSC-Bold.otf";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TrueTypeFontImporter)AssetImporter.GetAtPath(path);
        importer.fontTextureCase=FontTextureCase.Dynamic;importer.includeFontData=true;
        importer.fontRenderingMode=FontRenderingMode.HintedSmooth;importer.SaveAndReimport();
        var font=AssetDatabase.LoadAssetAtPath<Font>(path);if(font==null)throw new Exception("标题字体导入失败。");
        foreach(char ch in ParisUI.GameTitle)if(!font.HasCharacter(ch))throw new Exception("标题字库缺少："+ch);
        var style=Resources.Load<ParisUIStyle>("UI/Paris1832/Style");Undo.RecordObject(style,"主标题更换庄严宋体");
        style.titleFont=font;EditorUtility.SetDirty(style);AssetDatabase.SaveAssetIfDirty(style);
    }
}
#endif
