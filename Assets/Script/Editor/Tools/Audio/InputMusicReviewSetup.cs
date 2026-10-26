#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InputMusicReviewSetup
{
    [Serializable] sealed class Report {public bool success,scenePreserved;public int checks;public string error;public float fadeSeconds;}
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/InputMusicReviewSetup.request"));
    static InputMusicReviewSetup(){EditorApplication.update+=Poll;}
    static void Check(bool ok,Report report,string message){report.checks++;if(!ok)throw new Exception(message);}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);var r=new Report();var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;GameObject contents=null;
        try
        {
            const string path="Assets/Resources/Campaign/CampaignRoot.prefab";contents=PrefabUtility.LoadPrefabContents(path);var story=contents.GetComponent<StoryDirector>();story.gameplayMusicFadeSeconds=2;PrefabUtility.SaveAsPrefabAsset(contents,path);PrefabUtility.UnloadPrefabContents(contents);contents=null;
            r.fadeSeconds=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<StoryDirector>().gameplayMusicFadeSeconds;Check(r.fadeSeconds==2,r,"Serialized campaign fade is two seconds");
            var judge=new ConstructionJudgement();judge.Reset(1,-1,.12,10);judge.Reset(0,2,.12,11);
            Check(judge.Press(1,11.1,true)==ConstructionResult.Grace&&!judge.Resolved,r,"Grace reports feedback without consuming current prompt");
            Check(judge.Press(0,11.11,true)==ConstructionResult.None&&judge.Press(2,11.2,true)==ConstructionResult.Perfect,r,"Current chord still completes after grace feedback");
            judge.Reset(1,-1,.12,12);judge.Reset(0,-1,.12,13);Check(judge.Press(1,13.181,true)==ConstructionResult.Wrong,r,"Old key after 180ms is no longer protected");
            judge.Reset(1,-1,.12,14);judge.Reset(1,-1,.12,15);Check(judge.Press(1,15.1,false)==ConstructionResult.Ordinary,r,"Current-key match takes priority over previous-key grace");
            var envelope=new MusicVolumeEnvelope();envelope.Snap(.65f);envelope.Retarget(.16f,2);Check(envelope.Value==.65f,r,"Fade retarget never snaps to quiet gain");
            envelope.Advance(1);Check(Math.Abs(envelope.Value-.405f)<.001,r,"One-second gain is halfway through the two-second fade");envelope.Retarget(.16f,2);envelope.Advance(1);Check(envelope.Value==.16f,r,"Repeated target updates do not restart the fade");
            r.scenePreserved=SceneManager.GetActiveScene()==scene&&scene.isDirty==dirty;Check(r.scenePreserved,r,"Scene edits retained");r.success=true;
        }
        catch(Exception e){r.error=e.ToString();Debug.LogException(e);}
        finally{if(contents!=null)PrefabUtility.UnloadPrefabContents(contents);Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"原生配置检查.json"),JsonUtility.ToJson(r,true));}
    }
}
#endif
