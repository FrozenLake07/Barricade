#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Native Unity 2022 import bridge. All scenes/assets/prefabs are authored by Unity APIs.
[InitializeOnLoad]
public static class TavernScene3Import
{
    const string Art="Assets/Art/Cinematics/TavernUI";
    const string Root="Assets/Cinematics/TavernUI";
    const string Prefab=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    const string Timeline=Root+"/Timelines/TavernUI_Workbench.playable";
    const string StageName="TavernUI_AnimationStage";
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernScene3Import.request"));
    [Serializable] sealed class Item {public string file="";public int x=0,y=0;}
    [Serializable] sealed class Layout {public Item[] items=Array.Empty<Item>();}
    [Serializable] sealed class Report
    {
        public bool success,sceneWasDirty,sceneLeftDirty,previousScenePreserved;
        public string error,scene,prefab,timeline;
        public int environmentImages,extractedFrames,uiAnimationClips,checks;
        public string[] tested,initialRoots,finalRoots;
    }
    sealed class Actor
    {
        public GameObject root;public Animator motion,placement;public AnimationClip idle;
        public UnityEngine.UI.Image image;
    }
    static readonly List<string> checks=new List<string>();
    static int framesMade;
    static TavernScene3Import(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string review=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernUIReview.request"));
        if(File.Exists(review))
        {
            string previewOutput=File.ReadAllText(review).Trim();File.Delete(review);
            var prefab=PrefabUtility.LoadPrefabContents(Prefab);
            try{Render(prefab,Path.Combine(previewOutput,"酒馆_UI_场景预览.png"));File.WriteAllText(Path.Combine(previewOutput,"预览核验.txt"),"success");}
            catch(Exception e){File.WriteAllText(Path.Combine(previewOutput,"预览核验.txt"),e.ToString());Debug.LogException(e);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}return;
        }
        if(!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static void Save(UnityEngine.Object asset){EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);}
    static Sprite Single(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var t=AssetImporter.GetAtPath(path) as TextureImporter;
        if(t==null)throw new Exception("Texture importer unavailable: "+path);
        t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;
        t.spritePixelsPerUnit=32;
        t.filterMode=FilterMode.Point;t.textureCompression=TextureImporterCompression.Uncompressed;
        t.mipmapEnabled=false;t.alphaIsTransparency=true;t.npotScale=TextureImporterNPOTScale.None;
        t.maxTextureSize=2048;t.SaveAndReimport();
        var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);Check(s!=null,"Sprite imported: "+path);return s;
    }
    static Sprite Env(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/Environment/"+path);
    static Sprite[] Extract(string person,string source,string action,int count)
    {
        string target=Art+"/Characters/"+person+"/Frames/"+action;Folder(target);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            Check(texture.LoadImage(File.ReadAllBytes(Art+"/Characters/"+person+"/Source/"+source)),"Decode original sheet: "+person+" "+action);
            Check(texture.width==256&&texture.height==64*(count/4),"Original 64x64 grid: "+person+" "+action);
            var sprites=new Sprite[count];
            for(int i=0;i<count;i++)
            {
                var frame=new Texture2D(64,64,TextureFormat.RGBA32,false);
                try
                {
                    // Exact pixel extraction: top row first; no resizing, trimming or alpha changes.
                    frame.SetPixels(texture.GetPixels((i%4)*64,texture.height-(i/4+1)*64,64,64));frame.Apply();
                    string file=target+"/"+action+"_"+i.ToString("00")+".png";File.WriteAllBytes(file,frame.EncodeToPNG());
                    sprites[i]=Single(file);framesMade++;
                }
                finally{UnityEngine.Object.DestroyImmediate(frame);}
            }
            return sprites;
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    static AnimationClip Clip(string name,Sprite[] frames,float fps,bool loop)
    {
        string path=Root+"/Animations/"+name+".anim";
        Check(!File.Exists(path),"New UI clip path: "+name);
        var clip=new AnimationClip{name=name,frameRate=fps};
        var keys=new ObjectReferenceKeyframe[frames.Length+1];
        for(int i=0;i<frames.Length;i++)keys[i]=new ObjectReferenceKeyframe{time=i/fps,value=frames[i]};
        keys[frames.Length]=new ObjectReferenceKeyframe{time=frames.Length/fps,value=loop?frames[0]:frames[frames.Length-1]};
        AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(UnityEngine.UI.Image),propertyName="m_Sprite"},keys);
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
        AssetDatabase.CreateAsset(clip,path);return clip;
    }
    static AnimationClip ConvertHero(AnimationClip source)
    {
        var clip=new AnimationClip{name="UI_"+source.name,frameRate=source.frameRate};int curves=0;
        foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            if(binding.type!=typeof(SpriteRenderer)||binding.propertyName!="m_Sprite")continue;
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(UnityEngine.UI.Image),propertyName="m_Sprite"},AnimationUtility.GetObjectReferenceCurve(source,binding));curves++;
        }
        Check(curves==1,"Hero sprite curve converted to Image: "+source.name);
        AnimationUtility.SetAnimationClipSettings(clip,AnimationUtility.GetAnimationClipSettings(source));
        AssetDatabase.CreateAsset(clip,Root+"/Animations/"+clip.name+".anim");return clip;
    }
    static Sprite First(AnimationClip clip)
    {
        var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
        return (Sprite)AnimationUtility.GetObjectReferenceCurve(clip,binding)[0].value;
    }
    static AnimatorController Controller(string name,AnimationClip idle,IEnumerable<AnimationClip> clips)
    {
        var c=AnimatorController.CreateAnimatorControllerAtPath(Root+"/Animator/"+name+".controller");
        var machine=c.layers[0].stateMachine;
        foreach(var clip in clips){var state=machine.AddState(clip.name);state.motion=clip;state.writeDefaultValues=false;if(clip==idle)machine.defaultState=state;}
        Save(c);return c;
    }
    static RectTransform Node(string name,Transform parent,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;
        if(parent!=null)rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.sizeDelta=size;rect.anchoredPosition=Vector2.zero;return rect;
    }
    static RectTransform Group(string name,Transform parent)=>Node(name,parent,new Vector2(320,180));
    static UnityEngine.UI.Image ImageAt(string name,Transform parent,Sprite sprite,float x,float y,float width=-1,float height=-1)
    {
        Check(sprite!=null,"UI Image source resolves: "+name);
        var rect=Node(name,parent,new Vector2(width<0?sprite.rect.width:width,height<0?sprite.rect.height:height));rect.anchoredPosition=new Vector2(x,-y);
        var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.raycastTarget=false;image.type=UnityEngine.UI.Image.Type.Simple;return image;
    }
    static void Outside(string name,Transform parent,bool dawn)
    {
        var group=Group(name,parent);string time=dawn?"清晨":"夜晚";
        ImageAt("左窗外",group,Env("04_窗外/窗外_"+time+".png"),70,7,28,36);
        ImageAt("右窗外",group,Env("04_窗外/窗外_"+time+".png"),126,7,28,36);
        ImageAt("门后街景",group,Env("03_门动画/门后街景_"+time+".png"),286,0);group.gameObject.SetActive(!dawn);
    }
    static float Bottom(Sprite sprite)
    {
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));var p=tex.GetPixels32();var r=sprite.rect;
            for(int y=0;y<r.height;y++)for(int x=0;x<r.width;x++)if(p[((int)r.y+y)*tex.width+(int)r.x+x].a>=32)return y;
            return 0;
        }
        finally{UnityEngine.Object.DestroyImmediate(tex);}
    }
    static Actor Character(string name,Transform parent,AnimatorController controller,AnimationClip idle,float x,float footY)
    {
        var root=Node(name,parent,new Vector2(64,64));root.pivot=new Vector2(.5f,0);root.anchoredPosition=new Vector2(x,-footY);
        var image=ImageAt("Visual",root,First(idle),0,0,64,64);var visual=image.rectTransform;
        visual.anchorMin=visual.anchorMax=new Vector2(.5f,0);visual.pivot=new Vector2(.5f,0);visual.anchoredPosition=new Vector2(0,-Bottom(image.sprite));
        var motion=image.gameObject.AddComponent<Animator>();motion.runtimeAnimatorController=controller;motion.applyRootMotion=false;
        var placement=root.gameObject.AddComponent<Animator>();placement.applyRootMotion=false;
        return new Actor{root=root.gameObject,motion=motion,placement=placement,idle=idle,image=image};
    }
    static void Track(TimelineAsset timeline,PlayableDirector director,Actor actor)
    {
        var group=timeline.CreateTrack<GroupTrack>(null,actor.root.name);
        var placement=timeline.CreateTrack<AnimationTrack>(group,"位移（录制 RectTransform）");placement.trackOffset=TrackOffset.ApplySceneOffsets;
        director.SetGenericBinding(placement,actor.placement);
        var motion=timeline.CreateTrack<AnimationTrack>(group,"动作（UI Image）");motion.trackOffset=TrackOffset.ApplySceneOffsets;
        var clip=motion.CreateClip(actor.idle);clip.start=0;clip.duration=4;clip.displayName="待机占位，可替换动作";
        director.SetGenericBinding(motion,actor.motion);
    }
    static void Validate(GameObject root,TimelineAsset timeline)
    {
        Check(root.GetComponent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay,"Stage uses Screen Space Overlay uGUI");
        Check(root.GetComponentsInChildren<SpriteRenderer>(true).Length==0,"No SpriteRenderer on UI stage");
        Check(root.GetComponentsInChildren<HeroActor>(true).Length==0,"No gameplay HeroActor can override UI animation");
        foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))Check(!image.raycastTarget,"Decorative image ignores input: "+image.name);
        foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{Root+"/Animations"}))
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                Check(binding.type==typeof(UnityEngine.UI.Image),"Image binding: "+clip.name);
                Check(AnimationUtility.GetObjectReferenceCurve(clip,binding).All(k=>k.value is Sprite),"All sprite keys resolve: "+clip.name);
            }
        }
        var director=root.GetComponentInChildren<PlayableDirector>(true);
        Check(!director.playOnAwake&&director.playableAsset==timeline,"Director is bound and does not autoplay");
        foreach(var track in timeline.GetOutputTracks().OfType<AnimationTrack>())Check(director.GetGenericBinding(track) is Animator,"Timeline Animator binding resolves: "+track.parent.name+"/"+track.name);
        foreach(var animator in root.GetComponentsInChildren<Animator>(true).Where(a=>a.GetComponent<UnityEngine.UI.Image>()!=null))
        {
            foreach(var clip in animator.runtimeAnimatorController.animationClips)
            {
                var image=animator.GetComponent<UnityEngine.UI.Image>();var original=image.sprite;float at=clip.length*.45f;clip.SampleAnimation(animator.gameObject,at);
                var b=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();var keys=AnimationUtility.GetObjectReferenceCurve(clip,b);var expected=keys.Last(k=>k.time<=at+.00001f).value;
                Check(image.sprite==expected,"Actual UI animation sampling: "+clip.name);image.sprite=original;
            }
        }
    }
    static void Render(GameObject source,string file)
    {
        var scene=EditorSceneManager.NewPreviewScene();GameObject clone=null;RenderTexture target=null;Texture2D pixels=null;
        try
        {
            clone=UnityEngine.Object.Instantiate(source);SceneManager.MoveGameObjectToScene(clone,scene);
            var cameraNode=new GameObject("UI Preview Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraNode,scene);
            var camera=cameraNode.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=90;
            camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;
            var canvas=clone.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            var canvasRect=(RectTransform)canvas.transform;canvasRect.sizeDelta=new Vector2(320,180);canvasRect.position=Vector3.zero;canvasRect.localScale=Vector3.one;
            clone.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;canvas.scaleFactor=4;
            foreach(var a in clone.GetComponentsInChildren<Animator>(true))a.enabled=false;
            Canvas.ForceUpdateCanvases();
            foreach(var graphic in clone.GetComponentsInChildren<UnityEngine.UI.Graphic>()){graphic.SetAllDirty();graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);}
            Canvas.ForceUpdateCanvases();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            try{pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                Check(pixels.GetPixels32().Count(p=>p.r+p.g+p.b>30)>1280*720/3,"Rendered preview contains visible artwork");
                File.WriteAllBytes(file,pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;}
        }
        finally
        {
            if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void Run(string output)
    {
        Directory.CreateDirectory(output);checks.Clear();framesMade=0;var report=new Report();Scene preview=default;GameObject stage=null;
        var original=SceneManager.GetActiveScene();bool originalDirty=original.isDirty;var initial=original.GetRootGameObjects();
        try
        {
            Check(original.path=="Assets/Scenes/3.unity","User is editing scene 3; existing content will stay unsaved");
            Check(!original.GetRootGameObjects().Any(g=>g.name==StageName),"No existing UI stage will be overwritten");
            Check(!File.Exists(Prefab)&&!File.Exists(Timeline),"New native asset paths are available");
            report.scene=original.path;report.sceneWasDirty=originalDirty;report.initialRoots=initial.Select(g=>g.name).ToArray();
            foreach(string dir in new[]{Root,Root+"/Animations",Root+"/Animator",Root+"/Prefabs",Root+"/Timelines"})Folder(dir);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var envFiles=Directory.GetFiles(Art+"/Environment","*.png",SearchOption.AllDirectories);foreach(string p in envFiles)Single(p.Replace('\\','/'));report.environmentImages=envFiles.Length;
            var enjIdle=Clip("UI_Enjolras_Idle",new[]{Single(Art+"/Characters/Enjolras/Source/昂若拉.png")},.5f,true);
            var enjWalk=Clip("UI_Enjolras_Walk",Extract("Enjolras","走路.png","Walk",12),12,true);
            var grIdle=Clip("UI_Grantaire_Idle",new[]{Single(Art+"/Characters/Grantaire/Source/格朗泰尔.png")},.5f,true);
            var grWalk=Clip("UI_Grantaire_Walk",Extract("Grantaire","走路.png","Walk",12),12,true);
            var grDrink=Clip("UI_Grantaire_Drink",Extract("Grantaire","喝酒.png","Drink",24),12,true);
            var hero=Directory.GetFiles("Assets/Resources/HeroAnimations/Clips","*.anim").OrderBy(p=>p).Select(p=>ConvertHero(AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace('\\','/')))).ToArray();
            var heroIdle=hero.Single(c=>c.name=="UI_Hero_IdleLeft");
            var heroController=Controller("UI_Hero",heroIdle,hero);
            var enjController=Controller("UI_Enjolras",enjIdle,new[]{enjIdle,enjWalk});
            var grController=Controller("UI_Grantaire",grIdle,new[]{grIdle,grWalk,grDrink});
            var doors=new[]{Env("03_门动画/门扇_00_关闭.png"),Env("03_门动画/门扇_01_半开.png"),Env("03_门动画/门扇_02_打开.png")};
            var doorClosed=Clip("UI_Door_Closed",new[]{doors[0]},.5f,true);
            var doorOpen=Clip("UI_Door_Open",doors,6,false);var doorClose=Clip("UI_Door_Close",doors.Reverse().ToArray(),6,false);
            var doorController=Controller("UI_Door",doorClosed,new[]{doorClosed,doorOpen,doorClose});
            preview=EditorSceneManager.NewPreviewScene();
            stage=new GameObject(StageName,typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));SceneManager.MoveGameObjectToScene(stage,preview);
            var canvas=stage.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.pixelPerfect=true;
            var scaler=stage.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(320,180);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var black=Node("Letterbox",stage.transform,Vector2.zero);black.anchorMin=Vector2.zero;black.anchorMax=Vector2.one;black.offsetMin=black.offsetMax=Vector2.zero;
            var background=black.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=Color.black;background.raycastTarget=false;
            var frame=Node("Stage_320x180",stage.transform,new Vector2(320,180));frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);frame.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            ImageAt("00_地板",frame,Env("01_房间结构/地板_320x180.png"),0,0);
            Outside("01_窗外_夜晚",frame,false);Outside("02_窗外_清晨（备用）",frame,true);
            var structure=Group("03_房间结构",frame);ImageAt("墙面",structure,Env("01_房间结构/墙面_透明门窗洞口_320x180.png"),0,0);
            ImageAt("左窗框",structure,Env("05_窗框灯具/窗格_透明.png"),66,3);ImageAt("右窗框",structure,Env("05_窗框灯具/窗格_透明.png"),122,3);
            var furnishings=Group("04_家具与灯具",frame);var foreground=Group("06_前景桌椅",frame);
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Art+"/Layout.json"));int index=0;
            foreach(var item in layout.items)
            {
                bool front=item.y>=89;var parent=front?foreground:furnishings;
                ImageAt((++index).ToString("00")+"_"+Path.GetFileNameWithoutExtension(item.file),parent,Env(item.file),item.x,item.y);
            }
            var actors=Group("05_人物",frame);actors.SetSiblingIndex(foreground.GetSiblingIndex());
            var heroActor=Character("主角_Hero",actors,heroController,heroIdle,132,130);
            var enjActor=Character("昂若拉_Enjolras",actors,enjController,enjIdle,192,112);
            var grActor=Character("格朗泰尔_Grantaire",actors,grController,grIdle,239,130);
            var column=Group("07_前景木柱",frame);ImageAt("木柱",column,Env("01_房间结构/木柱_无绳.png"),48,0);ImageAt("绳索",column,Env("01_房间结构/绑缚绳索.png"),46,89);
            var door=Group("08_门（可动画）",frame);var leaf=ImageAt("门扇",door,doors[0],286,0);
            leaf.rectTransform.pivot=new Vector2(5f/32,.5f);leaf.rectTransform.anchoredPosition=new Vector2(291,-36);
            var doorAnimator=leaf.gameObject.AddComponent<Animator>();doorAnimator.runtimeAnimatorController=doorController;
            ImageAt("门框",door,Env("03_门动画/门框.png"),286,0);
            var extras=Group("09_备用道具（默认隐藏）",frame);ImageAt("横放酒桶",extras,Env("01_房间结构/横放酒桶.png"),220,100);ImageAt("棕色酒瓶",extras,Env("02_家具道具/棕色酒瓶.png"),242,100);ImageAt("横梁",extras,Env("05_窗框灯具/横梁.png"),160,0);extras.gameObject.SetActive(false);
            var directorNode=Node("Timeline_酒馆",stage.transform,Vector2.one);var director=directorNode.gameObject.AddComponent<PlayableDirector>();director.playOnAwake=false;director.extrapolationMode=DirectorWrapMode.Hold;
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.name="TavernUI_Workbench";AssetDatabase.CreateAsset(timeline,Timeline);director.playableAsset=timeline;
            Track(timeline,director,heroActor);Track(timeline,director,enjActor);Track(timeline,director,grActor);
            var doorTrack=timeline.CreateTrack<AnimationTrack>(null,"门扇（UI Image）");director.SetGenericBinding(doorTrack,doorAnimator);Save(timeline);
            foreach(var a in new[]{heroActor,enjActor,grActor})PrefabUtility.SaveAsPrefabAsset(a.root,Root+"/Prefabs/"+a.motion.runtimeAnimatorController.name+".prefab");
            Validate(stage,timeline);PrefabUtility.SaveAsPrefabAsset(stage,Prefab);
            var persisted=PrefabUtility.LoadPrefabContents(Prefab);
            try{Validate(persisted,timeline);Render(persisted,Path.Combine(output,"酒馆_UI_场景预览.png"));}
            finally{PrefabUtility.UnloadPrefabContents(persisted);}
            // Existing dirty scene is only augmented, never saved or closed.
            Check(SceneManager.GetActiveScene()==original&&initial.All(g=>g!=null),"Original user scene and all roots preserved before adding stage");
            int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("导入酒馆 UI 动画工作台");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),original);Undo.RegisterCreatedObjectUndo(instance,"导入酒馆 UI 动画工作台");Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(original);Selection.activeGameObject=instance.transform.Find("Timeline_酒馆").gameObject;
            report.sceneLeftDirty=original.isDirty;report.previousScenePreserved=initial.All(g=>g!=null&&g.scene==original);
            report.finalRoots=original.GetRootGameObjects().Select(g=>g.name).ToArray();report.extractedFrames=framesMade;
            report.uiAnimationClips=AssetDatabase.FindAssets("t:AnimationClip",new[]{Root+"/Animations"}).Length;report.prefab=Prefab;report.timeline=Timeline;
            Check(report.previousScenePreserved&&report.sceneLeftDirty,"Scene 3 keeps user edits, undo support and unsaved state");report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        finally
        {
            if(preview.IsValid())EditorSceneManager.ClosePreviewScene(preview);
            report.checks=checks.Count;report.tested=checks.ToArray();File.WriteAllText(Path.Combine(output,"原生导入报告.json"),JsonUtility.ToJson(report,true));
            if(report.success)Debug.Log("[TavernUI] 酒馆 UI、三个人物、25 个 UI 动画和 Timeline 已配置。场景3保留未保存状态，请检查后 Ctrl+S。");
        }
    }
}
#endif


