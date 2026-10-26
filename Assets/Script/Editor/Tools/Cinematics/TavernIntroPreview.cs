#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TavernIntroPreview
{
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernIntroPreview.request"));
    static TavernIntroPreview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Render(output);
    }
    static void Render(string output)
    {
        var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;Texture2D pixels=null;
        string oldName=RunState.PlayerName;bool introduced=RunState.ProtagonistIntroduced;var editing=SceneManager.GetActiveScene();bool dirty=editing.isDirty;
        try
        {
            Directory.CreateDirectory(Path.Combine(output,"预览帧"));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cinematics/TavernUI/Prefabs/TavernUI_AnimationStage.prefab");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            root.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(320,180);rect.position=Vector3.zero;rect.localScale=Vector3.one;
            var node=new GameObject("PreviewCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(node,scene);var camera=node.GetComponent<Camera>();
            camera.scene=scene;camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=90;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            target=new RenderTexture(960,540,24){filterMode=FilterMode.Point};target.Create();camera.targetTexture=target;canvas.worldCamera=camera;
            pixels=new Texture2D(960,540,TextureFormat.RGB24,false);var director=root.GetComponentInChildren<PlayableDirector>(true);director.timeUpdateMode=DirectorUpdateMode.Manual;director.Play();
            for(int frame=0;frame<164;frame++)
            {
                director.time=frame/8.0;director.Evaluate();Canvas.ForceUpdateCanvases();
                foreach(var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>()){graphic.SetAllDirty();graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);}
                Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                try{pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"预览帧/Frame_"+frame.ToString("0000")+".png"),pixels.EncodeToPNG());}
                finally{RenderTexture.active=previous;}
            }
            director.Stop();
            if(RunState.PlayerName!=oldName||RunState.ProtagonistIntroduced!=introduced||SceneManager.GetActiveScene()!=editing||editing.isDirty!=dirty)throw new Exception("Preview must preserve names and user scene state");
            File.WriteAllText(Path.Combine(output,"预览导出.txt"),"success;164frames;8fps;960x540;20.5seconds;motion-only;no-dialogue-or-input-waits;user-scene-preserved");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"预览导出.txt"),e.ToString());Debug.LogException(e);}
        finally
        {
            if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
#endif
