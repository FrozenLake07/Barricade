#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TavernIntroSetup
{
    const string Root="Assets/Cinematics/TavernUI";
    const string Stage=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    const string Intro=Root+"/Timelines/TavernIntroduction.playable";
    const string Lines=Root+"/Data/TavernDialogue.asset";
    const string Workbench=Root+"/Timelines/TavernUI_Workbench.playable";
    const float End=20.5f;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernIntroSetup.request"));
    [Serializable] sealed class Report
    {
        public bool success,scenePreserved,sceneDirty,workbenchPreserved,nameResponsesPreserved;
        public string error,timeline=Intro,dialogue=Lines;public int checks;public float duration;
        public string[] passed;
    }
    sealed class Actor
    {
        public string id;public RectTransform rect;public Animator placement,motion;public CanvasGroup opacity;public UnityEngine.UI.Image image;
    }
    static readonly List<string> passed=new List<string>();
    static TavernIntroSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);Run(output);
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);passed.Add(message);}
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static void Save(UnityEngine.Object asset){EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);}
    static string Hash(string path){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));}
    static Actor Find(GameObject stage,string id)
    {
        var a=stage.GetComponentsInChildren<Animator>(true).Single(x=>x.runtimeAnimatorController!=null&&x.runtimeAnimatorController.name=="UI_"+id&&x.GetComponent<UnityEngine.UI.Image>()!=null);
        var rect=(RectTransform)a.transform.parent;var opacity=rect.GetComponent<CanvasGroup>();if(opacity==null)opacity=rect.gameObject.AddComponent<CanvasGroup>();return new Actor{id=id,rect=rect,placement=rect.GetComponent<Animator>(),motion=a,image=a.GetComponent<UnityEngine.UI.Image>(),opacity=opacity};
    }
    static AnimationClip Motion(string name)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/UI_"+name+".anim");
    static Sprite First(AnimationClip clip)
    {
        var b=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(x=>x.type==typeof(UnityEngine.UI.Image));return (Sprite)AnimationUtility.GetObjectReferenceCurve(clip,b)[0].value;
    }
    static AnimationCurve Curve(float[] times,float[] values,bool stepped=false)
    {
        var keys=times.Select((t,i)=>new Keyframe(t,values[i])).ToArray();var curve=new AnimationCurve(keys);
        for(int i=0;i<keys.Length;i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve,i,stepped?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve,i,stepped?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }
    static void Float(AnimationClip clip,Type type,string property,float[] times,float[] values,bool stepped=false)
    {AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",type,property),Curve(times,values,stepped));}
    static AnimationClip Pose(string id,float[] times,float[] x,float[] y,float[] alphaTimes,float[] alpha,float[] faceTimes,float[] faces)
    {
        var clip=new AnimationClip{name="Intro_"+id+"_Position",frameRate=30};
        Float(clip,typeof(RectTransform),"m_AnchoredPosition.x",times,x);
        Float(clip,typeof(RectTransform),"m_AnchoredPosition.y",times,y.Select(v=>-v).ToArray());
        Float(clip,typeof(Transform),"m_LocalScale.x",faceTimes,faces,true);
        Float(clip,typeof(Transform),"m_LocalScale.y",new[]{0f,End},new[]{1f,1f});
        Float(clip,typeof(CanvasGroup),"m_Alpha",alphaTimes,alpha);
        AssetDatabase.CreateAsset(clip,Root+"/Animations/Intro/"+clip.name+".anim");return clip;
    }
    static TimelineClip Segment(AnimationTrack track,AnimationClip source,double start,double end,string name,bool fit=false)
    {
        if(source==null)throw new Exception("Missing clip: "+name);
        var clip=track.CreateClip(source);clip.start=start;clip.duration=end-start;clip.displayName=name;
        clip.blendInDuration=clip.blendOutDuration=0;
        if(fit)clip.timeScale=source.length/(end-start);return clip;
    }
    static void ActorTracks(TimelineAsset timeline,PlayableDirector director,Actor actor,AnimationClip pose,string[] names,float[] bounds)
    {
        var group=timeline.CreateTrack<GroupTrack>(null,actor.rect.name);
        var placement=timeline.CreateTrack<AnimationTrack>(group,"位移、朝向与出入门");placement.trackOffset=TrackOffset.ApplySceneOffsets;Segment(placement,pose,0,End,"位置 / 朝向 / 隐藏");director.SetGenericBinding(placement,actor.placement);
        var motion=timeline.CreateTrack<AnimationTrack>(group,"动作（UI Image）");motion.trackOffset=TrackOffset.ApplySceneOffsets;
        for(int i=0;i<names.Length;i++)Segment(motion,Motion(actor.id+"_"+names[i]),bounds[i],bounds[i+1],names[i]);
        director.SetGenericBinding(motion,actor.motion);
    }
    static SignalAsset Signal(string key)
    {
        string path=Root+"/Signals/"+key+".signal";var signal=AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
        if(signal==null){signal=ScriptableObject.CreateInstance<SignalAsset>();signal.name=key;AssetDatabase.CreateAsset(signal,path);}return signal;
    }
    static void Signals(TimelineAsset timeline,PlayableDirector director,TavernNameDialogue dialogue)
    {
        var track=timeline.CreateTrack<SignalTrack>(null,"对白与姓名（Signal）");var receiver=director.GetComponent<SignalReceiver>()??director.gameObject.AddComponent<SignalReceiver>();
        string[] keys={"Tavern_FirstDialogue","AskPlayerName","Tavern_SecondDialogue","Tavern_ThirdDialogue","Tavern_FourthDialogue"};
        double[] times={5.2,5.6,5.95,9.4,9.8};UnityAction[] actions={dialogue.FirstDialogue,dialogue.AskName,dialogue.SecondDialogue,dialogue.ThirdDialogue,dialogue.FourthDialogue};
        for(int i=0;i<keys.Length;i++)
        {
            var signal=Signal(keys[i]);var emitter=track.CreateMarker<SignalEmitter>(times[i]);emitter.asset=signal;emitter.retroactive=false;emitter.emitOnce=false;
            var reaction=receiver.GetReaction(signal);
            if(reaction==null){reaction=new UnityEvent();UnityEventTools.AddPersistentListener(reaction,actions[i]);receiver.AddReaction(signal,reaction);}
        }
        director.SetGenericBinding(track,receiver);
    }
    static Animator Fade(GameObject stage)
    {
        var parent=stage.transform.Find("Stage_320x180");var existing=parent.Find("IntroFade");if(existing!=null)return existing.GetComponent<Animator>();
        var go=new GameObject("IntroFade",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(CanvasGroup),typeof(Animator));
        var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var image=go.GetComponent<UnityEngine.UI.Image>();image.color=Color.black;image.raycastTarget=false;go.GetComponent<CanvasGroup>().alpha=0;return go.GetComponent<Animator>();
    }
    static void Bind(GameObject stage,TimelineAsset timeline)
    {
        var director=stage.GetComponentInChildren<PlayableDirector>(true);director.playableAsset=timeline;
        var dialogue=stage.GetComponentInChildren<TavernNameDialogue>(true);dialogue.director=director;dialogue.conversation=AssetDatabase.LoadAssetAtPath<TavernDialogueLines>(Lines);dialogue.nameReplyOnSeparateSignal=true;dialogue.echoNameAfterConfirmation=true;
        foreach(string id in new[]{"Hero","Grantaire","Enjolras"})
        {
            var actor=Find(stage,id);var group=timeline.GetRootTracks().OfType<GroupTrack>().Single(g=>g.name==actor.rect.name);
            foreach(var track in group.GetChildTracks().OfType<AnimationTrack>())director.SetGenericBinding(track,track.name.StartsWith("动作")?actor.motion:actor.placement);
        }
        director.SetGenericBinding(timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>t.name=="门扇开合"),stage.transform.Find("Stage_320x180/08_门（可动画）/门扇").GetComponent<Animator>());
        director.SetGenericBinding(timeline.GetOutputTracks().OfType<AnimationTrack>().Single(t=>t.name=="结束淡出"),Fade(stage));
        var receiver=director.GetComponent<SignalReceiver>();if(receiver==null)receiver=director.gameObject.AddComponent<SignalReceiver>();
        var signals=timeline.GetOutputTracks().OfType<SignalTrack>().Single(t=>t.name=="对白与姓名（Signal）");
        foreach(var emitter in signals.GetMarkers().OfType<SignalEmitter>())
        {
            if(receiver.GetReaction(emitter.asset)!=null)continue;
            UnityAction action;
            switch(emitter.asset.name)
            {
                case "Tavern_FirstDialogue":action=dialogue.FirstDialogue;break;
                case "AskPlayerName":action=dialogue.AskName;break;
                case "Tavern_SecondDialogue":action=dialogue.SecondDialogue;break;
                case "Tavern_ThirdDialogue":action=dialogue.ThirdDialogue;break;
                default:action=dialogue.FourthDialogue;break;
            }
            var reaction=new UnityEvent();UnityEventTools.AddPersistentListener(reaction,action);receiver.AddReaction(emitter.asset,reaction);
        }
        director.SetGenericBinding(signals,receiver);
        var hero=Find(stage,"Hero");hero.rect.anchoredPosition=new Vector2(302,-68);hero.opacity.alpha=0;hero.image.sprite=First(Motion("Hero_IdleLeft"));
        var gr=Find(stage,"Grantaire");gr.rect.anchoredPosition=new Vector2(204,-110);gr.opacity.alpha=1;gr.image.sprite=First(Motion("Grantaire_Drink"));
        var enj=Find(stage,"Enjolras");enj.rect.anchoredPosition=new Vector2(159,-110);enj.opacity.alpha=1;enj.image.sprite=First(Motion("Enjolras_Idle"));
    }
    static TimelineAsset Build(GameObject stage)
    {
        var dialogue=stage.GetComponentInChildren<TavernNameDialogue>(true);Check(dialogue!=null,"Existing naming controller reused");
        var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.name="TavernIntroduction";timeline.editorSettings.frameRate=30;AssetDatabase.CreateAsset(timeline,Intro);
        var director=stage.GetComponentInChildren<PlayableDirector>(true);director.playableAsset=timeline;director.playOnAwake=false;director.extrapolationMode=DirectorWrapMode.None;
        var hero=Find(stage,"Hero");var gr=Find(stage,"Grantaire");var enj=Find(stage,"Enjolras");
        var hp=Pose("Hero",new[]{0f,1f,2.1f,4.8f,13f,14.6f,15.8f,16.4f,End},new[]{302f,302,285,245,245,262,286,302,302},new[]{68f,68,85,126,126,96,81,68,68},new[]{0f,.95f,1.15f,16.35f,16.65f,End},new[]{0f,0,1,1,0,0},new[]{0f,11f,End},new[]{1f,-1,-1});
        var gp=Pose("Grantaire",new[]{0f,2.8f,4.4f,11.2f,12.8f,14.1f,14.6f,End},new[]{204f,204,217,217,254,285,302,302},new[]{110f,110,126,126,92,80,68,68},new[]{0f,14.5f,14.8f,End},new[]{1f,1,0,0},new[]{0f,End},new[]{1f,1});
        var ep=Pose("Enjolras",new[]{0f,7f,9f,14.9f,16.8f,18.1f,18.7f,End},new[]{159f,159,187,187,249,284,302,302},new[]{110f,110,126,126,99,82,68,68},new[]{0f,18.6f,18.9f,End},new[]{1f,1,0,0},new[]{0f,End},new[]{1f,1});
        ActorTracks(timeline,director,hero,hp,new[]{"IdleLeft","Walk","IdleLeft","Walk","IdleLeft"},new[]{0f,1,4.8f,13,16.5f,End});
        ActorTracks(timeline,director,gr,gp,new[]{"Drink","Walk","Idle","Walk","Idle"},new[]{0f,2.8f,4.4f,11.2f,14.7f,End});
        ActorTracks(timeline,director,enj,ep,new[]{"Idle","Walk","Idle","Walk","Idle"},new[]{0f,7,9,14.9f,18.8f,End});
        var door=timeline.CreateTrack<AnimationTrack>(null,"门扇开合");door.trackOffset=TrackOffset.ApplySceneOffsets;
        var opened=new AnimationClip{name="Intro_Door_Opened",frameRate=6};
        var openSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Cinematics/TavernUI/Environment/03_门动画/门扇_02_打开.png");
        AnimationUtility.SetObjectReferenceCurve(opened,new EditorCurveBinding{path="",type=typeof(UnityEngine.UI.Image),propertyName="m_Sprite"},new[]{new ObjectReferenceKeyframe{time=0,value=openSprite}});
        var settings=AnimationUtility.GetAnimationClipSettings(opened);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(opened,settings);AssetDatabase.CreateAsset(opened,Root+"/Animations/Intro/Intro_Door_Opened.anim");
        Segment(door,Motion("Door_Closed"),0,.6,"关闭");Segment(door,Motion("Door_Open"),.6,1.1,"开门",true);Segment(door,opened,1.1,2.2,"开着");
        Segment(door,Motion("Door_Close"),2.2,2.7,"关门",true);Segment(door,Motion("Door_Closed"),2.7,10.8,"关闭");Segment(door,Motion("Door_Open"),10.8,11.3,"开门",true);
        Segment(door,opened,11.3,19.1,"依次离场，保持开门");Segment(door,Motion("Door_Close"),19.1,19.6,"关门",true);Segment(door,Motion("Door_Closed"),19.6,End,"关闭");
        var fade=timeline.CreateTrack<AnimationTrack>(null,"结束淡出");var fadeClip=new AnimationClip{name="Intro_Fade",frameRate=30};Float(fadeClip,typeof(CanvasGroup),"m_Alpha",new[]{0f,19.7f,End},new[]{0f,0,1});
        AssetDatabase.CreateAsset(fadeClip,Root+"/Animations/Intro/Intro_Fade.anim");Segment(fade,fadeClip,0,End,"末尾黑场");
        Signals(timeline,director,dialogue);Save(timeline);Bind(stage,timeline);
        hero.rect.anchoredPosition=new Vector2(302,-68);hero.opacity.alpha=0;hero.image.sprite=First(Motion("Hero_IdleLeft"));
        gr.rect.anchoredPosition=new Vector2(204,-110);gr.opacity.alpha=1;gr.image.sprite=First(Motion("Grantaire_Drink"));
        enj.rect.anchoredPosition=new Vector2(159,-110);enj.opacity.alpha=1;enj.image.sprite=First(Motion("Enjolras_Idle"));
        return timeline;
    }
    static void Validate(GameObject stage,TimelineAsset timeline)
    {
        var director=stage.GetComponentInChildren<PlayableDirector>(true);Check(director.playableAsset==timeline,"Stage Director uses the completed introduction");
        foreach(var track in timeline.GetOutputTracks())Check(director.GetGenericBinding(track)!=null,"Track binding: "+track.name);
        var controller=stage.GetComponentInChildren<TavernNameDialogue>(true);
        Check(controller.conversation!=null&&controller.nameReplyOnSeparateSignal&&controller.echoNameAfterConfirmation,"Name echo and conditional second dialogue are separate steps");
        Check(controller.conversation.third.speaker=="昂若拉"&&controller.conversation.fourth.speaker=="昂若拉","Third and fourth dialogue both belong to Enjolras");
        var signalTrack=timeline.GetOutputTracks().OfType<SignalTrack>().Single();var receiver=director.GetGenericBinding(signalTrack) as SignalReceiver;
        foreach(var marker in signalTrack.GetMarkers().OfType<SignalEmitter>())
        {
            var reaction=receiver.GetReaction(marker.asset);Check(reaction!=null&&reaction.GetPersistentEventCount()>0&&reaction.GetPersistentTarget(0)==controller,"Signal reaction: "+marker.asset.name);
        }
        Check(signalTrack.GetMarkers().Count()==5,"Four dialogue steps plus one name-entry gate");
        Check(Math.Abs(timeline.duration-End)<.05,"Authored timeline has 20.5 seconds excluding interaction waits");
    }
    static void VerifyAndRender(GameObject stage,string output)
    {
        var director=stage.GetComponentInChildren<PlayableDirector>(true);var hero=Find(stage,"Hero");var gr=Find(stage,"Grantaire");var enj=Find(stage,"Enjolras");
        director.timeUpdateMode=DirectorUpdateMode.Manual;director.Play();
        Action<double> sample=t=>{director.time=t;director.Evaluate();Canvas.ForceUpdateCanvases();};
        try
        {
            sample(0);Check(hero.opacity.alpha<.01f&&gr.opacity.alpha>.99f,"Opening hides arriving hero and shows Grantaire");
            Check(gr.image.sprite==First(Motion("Grantaire_Drink")),"Opening actually samples Grantaire's drinking clip");
            sample(3);Check(hero.rect.anchoredPosition.x<285&&hero.rect.anchoredPosition.x>245&&hero.opacity.alpha>.99f,"Hero enters from the doorway");
            sample(5);Check(Math.Abs(hero.rect.anchoredPosition.x-245)<.1&&Math.Abs(gr.rect.anchoredPosition.x-217)<.1,"Grantaire blocks hero with space between actors");
            sample(6.5);Check(Math.Abs(gr.rect.anchoredPosition.x-217)<.1,"Blocking actor remains idle during naming");
            sample(9.2);Check(Math.Abs(enj.rect.anchoredPosition.x-187)<.1,"Enjolras approaches before his two dialogue nodes");
            sample(12);Check(gr.rect.anchoredPosition.x>217&&Math.Abs(hero.rect.anchoredPosition.x-245)<.1,"Grantaire leaves first while hero waits");
            sample(15.1);Check(gr.opacity.alpha<.01f&&hero.opacity.alpha>.99f&&enj.opacity.alpha>.99f,"Only Grantaire has exited at the first exit checkpoint");
            sample(17);Check(hero.opacity.alpha<.01f&&enj.opacity.alpha>.99f,"Hero exits second while Enjolras is still walking");
            sample(19);Check(gr.opacity.alpha<.01f&&hero.opacity.alpha<.01f&&enj.opacity.alpha<.01f,"All three actors have crossed the doorway before closing");
            var render=typeof(TavernScene3Import).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Static);
            foreach(var shot in new[]{new KeyValuePair<float,string>(0,"01_格朗泰尔喝酒"),new KeyValuePair<float,string>(3,"02_主角入门"),new KeyValuePair<float,string>(5,"03_拦住主角"),new KeyValuePair<float,string>(9.2f,"04_昂若拉走来"),new KeyValuePair<float,string>(15.5f,"05_依次离场")})
            {
                sample(shot.Key);render.Invoke(null,new object[]{stage,Path.Combine(output,shot.Value+".png")});
            }
        }
        finally{director.Stop();}
    }
    static void VerifyDialogue(GameObject stage)
    {
        var c=stage.GetComponentInChildren<TavernNameDialogue>(true);var oldName=RunState.PlayerName;bool oldIntroduced=RunState.ProtagonistIntroduced;
        var responses=c.responses;var clone=UnityEngine.Object.Instantiate(responses);const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var block=typeof(TavernNameDialogue).GetField("blockedThrough",flags);var begin=typeof(TavernNameDialogue).GetMethod("BeginEntry",flags);var second=typeof(TavernNameDialogue).GetMethod("BeginNameResponse",flags);var reply=typeof(TavernNameDialogue).GetMethod("BeginReply",flags);
        var d=c.director;int stops=0;Action<PlayableDirector> onStop=x=>stops++;d.stopped+=onStop;
        try
        {
            c.responses=clone;clone.specialNames[0].reply="核验第二段 {playerName}";d.Play();RunState.ProtagonistIntroduced=false;RunState.PlayerName="";
            reply.Invoke(c,new object[]{"格朗泰尔","核验第一段"});Check(c.Waiting&&d.state==PlayState.Paused,"First dialogue holds its Timeline");block.SetValue(c,-1);c.continueButton.onClick.Invoke();
            begin.Invoke(c,null);block.SetValue(c,-1);c.input.text="Enjolras";c.confirmButton.onClick.Invoke();
            Check(c.Waiting&&c.replySpeaker.text=="主角"&&c.replyText.text=="Enjolras","Hero replies with the confirmed player name before special response");
            block.SetValue(c,-1);c.continueButton.onClick.Invoke();second.Invoke(c,null);
            Check(c.Waiting&&c.SelectedRuleId=="enjolras"&&c.replyText.text=="核验第二段 Enjolras","Second dialogue selects and formats the special name response");
            block.SetValue(c,-1);c.continueButton.onClick.Invoke();
            foreach(string line in new[]{"核验第三段","核验第四段"})
            {
                reply.Invoke(c,new object[]{"昂若拉",line});Check(c.Waiting&&c.replySpeaker.text=="昂若拉"&&d.state==PlayState.Paused,"Enjolras dialogue holds: "+line);block.SetValue(c,-1);c.continueButton.onClick.Invoke();
            }
            Check(!c.Waiting&&d.state==PlayState.Playing&&stops==0,"All four dialogue steps resume without stopping the cutscene");
        }
        finally{d.stopped-=onStop;d.Stop();c.responses=responses;UnityEngine.Object.DestroyImmediate(clone);RunState.PlayerName=oldName;RunState.ProtagonistIntroduced=oldIntroduced;}
    }
    static void Run(string output)
    {
        Directory.CreateDirectory(output);passed.Clear();var r=new Report();var scene=SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();GameObject contents=null;
        try
        {
            Check(scene.path=="Assets/Scenes/3.unity","Scene 3 is active in edit mode");Check(GameFont.Silver!=null,"Existing game font resolves");
            string workHash=Hash(Workbench),responsePath=Root+"/Data/NameResponses.asset",responseHash=Hash(responsePath);
            Folder(Root+"/Animations/Intro");Folder(Root+"/Data");Folder(Root+"/Signals");Directory.CreateDirectory(Path.Combine(output,"备份"));
            foreach(string path in new[]{Stage,Workbench,responsePath}){string target=Path.Combine(output,"备份",Path.GetFileName(path));if(!File.Exists(target))File.Copy(path,target);}
            var lines=AssetDatabase.LoadAssetAtPath<TavernDialogueLines>(Lines);if(lines==null){lines=ScriptableObject.CreateInstance<TavernDialogueLines>();lines.name="TavernDialogue";AssetDatabase.CreateAsset(lines,Lines);}
            contents=PrefabUtility.LoadPrefabContents(Stage);var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(Intro);
            if(timeline==null)timeline=Build(contents);else Bind(contents,timeline);
            Validate(contents,timeline);PrefabUtility.SaveAsPrefabAsset(contents,Stage);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            contents=PrefabUtility.LoadPrefabContents(Stage);Validate(contents,timeline);VerifyAndRender(contents,output);VerifyDialogue(contents);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            var live=scene.GetRootGameObjects().Single(g=>g.name=="TavernUI_AnimationStage");var director=live.GetComponentInChildren<PlayableDirector>(true);Undo.RecordObject(director,"接入完整酒馆演出");Bind(live,timeline);PrefabUtility.RecordPrefabInstancePropertyModifications(director);Validate(live,timeline);
            var table=Resources.Load<ChapterTable>("Campaign/ChapterTable");Check(table.chapters[1].openingSequence.cinematicPrefab==AssetDatabase.LoadAssetAtPath<GameObject>(Stage)&&!table.chapters[1].openingSequence.allowSkip,"Chapter two still uses this complete tavern prefab with skip disabled");
            r.workbenchPreserved=Hash(Workbench)==workHash;r.nameResponsesPreserved=Hash(responsePath)==responseHash;Check(r.workbenchPreserved&&r.nameResponsesPreserved,"Previous workbench and user response text are preserved");
            r.scenePreserved=roots.All(g=>g!=null&&g.scene==scene);EditorSceneManager.MarkSceneDirty(scene);r.sceneDirty=scene.isDirty;Check(r.scenePreserved,"Existing scene roots and unsaved author edits preserved");r.duration=(float)timeline.duration;
            Selection.activeGameObject=director.gameObject;r.success=true;
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally{if(contents!=null)PrefabUtility.UnloadPrefabContents(contents);r.checks=passed.Count;r.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"原生演出报告.json"),JsonUtility.ToJson(r,true));}
    }
}
#endif
