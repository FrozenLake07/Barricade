#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameIconSetup
{
    [Serializable] sealed class Report {public bool success,scenePreserved;public string error,asset,guid;public int width,height;public int[] windowsSizes;}
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/GameIconSetup.request"));
    static GameIconSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);var report=new Report{asset="Assets/UI/GameIcon/GameIcon.png"};var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        try
        {
            AssetDatabase.ImportAsset(report.asset,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(report.asset);
            importer.textureType=TextureImporterType.Default;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Point;importer.SaveAndReimport();
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(report.asset);if(icon==null)throw new Exception("Icon texture did not import.");report.width=icon.width;report.height=icon.height;
            if(icon.width!=icon.height)throw new Exception("Icon must be square.");
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{icon},IconKind.Any);
            var sizes=PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone,IconKind.Any);report.windowsSizes=sizes;
            if(sizes.Length==0)throw new Exception("Standalone does not expose icon slots.");
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone,Enumerable.Repeat(icon,sizes.Length).ToArray(),IconKind.Any);
            var getter=typeof(PlayerSettings).GetMethod("InternalGetPlayerSettingsObject",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var settings=(UnityEngine.Object)getter.Invoke(null,null);EditorUtility.SetDirty(settings);
            var snapshot=UnityEngine.Object.Instantiate(settings);
            try
            {
                string temporary=Path.Combine(output,"PlayerSettings.native.asset");
                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[]{snapshot},temporary,true);
                if(!File.ReadAllText(temporary).Contains(AssetDatabase.AssetPathToGUID(report.asset)))throw new Exception("Native settings snapshot is missing icon references.");
                File.Copy(temporary,Path.GetFullPath(Path.Combine(Application.dataPath,"../ProjectSettings/ProjectSettings.asset")),true);
            }
            finally{UnityEngine.Object.DestroyImmediate(snapshot);}
            var defaults=PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown,IconKind.Any);var standalone=PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone,IconKind.Any);
            if(defaults.Length==0||defaults.Any(x=>x!=icon)||standalone.Length!=sizes.Length||standalone.Any(x=>x!=icon))throw new Exception("Icon assignments did not round-trip.");
            report.guid=AssetDatabase.AssetPathToGUID(report.asset);
            var serialized=File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../ProjectSettings/ProjectSettings.asset")));
            if(!serialized.Contains(report.guid))throw new Exception("Icon reference was not persisted to ProjectSettings.");report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally{report.scenePreserved=SceneManager.GetActiveScene()==scene&&scene.isDirty==dirty;report.success&=report.scenePreserved;File.WriteAllText(Path.Combine(output,"图标配置检查.json"),JsonUtility.ToJson(report,true));}
    }
}
#endif
