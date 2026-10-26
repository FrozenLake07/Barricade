#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HeroBarricadePlacementSetup
{
    const string ScenePath="Assets/Scenes/2.unity";
    [Serializable] sealed class RequestData{public string mode="inspect",output="";public float x=6,y=.9f;}
    [Serializable] sealed class Report
    {
        public bool success,saved,sceneWasLoaded,wasDirty;public string error;
        public Vector3 before,after,barricadePosition,wallMin,wallMax;public string[] sorting;
    }
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/HeroBarricadePlacement.request"));
    static HeroBarricadePlacementSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        var data=JsonUtility.FromJson<RequestData>(File.ReadAllText(Request));File.Delete(Request);Run(data);
    }
    static void Capture(Camera camera,string path)
    {
        var old=camera.targetTexture;var active=RenderTexture.active;var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
    }
    static void Run(RequestData data)
    {
        Directory.CreateDirectory(data.output);var report=new Report();var active=SceneManager.GetActiveScene();Scene scene=default;HeroActor actor=null;
        try
        {
            scene=SceneManager.GetSceneByPath(ScenePath);report.sceneWasLoaded=scene.IsValid()&&scene.isLoaded;
            if(!report.sceneWasLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            report.wasDirty=scene.isDirty;
            actor=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HeroActor>(true)).Single();
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single(c=>c.enabled);
            var wall=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BarricadeHealthVisual>(true)).Single();
            var render=wall.GetComponent<SpriteRenderer>();
            report.before=actor.transform.position;report.barricadePosition=wall.transform.position;report.wallMin=render.bounds.min;report.wallMax=render.bounds.max;
            report.sorting=new[]{"Hero="+actor.visual.sortingLayerID+":"+actor.visual.sortingOrder,"Barricade="+render.sortingLayerID+":"+render.sortingOrder};
            if(data.mode=="apply")
            {
                File.Copy(ScenePath,Path.Combine(data.output,"场景2_修改前.unity"),true);
                actor.transform.position=new Vector3(data.x,data.y,report.before.z);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save defense scene");
                report.saved=true;Capture(camera,Path.Combine(data.output,"调整后_场景.png"));
            }
            else
            {
                Capture(camera,Path.Combine(data.output,"原场景站位.png"));
                actor.transform.position=new Vector3(report.before.x,-2,report.before.z);Capture(camera,Path.Combine(data.output,"原运行时站位.png"));
                foreach(var candidate in new[]{new Vector2(4.7f,.5f),new Vector2(4.7f,.9f),new Vector2(5.1f,.6f),new Vector2(5.1f,1f),new Vector2(5.5f,.7f)})
                {actor.transform.position=new Vector3(candidate.x,candidate.y,report.before.z);Capture(camera,Path.Combine(data.output,"候选_"+candidate.x.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+"_"+candidate.y.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png"));}
                actor.transform.position=new Vector3(data.x,data.y,report.before.z);Capture(camera,Path.Combine(data.output,"自定候选.png"));
                actor.transform.position=report.before;
            }
            report.after=actor.transform.position;report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            if(!report.saved&&actor!=null)actor.transform.position=report.before;
            if(scene.IsValid()&&!report.sceneWasLoaded)EditorSceneManager.CloseScene(scene,true);
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
            File.WriteAllText(Path.Combine(data.output,data.mode+".json"),JsonUtility.ToJson(report,true));
        }
    }
}
#endif

