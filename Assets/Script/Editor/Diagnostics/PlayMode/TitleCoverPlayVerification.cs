#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TitleCoverPlayVerification
{
    const string Pending="Barricade.TitleCovers.Pending";
    static int frame=-1;static double changed;static bool stepping;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/TitleCoverPlay.request"));
    static TitleCoverPlayVerification(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Pending+".active",false)){EditorApplication.isPaused=false;PlayerSettings.runInBackground=SessionState.GetBool(Pending+".background",false);RestoreFirstCoverPreference();SessionState.EraseBool(Pending+".active");}};}
    static void RestoreFirstCoverPreference()
    {
        if(SessionState.GetBool(Pending+".hadFirstKey",false))PlayerPrefs.SetInt(StartScreenView.FirstLaunchCoverSeenKey,SessionState.GetInt(Pending+".firstValue",0));
        else PlayerPrefs.DeleteKey(StartScreenView.FirstLaunchCoverSeenKey);
        PlayerPrefs.Save();
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string firstRequest=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/FirstLaunchCoverPlay.request"));
        bool firstScenario=File.Exists(firstRequest);string request=firstScenario?firstRequest:Request;
        if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            string output=File.ReadAllText(request).Trim();File.Delete(request);
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty){File.WriteAllText(Path.Combine(output,"原生Play验证.json"),"{\"success\":false,\"error\":\"当前场景存在未保存修改，未切换场景或启动回归。\"}");return;}
            if(scene.name!="Title")UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            SessionState.SetBool(Pending+".firstScenario",firstScenario);
            SessionState.SetBool(Pending+".hadFirstKey",PlayerPrefs.HasKey(StartScreenView.FirstLaunchCoverSeenKey));
            SessionState.SetInt(Pending+".firstValue",PlayerPrefs.GetInt(StartScreenView.FirstLaunchCoverSeenKey,0));
            if(firstScenario){PlayerPrefs.DeleteKey(StartScreenView.FirstLaunchCoverSeenKey);PlayerPrefs.Save();}
            SessionState.SetBool(Pending+".active",true);SessionState.SetBool(Pending+".background",PlayerSettings.runInBackground);PlayerSettings.runInBackground=true;
            frame=-1;changed=EditorApplication.timeSinceStartup;stepping=false;SessionState.SetString(Pending,output);EditorApplication.isPaused=false;EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending+".active",false))return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(frame!=Time.frameCount){frame=Time.frameCount;changed=EditorApplication.timeSinceStartup;}
        if(stepping||EditorApplication.timeSinceStartup-changed>1){stepping=true;EditorApplication.isPaused=true;EditorApplication.Step();}
        if(StoryDirector.Current!=null&&!string.IsNullOrEmpty(SessionState.GetString(Pending,"")))
        {string output=SessionState.GetString(Pending,"");SessionState.EraseString(Pending);StoryDirector.Current.gameObject.AddComponent<TitleCoverPlayProbe>().Begin(output);}
    }
}

