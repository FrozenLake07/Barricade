#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TitleSettingsMenuPlayVerification
{
    const string Pending="1832.TitleSettingsMenu.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TitleSettingsMenuPlay.request"));
    static TitleSettingsMenuPlayVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var output=File.ReadAllText(Request).Trim();File.Delete(Request);SessionState.SetString(Pending,output);SessionState.SetBool(Pending+".active",true);
            previousFrame=-1;lastAdvance=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Pending+".active",false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(Time.frameCount!=previousFrame){previousFrame=Time.frameCount;lastAdvance=EditorApplication.timeSinceStartup;}
        if(EditorApplication.timeSinceStartup-lastAdvance>1){EditorApplication.isPaused=true;EditorApplication.Step();}
        string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;
        SessionState.EraseString(Pending);Application.runInBackground=true;
        var story=StoryDirector.Current;
        if(story==null)story=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Campaign/CampaignRoot")).GetComponent<StoryDirector>();
        story.gameObject.AddComponent<TitleSettingsMenuPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class TitleSettingsMenuPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Real grenade prefab, physics wall contact and scheduled WaveThreat arrival in actual chapter defense; no full playthrough or hardware input injection. User scene not saved.";}
    readonly List<string> passed=new List<string>();Report report;string output;StoryDirector story;float listenerVolume;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    CampaignCinematicView Film=>story.GetComponent<CampaignCinematicView>();
    T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    void Check(bool ok,string text){if(!ok)throw new Exception(text);passed.Add(text);}
    public void Begin(string path){output=path;story=GetComponent<StoryDirector>();report=new Report{startFrame=Time.frameCount};listenerVolume=AudioListener.volume;AudioListener.volume=0;StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            object current=null;bool more=false;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)current=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();AudioListener.volume=listenerVolume;AudioListener.pause=false;Time.timeScale=1;
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"标题菜单Play检查.json"),JsonUtility.ToJson(report,true));TitleSettingsMenuPlayVerification.Finished();
    }
    IEnumerator Until(Func<bool> ready,bool skipCards=false)
    {
        for(int i=0;i<600;i++){if(ready())yield break;if(skipCards)story.DebugSkip();yield return null;}throw new TimeoutException("Play check waiting for "+story.Phase);
    }
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    IEnumerator Progress(CampaignActor actor,float progress)
    {
        Set(actor,"elapsed",actor.duration*progress);yield return Frames(2);
    }
    BombHP Bomb(Vector3 position,int group)
    {
        var go=Instantiate(Resources.Load<GameObject>("arrow/Bomb"),position,Quaternion.identity);var hp=go.GetComponent<BombHP>();hp.Configure(group,position.y);return hp;
    }
    void CaptureUi(int width,int height,string name)
    {
        var canvas=Get<CampaignUI>(story,"ui").View.GetComponent<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
        var node=new GameObject("UiCapture");var cam=node.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.nearClipPlane=.1f;cam.farClipPlane=100;
        var target=new RenderTexture(width,height,24);target.Create();var active=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{cam.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());}
        finally{canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;RenderTexture.active=active;target.Release();Destroy(target);Destroy(texture);Destroy(node);Canvas.ForceUpdateCanvases();}
    }

    Rect ScreenRect(RectTransform rect)
    {
        Canvas.ForceUpdateCanvases();var corners=new Vector3[4];rect.GetWorldCorners(corners);
        var canvas=rect.GetComponentInParent<Canvas>();var a=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,corners[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
    }
    UnityEngine.UI.Button TitleSettings(CampaignMenuView menu)=>menu.titleMenu.transform.parent.Find("Settings").GetComponent<UnityEngine.UI.Button>();
    void CheckLayout(string label)
    {
        var menu=FindFirstObjectByType<CampaignMenuView>();var settings=TitleSettings(menu);
        var buttons=new[]{menu.newGame,menu.continueGame,settings,menu.titleQuit};
        for(int i=0;i<buttons.Length;i++)
        {
            var rect=ScreenRect(buttons[i].GetComponent<RectTransform>());
            Check(rect.width>1&&rect.height>1,label+" visible button "+buttons[i].name);
            Check(buttons[i].GetComponent<ParisMenuAccent>()!=null&&buttons[i].colors.normalColor==menu.newGame.colors.normalColor,label+" shared menu style "+buttons[i].name);
            Check(buttons[i].GetComponentInChildren<UnityEngine.UI.Text>().alignment==TextAnchor.MiddleLeft,label+" shared text alignment "+buttons[i].name);
            for(int j=i+1;j<buttons.Length;j++)Check(!rect.Overlaps(ScreenRect(buttons[j].GetComponent<RectTransform>())),label+" no overlap "+buttons[i].name+" / "+buttons[j].name);
        }
        Check(Mathf.Abs(ScreenRect(settings.GetComponent<RectTransform>()).x-ScreenRect(menu.newGame.GetComponent<RectTransform>()).x)<1,label+" settings follows the cover's left/right column");
    }
    IEnumerator Test()
    {
        yield return Frames(4);DebugConsole.Current?.SetOpen(false);yield return Until(()=>story.Phase==CampaignPhase.Title);
        CheckLayout("Actual initial title");
        var ui=Get<CampaignUI>(story,"ui");var screen=ui.View;var menu=FindFirstObjectByType<CampaignMenuView>();
        menu.titleMenu.SetActive(true);screen.prompt.gameObject.SetActive(false);
        foreach(int cover in new[]{0,1,2,StartScreenView.FirstLaunchCoverIndex})
        {
            Set(screen,"selectedTitleCover",cover);screen.ApplyTitleCover(false);yield return Frames(2);CheckLayout("Cover "+cover);CaptureUi(1280,720,"标题封面"+cover);
        }
        Set(screen,"selectedTitleCover",0);screen.ApplyTitleCover(false);yield return Frames(2);
        var settings=TitleSettings(menu);settings.GetComponent<ParisMenuAccent>().OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));yield return Frames(2);CaptureUi(1280,720,"标题设置悬停");settings.GetComponent<ParisMenuAccent>().OnPointerExit(null);
        settings.onClick.Invoke();yield return Frames(2);Check(GameSettingsView.IsOpen,"Styled title settings button opens actual settings page");CaptureUi(1280,720,"设置统一样式");
        var page=GameSettingsView.Current.transform.Find("SettingsPage");Check(page.GetComponentsInChildren<UnityEngine.UI.Button>().All(b=>b.GetComponent<ParisMenuAccent>()!=null),"Settings controls all reuse the original menu accent instead of generic blocks");
        GameSettingsView.Current.Close();yield return Frames(2);Check(!GameSettingsView.IsOpen,"Settings returns to title normally");
        menu.titleMenu.SetActive(false);screen.prompt.gameObject.SetActive(true);Check(settings.gameObject.activeInHierarchy,"Settings remains accessible with first-launch prompt");Check(!ScreenRect(settings.GetComponent<RectTransform>()).Overlaps(ScreenRect(screen.prompt.rectTransform)),"First-launch prompt does not overlap settings");CaptureUi(960,540,"首次提示960");menu.titleMenu.SetActive(true);screen.prompt.gameObject.SetActive(false);
        StartCoroutine(story.DebugChapter(3,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&WaveDirector.Current!=null);
        StartCoroutine(story.DebugPhase("title"));yield return Until(()=>story.Phase==CampaignPhase.Title);yield return Frames(4);CheckLayout("Return from gameplay");
        menu=FindFirstObjectByType<CampaignMenuView>();Check(menu.titleMenu.transform.parent.GetComponentsInChildren<UnityEngine.UI.Button>(true).Count(b=>b.name=="Settings")==1,"Returning to title creates no duplicate settings buttons");CaptureUi(1280,720,"回到标题");
        var pause=menu.pausePage.transform.Find("Settings").GetComponent<UnityEngine.UI.Button>();Check(pause.GetComponent<ParisMenuAccent>()!=null,"Pause settings entry also matches the original menu style");
        report.coverage="Actual first title, all three covers plus first-launch cover, repeated cover layout, hover/open/close, first prompt at 960x540 and gameplay-to-title return. Native Canvas screenshots inspected. No build, disk-save write or scene save.";
    }
}
#endif
