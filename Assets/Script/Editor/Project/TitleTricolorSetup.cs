#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TitleTricolorSetup
{
    static TitleTricolorSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TitleTricolorSetup.request"));
        if(!File.Exists(request))return;
        string output=File.ReadAllText(request).Trim();File.Delete(request);
        bool original=output.StartsWith("original|");if(original)output=output.Substring(9);
        try
        {
            if(original)
            {
                const string source="Assets/UI/Campaign/StartScreen.prefab";
                var restored=PrefabUtility.LoadPrefabContents(source);
                try
                {
                    var view=restored.GetComponent<StartScreenView>();
                    view.titlePage.transform.Find("GameTitleMark").GetComponent<UnityEngine.UI.Image>().material=null;
                    PrefabUtility.SaveAsPrefabAsset(restored,source);
                }
                finally{PrefabUtility.UnloadPrefabContents(restored);}
                File.WriteAllText(output,"SUCCESS: Original title restored; prefab uses default UI material. Chapter beats unchanged.");
                return;
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/UI/Paris1832/TitleTricolor.shader");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Title shader failed to compile");
            const string path="Assets/Resources/UI/Paris1832/TitleTricolor.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.SetColor("_Blue",new Color32(0,0,145,255));mat.SetColor("_White",Color.white);mat.SetColor("_Red",new Color32(225,0,15,255));
            EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);
            const string prefab="Assets/UI/Campaign/StartScreen.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var view=root.GetComponent<StartScreenView>();
                var image=view.titlePage.transform.Find("GameTitleMark").GetComponent<UnityEngine.UI.Image>();
                image.material=mat;PrefabUtility.SaveAsPrefabAsset(root,prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(output,"SUCCESS\nShader compiles; title material assigned to prefab.\nBlue #000091, white #FFFFFF, red #E1000F.\nOriginal artwork and its alpha are unchanged.");
        }
        catch(Exception e){File.WriteAllText(output,"FAILED\n"+e);Debug.LogException(e);}
    }
}
#endif
