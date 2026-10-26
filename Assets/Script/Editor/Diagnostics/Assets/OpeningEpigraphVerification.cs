#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class OpeningEpigraphVerification
{
    const string Pending="Barricade.Epigraph.Pending";
    static int frame;static double changed;static bool stepping;
    public const string Content="<size=72>我们走进的，是一座浸透曙光的坟墓。</size>\n\n<size=48>« … et nous entrons dans une tombe toute pénétrée d'aurore. »</size>\n\n<size=36>——维克多·雨果《悲惨世界》\nVictor Hugo · Les Misérables\n第五部《冉阿让》·第一卷·第五章</size>";
    static string Marker(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/"+name+".request"));
    static OpeningEpigraphVerification()
    {
        EditorApplication.update+=Poll;
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Pending+".active",false)){PlayerSettings.runInBackground=SessionState.GetBool(Pending+".background",false);EditorApplication.isPaused=false;SessionState.EraseBool(Pending+".active");}};
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Marker("OpeningEpigraphSetup")))
        {
            string output=File.ReadAllText(Marker("OpeningEpigraphSetup")).Trim();File.Delete(Marker("OpeningEpigraphSetup"));
            try
            {
                var asset=Resources.Load<CampaignCinematics>("Campaign/Cinematics");
                Undo.RecordObject(asset,"设置中法双语卷首语");asset.openingSubtitle=Content;asset.openingSubtitleSeconds=12;asset.openingAllowSkip=true;
                asset.Validate();EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
                File.WriteAllText(Path.Combine(output,"卷首语配置.txt"),"success\n12 seconds\n"+Content);
            }
            catch(Exception e){File.WriteAllText(Path.Combine(output,"卷首语配置.txt"),e.ToString());Debug.LogException(e);}return;
        }
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Marker("OpeningEpigraphPlay")))
        {
            string output=File.ReadAllText(Marker("OpeningEpigraphPlay")).Trim();File.Delete(Marker("OpeningEpigraphPlay"));
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){File.WriteAllText(Path.Combine(output,"卷首语Play.json"),"{\"success\":false,\"error\":\"当前场景有未保存编辑\"}");return;}
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Title")UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            SessionState.SetString(Pending,output);SessionState.SetBool(Pending+".active",true);SessionState.SetBool(Pending+".background",PlayerSettings.runInBackground);
            PlayerSettings.runInBackground=true;frame=-1;changed=EditorApplication.timeSinceStartup;stepping=false;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Pending+".active",false))return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(frame!=Time.frameCount){frame=Time.frameCount;changed=EditorApplication.timeSinceStartup;}
        if(stepping||EditorApplication.timeSinceStartup-changed>1){stepping=true;EditorApplication.isPaused=true;EditorApplication.Step();}
        string pending=SessionState.GetString(Pending,"");
        if(StoryDirector.Current!=null&&!string.IsNullOrEmpty(pending)){SessionState.EraseString(Pending);StoryDirector.Current.gameObject.AddComponent<OpeningEpigraphProbe>().Begin(pending);}
    }
}
public sealed class OpeningEpigraphProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success;public int startFrame,endFrame;public string error;}
    string output;Report report;
    void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public void Begin(string directory){output=directory;report=new Report{startFrame=Time.frameCount};StartCoroutine(Run());}
    IEnumerator Run()
    {
        yield return null;
        var story=StoryDirector.Current;var film=story.GetComponent<CampaignCinematicView>();
        var flow=StartCoroutine(story.DebugChapter(1));
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
        try
        {
            var asset=Resources.Load<CampaignCinematics>("Campaign/Cinematics");
            Check(asset.openingSubtitle==OpeningEpigraphVerification.Content&&asset.openingSubtitleSeconds==12&&asset.openingAllowSkip,"字幕、阅读时间或跳过设置不符");
            Check(story.Phase==CampaignPhase.Transition&&film.IsBlackCardVisible,"第一章未进入卷首语黑屏");
            var label=FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Single(t=>t.name=="OpeningCenteredText");
            Check(label.text==asset.openingSubtitle&&label.supportRichText&&label.font==GameFont.Silver,"双语字体或排版未应用");
            string plain=System.Text.RegularExpressions.Regex.Replace(label.text,"<[^>]+>","");
            foreach(char c in plain.Where(c=>!char.IsWhiteSpace(c)))Check(label.font.HasCharacter(c),"字体缺字："+c);
            Check(label.preferredHeight<=label.rectTransform.rect.height&&label.preferredWidth<=label.rectTransform.rect.width,"双语或出处超出黑屏文字区域");
            Check(label.transform.parent.GetComponent<UnityEngine.UI.Image>().color==Color.black,"卷首语背景不是纯黑");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"双语卷首语_游戏内.png"));
        }
        catch(Exception e){report.error=e.ToString();}
        yield return null;yield return null;
        if(string.IsNullOrEmpty(report.error))
        {
            // Skip the real opening coroutine, then check the following chapter announcement.
            film.DebugSkip();yield return null;yield return null;yield return null;
            try
            {
                var label=FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Single(t=>t.name=="OpeningCenteredText");
                Check(film.IsBlackCardVisible&&!label.supportRichText&&label.font==GameFont.Silver&&label.text.Contains("第一章"),"卷首语之后的第一章报幕未正常恢复");
                report.success=true;
            }
            catch(Exception e){report.error=e.ToString();}
        }
        StopCoroutine(flow);film.Hide();yield return story.DebugPhase("title");
        report.endFrame=Time.frameCount;File.WriteAllText(Path.Combine(output,"卷首语Play.json"),JsonUtility.ToJson(report,true));EditorApplication.isPlaying=false;
    }
}
#endif
