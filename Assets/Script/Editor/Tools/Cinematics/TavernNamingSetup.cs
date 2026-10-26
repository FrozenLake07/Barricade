#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TavernNamingSetup
{
    const string Root="Assets/Cinematics/TavernUI";
    const string Stage=Root+"/Prefabs/TavernUI_AnimationStage.prefab";
    const string Table=Root+"/Data/NameResponses.asset";
    const string Signal=Root+"/Signals/AskPlayerName.signal";
    [Serializable] sealed class Report
    {
        public bool success,sceneDirty,sceneRootsPreserved;
        public int checks,legacyNameGatesDisabled;
        public string error,scene,table=Table,signal=Signal,fingerprintBefore,fingerprintAfter;
        public string[] passed;
    }
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TavernNamingSetup.request"));
    static readonly List<string> passed=new List<string>();
    static TavernNamingSetup(){EditorApplication.update+=Poll;}
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
    static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
    }
    static UnityEngine.UI.Image Surface(RectTransform rect,Color color)
    {var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=true;return image;}
    static UnityEngine.UI.Text Label(string name,Transform parent,string text,float x,float y,float width,float height,int size)
    {
        var r=Rect(name,parent,x,y,width,height);var t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.text=text;t.font=GameFont.Silver;t.fontSize=size;
        t.color=new Color(.92f,.9f,.84f);t.supportRichText=false;t.raycastTarget=false;t.alignment=TextAnchor.MiddleLeft;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
    }
    static UnityEngine.UI.Button Button(string name,Transform parent,string text,float x,float y,float width,float height)
    {
        var r=Rect(name,parent,x,y,width,height);var image=Surface(r,new Color(.19f,.18f,.16f));var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;
        b.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};var label=Label("Label",r,text,0,0,width,height,8);label.alignment=TextAnchor.MiddleCenter;return b;
    }
    static NameResponseTable MakeTable()
    {
        var table=AssetDatabase.LoadAssetAtPath<NameResponseTable>(Table);if(table!=null)return table;
        table=ScriptableObject.CreateInstance<NameResponseTable>();table.name="NameResponses";
        table.specialNames=new[]{new NameResponseTable.Response{id="enjolras",aliases=new[]{"昂若拉","Enjolras"}},new NameResponseTable.Response{id="grantaire",aliases=new[]{"格朗泰尔","Grantaire"}}};
        AssetDatabase.CreateAsset(table,Table);return table;
    }
    static TavernNameDialogue Build(GameObject stage,NameResponseTable table,SignalAsset signal)
    {
        Check(stage.GetComponentInChildren<TavernNameDialogue>(true)==null,"No existing authored name controller is overwritten");
        var parent=stage.transform.Find("Stage_320x180/Dialogue");Check(parent!=null,"Existing blank Dialogue is retained");
        var c=parent.gameObject.AddComponent<TavernNameDialogue>();c.responses=table;c.director=stage.GetComponentInChildren<PlayableDirector>(true);
        c.authoredDialogue=parent.Find("Line01").GetComponent<CanvasGroup>();
        var entry=Rect("NameEntry",parent,42,100,236,78);Surface(entry,new Color(.035f,.035f,.04f,.98f));c.entryPanel=entry.gameObject;
        Label("Prompt",entry,"姓名",8,3,220,14,9);
        var field=Rect("PlayerNameInput",entry,8,22,220,18);var surface=Surface(field,new Color(.14f,.14f,.14f));
        c.input=field.gameObject.AddComponent<UnityEngine.UI.InputField>();c.input.targetGraphic=surface;
        var value=Label("Text",field,"",4,0,212,18,11);var placeholder=Label("Placeholder",field,"",4,0,212,18,9);placeholder.color=new Color(.5f,.5f,.5f);
        c.input.textComponent=value;c.input.placeholder=placeholder;c.input.lineType=UnityEngine.UI.InputField.LineType.SingleLine;c.input.contentType=UnityEngine.UI.InputField.ContentType.Standard;
        c.input.characterLimit=0;c.input.customCaretColor=true;c.input.caretColor=new Color(.92f,.9f,.84f);c.input.selectionColor=new Color(.45f,.25f,.2f,.65f);
        c.errorText=Label("Validation",entry,"",8,42,220,11,7);c.errorText.color=new Color(.9f,.6f,.5f);
        c.confirmButton=Button("Confirm",entry,"确认",8,56,104,16);c.unnamedButton=Button("Unnamed",entry,"暂不命名",124,56,104,16);
        var reply=Rect("NameReply",parent,8,138,304,40);Surface(reply,new Color(.035f,.035f,.04f,.98f));c.replyPanel=reply.gameObject;
        c.replySpeaker=Label("Name",reply,"",8,2,260,12,8);c.replySpeaker.color=new Color(.85f,.75f,.55f);
        c.replyText=Label("Content",reply,"",8,15,260,23,9);c.replyText.alignment=TextAnchor.UpperLeft;c.continueButton=Button("Continue",reply,"继续",274,23,24,13);
        entry.gameObject.SetActive(false);reply.gameObject.SetActive(false);
        var timeline=(TimelineAsset)c.director.playableAsset;
        var track=timeline.GetOutputTracks().OfType<SignalTrack>().SingleOrDefault(t=>t.name=="姓名输入（Signal）");
        if(track==null)
        {
            track=timeline.CreateTrack<SignalTrack>(null,"姓名输入（Signal）");var marker=track.CreateMarker<SignalEmitter>(1);marker.asset=signal;marker.emitOnce=false;marker.retroactive=false;Save(timeline);
        }
        var receiver=c.director.GetComponent<SignalReceiver>()??c.director.gameObject.AddComponent<SignalReceiver>();
        var reaction=new UnityEvent();UnityEventTools.AddPersistentListener(reaction,c.AskName);receiver.AddReaction(signal,reaction);c.director.SetGenericBinding(track,receiver);
        return c;
    }
    static void Validate(GameObject stage,NameResponseTable table,SignalAsset signal)
    {
        var c=stage.GetComponentInChildren<TavernNameDialogue>(true);Check(c!=null&&c.responses==table,"Name controller and editable response asset resolve");
        Check(c.input!=null&&c.input.textComponent.font==GameFont.Silver,"Input uses game font");
        Check(!c.entryPanel.activeSelf&&!c.replyPanel.activeSelf,"Naming and reply panels start hidden");
        Check(c.replySpeaker.text==""&&c.replyText.text=="","Narrative text remains empty");
        Check(c.input.lineType==UnityEngine.UI.InputField.LineType.SingleLine&&c.input.characterLimit==0,"Input delegates grapheme limit to existing name validation");
        var timeline=(TimelineAsset)c.director.playableAsset;var track=timeline.GetOutputTracks().OfType<SignalTrack>().Single(t=>t.name=="姓名输入（Signal）");
        var receiver=c.director.GetGenericBinding(track) as SignalReceiver;Check(receiver!=null,"Director signal receiver binding resolves");
        var reaction=receiver.GetReaction(signal);Check(reaction!=null&&reaction.GetPersistentEventCount()==1&&reaction.GetPersistentTarget(0)==c&&reaction.GetPersistentMethodName(0)=="AskName","Persistent Signal invokes this prefab's AskName");
        Check(track.GetMarkers().OfType<SignalEmitter>().Any(m=>m.asset==signal&&m.time>0&&m.time<timeline.duration),"Name gate is before Timeline completion");
    }
    static void VerifyFlow(GameObject stage,NameResponseTable table)
    {
        string oldName=RunState.PlayerName;bool oldIntroduced=RunState.ProtagonistIntroduced;var oldEvents=EventSystem.current;
        var c=stage.GetComponentInChildren<TavernNameDialogue>(true);var originalResponses=c.responses;var test=UnityEngine.Object.Instantiate(table);
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var begin=typeof(TavernNameDialogue).GetMethod("BeginEntry",flags);var block=typeof(TavernNameDialogue).GetField("blockedThrough",flags);
        var stopped=0;Action<PlayableDirector> observe=d=>stopped++;
        var director=c.director;director.stopped+=observe;director.timeUpdateMode=DirectorUpdateMode.Manual;
        try
        {
            Check(table.Resolve("  eNjOlRaS ").id=="enjolras","Alias matching trims and ignores English case");
            Check(table.Resolve("Ｅｎｊｏｌｒａｓ").id=="enjolras","Full-width English aliases match");
            Check(table.Resolve("格朗泰尔").id=="grantaire","Chinese alias matches");
            Check(table.Resolve("EnjolrasX").id=="default","Aliases require complete names");
            Check(table.Resolve("").id=="unnamed","Unnamed branch is separate");
            Check(!ProtagonistNameRules.TryNormalize("abc\n",out _)&&!ProtagonistNameRules.TryNormalize("1234567890123",out _),"Existing control-character and length rejection retained");
            c.responses=test;test.specialNames[0].speaker="核验";test.specialNames[0].reply="核验 {playerName}";
            RunState.PlayerName="";RunState.ProtagonistIntroduced=false;
            director.Play();director.time=1.1;director.Evaluate();
            Check(!c.Waiting&&!RunState.ProtagonistIntroduced,"Editor Timeline signals do not ask or change player names");
            begin.Invoke(c,null);Check(c.IsNaming&&c.entryPanel.activeSelf&&director.state==PlayState.Paused,"Actual controller opens input and pauses its Director");
            Check(TavernNameDialogue.InputCaptured,"Naming captures transition input");
            block.SetValue(c,-1);c.input.text="\n";c.confirmButton.onClick.Invoke();Check(!RunState.ProtagonistIntroduced&&c.IsNaming&&c.errorText.text!="","Actual invalid input stays at naming gate");
            c.input.text=" Enjolras ";c.confirmButton.onClick.Invoke();
            Check(RunState.PlayerName=="Enjolras"&&RunState.ProtagonistIntroduced&&c.SelectedRuleId=="enjolras","Actual confirmation stores normalized name and resolves special rule");
            Check(c.replyPanel.activeSelf&&c.replyText.text=="核验 Enjolras"&&director.state==PlayState.Paused,"Selected response formats name and holds Timeline");
            c.continueButton.onClick.Invoke();Check(c.Waiting,"Same-frame confirmation cannot dismiss response");
            block.SetValue(c,-1);c.continueButton.onClick.Invoke();Check(!c.Waiting&&director.state==PlayState.Playing&&stopped==0,"Response confirmation resumes without stopping the transition");
            var snapshot=CampaignStateCodec.Capture(typeof(RunState),o=>null,"PlayerName","ProtagonistIntroduced");RunState.PlayerName="";RunState.ProtagonistIntroduced=false;
            CampaignStateCodec.Restore(typeof(RunState),snapshot,k=>null);Check(RunState.PlayerName=="Enjolras"&&RunState.ProtagonistIntroduced,"Existing save codec round-trips the migrated name fields");
            RunState.ProtagonistIntroduced=false;begin.Invoke(c,null);block.SetValue(c,-1);c.unnamedButton.onClick.Invoke();
            Check(RunState.PlayerName==""&&RunState.ProtagonistIntroduced&&c.SelectedRuleId=="unnamed"&&!c.Waiting&&director.state==PlayState.Playing,"Unnamed branch saves empty name and blank reply resumes immediately");
        }
        finally
        {
            director.stopped-=observe;director.Stop();c.responses=originalResponses;UnityEngine.Object.DestroyImmediate(test);
            RunState.PlayerName=oldName;RunState.ProtagonistIntroduced=oldIntroduced;EventSystem.current=oldEvents;
        }
    }
    static void Run(string output)
    {
        Directory.CreateDirectory(output);passed.Clear();var r=new Report();var scene=SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();GameObject contents=null;
        try
        {
            Check(scene.path=="Assets/Scenes/3.unity","Scene 3 remains active");r.scene=scene.path;
            var live=roots.Single(g=>g.name=="TavernUI_AnimationStage");Check(live.GetComponentInChildren<TavernNameDialogue>(true)==null||AssetDatabase.GetAssetPath(live.GetComponentInChildren<TavernNameDialogue>(true).responses)==Table,"Only this setup owns an existing naming controller");
            var chapters=Resources.Load<ChapterTable>("Campaign/ChapterTable");var cinema=Resources.Load<CampaignCinematics>("Campaign/Cinematics");var chapter=chapters.chapters[1];
            var opening=chapter.openingSequence;Check(opening==null||(opening.cinematicPrefab==null&&opening.timeline==null)||opening.cinematicPrefab==AssetDatabase.LoadAssetAtPath<GameObject>(Stage),"Existing chapter-two custom opening is preserved");
            r.fingerprintBefore=CampaignTelemetry.Fingerprint(chapters);
            Folder(Root+"/Data");Folder(Root+"/Signals");
            Directory.CreateDirectory(Path.Combine(output,"备份"));
            foreach(string path in new[]{Stage,AssetDatabase.GetAssetPath(chapters),AssetDatabase.GetAssetPath(cinema),AssetDatabase.GetAssetPath(live.GetComponentInChildren<PlayableDirector>(true).playableAsset)})
            {var backup=Path.Combine(output,"备份",Path.GetFileName(path));if(!File.Exists(backup))File.Copy(path,backup);}
            var table=MakeTable();var signal=AssetDatabase.LoadAssetAtPath<SignalAsset>(Signal);
            if(signal==null){signal=ScriptableObject.CreateInstance<SignalAsset>();signal.name="AskPlayerName";AssetDatabase.CreateAsset(signal,Signal);}
            contents=PrefabUtility.LoadPrefabContents(Stage);if(contents.GetComponentInChildren<TavernNameDialogue>(true)==null)Build(contents,table,signal);Validate(contents,table,signal);PrefabUtility.SaveAsPrefabAsset(contents,Stage);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            live=scene.GetRootGameObjects().Single(g=>g.name=="TavernUI_AnimationStage");Validate(live,table,signal);
            contents=PrefabUtility.LoadPrefabContents(Stage);Validate(contents,table,signal);VerifyFlow(contents,table);
            var c=contents.GetComponentInChildren<TavernNameDialogue>(true);c.entryPanel.SetActive(true);c.authoredDialogue.alpha=0;
            typeof(TavernScene3Import).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{contents,Path.Combine(output,"姓名输入_UI预览.png")});
            PrefabUtility.UnloadPrefabContents(contents);contents=null;
            chapter.openingSequence=chapter.openingSequence??new ChapterTransition();chapter.openingSequence.cinematicPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Stage);chapter.openingSequence.timeline=null;chapter.openingSequence.allowSkip=false;chapter.openingSequence.placeholderSeconds=0;chapter.openingSequence.waitForSignal=false;chapter.openingSequence.caption="";
            foreach(var route in cinema.routes.Where(route=>route.chapter==2&&route.key=="chapter-entry"))if(route.nameAfterShot>=0){route.nameAfterShot=-1;r.legacyNameGatesDisabled++;}
            chapters.Validate();cinema.Validate();Save(chapters);Save(cinema);r.fingerprintAfter=CampaignTelemetry.Fingerprint(chapters);
            Check(chapter.openingSequence.cinematicPrefab!=null&&!chapter.openingSequence.allowSkip,"Chapter two opening uses tavern prefab with skip disabled");
            Check(cinema.routes.Where(route=>route.chapter==2&&route.key=="chapter-entry").All(route=>route.nameAfterShot==-1),"Legacy picture naming gates disabled");
            EditorSceneManager.MarkSceneDirty(scene);r.sceneDirty=scene.isDirty;r.sceneRootsPreserved=roots.All(g=>g!=null&&g.scene==scene);Check(r.sceneRootsPreserved,"Original dirty scene roots and author edits preserved");
            Selection.activeObject=table;r.success=true;
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally
        {
            if(contents!=null)PrefabUtility.UnloadPrefabContents(contents);r.checks=passed.Count;r.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"原生接入报告.json"),JsonUtility.ToJson(r,true));
        }
    }
}
#endif