public sealed class TitleCoverPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,nativePlayVerified;public int startFrame,endFrame,checks;public string error;public string[] tested;public int[] sequence;}
    Report report;string output;StoryDirector story;readonly List<string> tested=new List<string>();readonly List<int> sequence=new List<int>();
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    void Require(bool ok,string why){report.checks++;tested.Add(why);if(!ok)throw new Exception(why);}
    public void Begin(string directory){output=directory;story=StoryDirector.Current;report=new Report{startFrame=Time.frameCount};StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            bool more=false;object next=null;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)next=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(next is IEnumerator child){stack.Push(child);continue;}yield return next;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();Time.timeScale=1;AudioListener.pause=false;
        report.endFrame=Time.frameCount;report.nativePlayVerified=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.tested=tested.ToArray();report.sequence=sequence.ToArray();
        File.WriteAllText(Path.Combine(output,"封面原生Play验证.json"),JsonUtility.ToJson(report,true));EditorApplication.isPlaying=false;
    }
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    StartScreenView View=>FindFirstObjectByType<StartScreenView>();
    IEnumerator Test()
    {
        yield return Frames(3);var style=ParisUI.Style;Require(style.titleCovers.Length==3&&style.titleCovers.All(s=>s!=null),"三版封面均随工程加载");
        foreach(var sprite in style.titleCovers)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            Require(sprite.rect.width==320&&sprite.rect.height==180&&importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"像素封面导入设置正确："+sprite.name);
        }
        if(SessionState.GetBool("Barricade.TitleCovers.Pending.firstScenario",false))
        {
            var view=View;var background=view.titlePage.transform.Find("Background").GetComponent<UnityEngine.UI.Image>();
            Require(style.firstLaunchCover!=null&&style.firstLaunchCover.rect.width==1672&&style.firstLaunchCover.rect.height==941,"新封面原尺寸导入");
            Require(!style.titleCovers.Contains(style.firstLaunchCover),"首次封面独立于随机池");
            Require(view.TitleCoverIndex==StartScreenView.FirstLaunchCoverIndex&&background.sprite==style.firstLaunchCover,"首次进入实际显示葬礼封面");
            Require(PlayerPrefs.GetInt(StartScreenView.FirstLaunchCoverSeenKey)==1,"首次展示已保存本机记录");
            ParisUITheme.Apply(view);Require(background.sprite==style.firstLaunchCover,"刷新布局不会提前换掉首次封面");
            Require(view.titlePage.transform.Find("GameTitleMark").GetComponent<RectTransform>().anchorMin.x>.5f,"首次封面标题位于右侧天空");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"首次进入_主界面Play.png"));yield return Frames(3);
            yield return story.DebugPhase("title");yield return Frames(3);
            Require(View.TitleCoverIndex>=0&&View.TitleCoverIndex<3,"返回标题后不再显示首次封面");
            typeof(StartScreenView).GetMethod("ResetCoverHistory",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            View.ShowTitle();Require(View.TitleCoverIndex>=0&&View.TitleCoverIndex<3,"重置会话随机状态仍读取已展示记录");
            PlayerPrefs.Save();Require(PlayerPrefs.GetInt(StartScreenView.FirstLaunchCoverSeenKey)==1,"返回标题与会话重置保留已展示记录");
        }
        var before=JsonUtility.ToJson(UnityEngine.Random.state);int previous=-1;var visited=new HashSet<int>();
        for(int i=0;i<24;i++)
        {
            View.ShowTitle();int next=View.TitleCoverIndex;visited.Add(next);sequence.Add(next);
            Require(next>=0&&next<3&&next!=previous,"随机封面有效且不连续重复："+i);previous=next;
        }
        Require(visited.Count==3,"随机序列覆盖三版封面");Require(before==JsonUtility.ToJson(UnityEngine.Random.state),"抽封面不消耗战斗Unity随机数");
        for(int index=0;index<3;index++)
        {
            var view=View;typeof(StartScreenView).GetField("selectedTitleCover",Private).SetValue(view,index);view.ApplyTitleCover();
            var menu=CampaignMenuView.Ensure(view);view.prompt.gameObject.SetActive(false);menu.titleMenu.SetActive(true);Canvas.ForceUpdateCanvases();yield return Frames(3);
            var background=view.titlePage.transform.Find("Background").GetComponent<UnityEngine.UI.Image>();
            Require(background.sprite==style.titleCovers[index]&&background.color==Color.white,"实际显示新封面原色："+index);
            var fit=background.GetComponent<UnityEngine.UI.AspectRatioFitter>();Require(fit!=null&&Mathf.Abs(fit.aspectRatio-16f/9)<.001f&&fit.aspectMode==UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent,"封面等比铺满："+index);
            var title=view.titlePage.transform.Find("GameTitle").GetComponent<UnityEngine.UI.Text>();
            Require(!title.enabled&&title.text==ParisUI.GameTitle&&style.titleArtwork!=null,"标题完整可见且不截断："+index);
            Require(new[]{menu.newGame,menu.continueGame,menu.titleQuit}.All(b=>b.gameObject.activeInHierarchy),"三枚菜单按钮均显示："+index);
            Require(index==1?menu.newGame.GetComponent<RectTransform>().anchorMin.x>.5f:menu.newGame.GetComponent<RectTransform>().anchorMin.x<.2f,"菜单根据构图移至正确一侧："+index);
            var logo=view.titlePage.transform.Find("GameTitleMark").GetComponent<UnityEngine.UI.Image>();
            Require(logo.enabled&&logo.sprite==style.titleArtwork&&logo.preserveAspect&&!logo.raycastTarget&&logo.rectTransform.rect.width>0&&Mathf.Abs(logo.rectTransform.anchorMin.x-title.rectTransform.anchorMin.x)<.001f,"新字标显示且无文字重叠："+index);
            ParisUITheme.Apply(view);Require(view.TitleCoverIndex==index&&background.sprite==style.titleCovers[index],"刷新主题不重新抽图或恢复旧背景："+index);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"封面"+(char)('A'+index)+"_主界面Play.png"));yield return Frames(3);
            int stable=view.TitleCoverIndex;yield return Frames(4);Require(view.TitleCoverIndex==stable,"停留主界面时保持同一封面："+index);
        }
        for(int i=0;i<3;i++)
        {
            View.ShowTitle();int old=View.TitleCoverIndex;yield return story.DebugPhase("title");yield return Frames(3);
            Require(story.Phase==CampaignPhase.Title&&View.TitleCoverIndex!=old,"实际重载返回主界面不连续重复："+i);
        }
    }
}
#endif
