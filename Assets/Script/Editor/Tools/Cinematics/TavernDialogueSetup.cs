#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TavernDialogueSetup
{
    const string Root="Assets/Cinematics/TavernUI";
    const string StagePrefab=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    const string DialoguePrefab=Root+"/Prefabs/DialogueBlank.prefab";
    [Serializable] sealed class RequestData {public string mode="inspect",output="";}
    [Serializable] sealed class Report
    {
        public bool success,playing,dirty,dialogueExists,previousRootsPreserved;
        public string scene,error,stageSource,timeline,font,prefab=DialoguePrefab;
        public string[] children;public int emptyTextFields;
    }
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernDialogue.request"));
    static TavernDialogueSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        var data=JsonUtility.FromJson<RequestData>(File.ReadAllText(Request));File.Delete(Request);Run(data);
    }
    static GameObject LiveStage(Scene scene)=>scene.GetRootGameObjects().SingleOrDefault(g=>g.name=="TavernUI_AnimationStage");
    static RectTransform Rect(string name,Transform parent)
    {
        var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;
        if(parent!=null)rect.SetParent(parent,false);rect.localScale=Vector3.one;return rect;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
    }
    static UnityEngine.UI.Text Text(string name,Transform parent,float x,float y,float width,float height,int size,Color color)
    {
        var rect=Rect(name,parent);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);
        var text=rect.gameObject.AddComponent<UnityEngine.UI.Text>();text.font=GameFont.Silver;text.text="";text.fontSize=size;text.color=color;
        text.alignment=TextAnchor.UpperLeft;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        text.supportRichText=true;text.resizeTextForBestFit=false;text.raycastTarget=false;return text;
    }
    static GameObject MakeAsset()
    {
        if(File.Exists(DialoguePrefab))throw new Exception("The dialogue prefab already exists; refusing to overwrite author edits.");
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=Rect("Dialogue",null);SceneManager.MoveGameObjectToScene(root.gameObject,scene);Stretch(root);
            var line=Rect("Line01",root);line.anchorMin=Vector2.zero;line.anchorMax=new Vector2(1,0);line.pivot=new Vector2(.5f,0);line.anchoredPosition=new Vector2(0,2);line.sizeDelta=new Vector2(-16,40);
            var frame=line.gameObject.AddComponent<UnityEngine.UI.Image>();frame.color=new Color(.35f,.31f,.24f,.92f);frame.raycastTarget=false;
            var fill=Rect("Background",line);Stretch(fill);fill.offsetMin=Vector2.one;fill.offsetMax=-Vector2.one;
            var background=fill.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=new Color(.035f,.035f,.04f,.93f);background.raycastTarget=false;
            var group=line.gameObject.AddComponent<CanvasGroup>();group.alpha=1;group.interactable=false;group.blocksRaycasts=false;
            Text("Name",line,8,3,288,11,8,new Color(.85f,.75f,.55f,1));
            Text("Content",line,8,15,288,22,9,new Color(.92f,.9f,.84f,1));
            line.gameObject.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root.gameObject,DialoguePrefab);
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static GameObject Add(Transform stage,GameObject prefab,bool undo)
    {
        var frame=stage.Find("Stage_320x180");if(frame==null)throw new Exception("Stage_320x180 not found");
        var existing=frame.Find("Dialogue");if(existing!=null)throw new Exception("An existing Dialogue will not be replaced.");
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,frame);instance.name="Dialogue";Stretch((RectTransform)instance.transform);instance.transform.SetAsLastSibling();
        if(undo)Undo.RegisterCreatedObjectUndo(instance,"创建空白对话 UI");return instance;
    }
    static ActivationTrack Track(TimelineAsset timeline)
    {
        if(timeline.GetOutputTracks().Any(t=>t.name=="对白_Line01"))throw new Exception("An existing dialogue track will not be replaced.");
        var track=timeline.CreateTrack<ActivationTrack>(null,"对白_Line01");track.postPlaybackState=ActivationTrack.PostPlaybackState.Inactive;
        var clip=track.CreateDefaultClip();clip.start=0;clip.duration=4;clip.displayName="空白对白（填写 Name / Content）";
        EditorUtility.SetDirty(timeline);AssetDatabase.SaveAssetIfDirty(timeline);return track;
    }
    static void Run(RequestData data)
    {
        var r=new Report();var scene=SceneManager.GetActiveScene();var initial=scene.GetRootGameObjects();r.scene=scene.path;r.dirty=scene.isDirty;r.playing=EditorApplication.isPlaying;
        GameObject prefabContents=null;
        try
        {
            if(scene.path!="Assets/Scenes/3.unity")throw new Exception("Scene 3 must be active; no scene switch will be made.");
            var live=LiveStage(scene);if(live==null)throw new Exception("The existing tavern UI stage was not found.");
            r.stageSource=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(live);r.font=AssetDatabase.GetAssetPath(GameFont.Silver);
            r.children=live.transform.Find("Stage_320x180").Cast<Transform>().Select(t=>t.name).ToArray();r.dialogueExists=r.children.Contains("Dialogue");
            var liveDirector=live.GetComponentInChildren<PlayableDirector>(true);r.timeline=AssetDatabase.GetAssetPath(liveDirector.playableAsset);
            if(data.mode=="create")
            {
                if(GameFont.Silver==null||r.dialogueExists)throw new Exception("Missing game font or existing authored Dialogue; no overwrite.");
                var liveTimeline=liveDirector.playableAsset as TimelineAsset;if(liveTimeline==null)throw new Exception("The current Director needs a TimelineAsset.");
                Directory.CreateDirectory(Path.Combine(data.output,"备份"));
                foreach(string path in new[]{StagePrefab,r.timeline})
                {
                    var target=Path.Combine(data.output,"备份",Path.GetFileName(path));if(!File.Exists(target))File.Copy(path,target);
                }
                var asset=MakeAsset();var track=Track(liveTimeline);
                prefabContents=PrefabUtility.LoadPrefabContents(StagePrefab);var sourceDirector=prefabContents.GetComponentInChildren<PlayableDirector>(true);
                var savedDialogue=Add(prefabContents.transform,asset,false);
                if(sourceDirector.playableAsset==liveTimeline)sourceDirector.SetGenericBinding(track,savedDialogue.transform.Find("Line01").gameObject);
                else
                {
                    var sourceTimeline=sourceDirector.playableAsset as TimelineAsset;if(sourceTimeline==null)throw new Exception("Stage asset has no TimelineAsset.");
                    sourceDirector.SetGenericBinding(Track(sourceTimeline),savedDialogue.transform.Find("Line01").gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(prefabContents,StagePrefab);PrefabUtility.UnloadPrefabContents(prefabContents);prefabContents=null;
                // Refreshing the prefab may already add its new nested child to the live scene.
                live=LiveStage(scene);liveDirector=live.GetComponentInChildren<PlayableDirector>(true);
                var dialogue=live.transform.Find("Stage_320x180/Dialogue");
                if(dialogue==null)dialogue=Add(live.transform,asset,true).transform;
                Undo.RecordObject(liveDirector,"绑定空白对白轨道");liveDirector.SetGenericBinding(track,dialogue.Find("Line01").gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(liveDirector);
                var texts=dialogue.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                if(texts.Length!=2||texts.Any(t=>t.text!=""||t.font!=GameFont.Silver))throw new Exception("Expected empty Name and Content with game font.");
                if(dialogue.Find("Line01").gameObject.activeSelf)throw new Exception("Blank dialogue must start hidden.");
                if(liveDirector.GetGenericBinding(track)!=dialogue.Find("Line01").gameObject)throw new Exception("Dialogue activation binding is missing.");
                r.emptyTextFields=texts.Length;r.dialogueExists=true;EditorSceneManager.MarkSceneDirty(scene);r.dirty=scene.isDirty;
                r.previousRootsPreserved=initial.All(g=>g!=null&&g.scene==scene);if(!r.previousRootsPreserved)throw new Exception("An original scene root was lost.");
                Selection.activeGameObject=dialogue.Find("Line01").gameObject;
            }
            r.success=true;
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally
        {
            if(prefabContents!=null)PrefabUtility.UnloadPrefabContents(prefabContents);
            Directory.CreateDirectory(data.output);File.WriteAllText(Path.Combine(data.output,data.mode=="create"?"创建报告.json":"现场检查.json"),JsonUtility.ToJson(r,true));
        }
    }
}
#endif
