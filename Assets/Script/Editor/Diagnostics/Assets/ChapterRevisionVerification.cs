#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChapterRevisionVerification
{
    [Serializable] sealed class Report{public bool success,projectSaved;public int checks,totalBeats,filmBeats;public string error,utc;}
    static Report report;
    static void Require(bool pass,string message){report.checks++;if(!pass)throw new InvalidOperationException(message);}
    [MenuItem("街垒/战役/检查四图第一章与跨章生命")]
    public static void Run()
    {
        report=new Report{utc=DateTime.UtcNow.ToString("O")};
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var table=Resources.Load<ChapterTable>("Campaign/ChapterTable");Require(table!=null,"章节表缺失。");table.Validate();
            report.totalBeats=table.chapters.Sum(c=>c.TotalBeats);report.filmBeats=table.firstChapterFrames.Sum(f=>f.beats);
            Require((!table.enforceBeatBudget||report.totalBeats==table.targetBeatBudget)&&report.filmBeats==table.chapters[0].cinematicBeats,"拍数预算不符。");
            foreach(var c in table.chapters)
            {
                Require(c.lines.Length==0,"仍有章中叙事文字。");
                Require(CampaignRevisionRules.EnterChapterHealth(237,600,false,0)==237,"跨章血量没有保留。");
                Require(CampaignRevisionRules.EnterChapterHealth(0,600,true,237)==237,"重试未还原章首血量。");
            }
            var root=Resources.Load<GameObject>("Campaign/CampaignRoot").GetComponent<StoryDirector>();
            Require(root.titleMusic==null,"标题页仍绑定音乐。");
            Require(root.chapterMusic!=null&&root.chapterMusic.name=="Do You Hear The People Sing","游戏录音引用错误。");
            foreach(var frame in table.firstChapterFrames)
            {
                Require(frame.image!=null&&frame.image.texture.width==1280&&frame.image.texture.height==720,"图片未按原尺寸导入。");
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame.image));
                Require(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"图片像素导入设置错误。");
            }
            GameObject screen=null;
            try
            {
                screen=UnityEngine.Object.Instantiate(root.screenPrefab);screen.hideFlags=HideFlags.HideAndDontSave;
                var view=screen.GetComponent<StartScreenView>();var film=screen.AddComponent<ChapterSequenceView>();film.Initialize(view);
                for(int i=0;i<4;i++)
                {
                    film.ShowFrame(i,table.firstChapterFrames[i],table.firstChapterCrossfadeSeconds);film.Advance(1);
                    Require(film.FrameIndex==i&&film.Current.sprite==table.firstChapterFrames[i].image&&film.Current.color.a==1,"画面顺序或淡入错误。");
                    Require(film.Current.preserveAspect&&!film.Current.raycastTarget,"画面拉伸或阻挡交互。");
                    Require(!film.Subtitle.gameObject.activeSelf,"空字幕接口不应出现文字。");
                }
                var testFrame=new ChapterFrame{image=table.firstChapterFrames[0].image,beats=8,subtitle="接口核验"};
                film.ShowFrame(0,testFrame,.6f);Require(film.Subtitle.text=="接口核验"&&film.Subtitle.gameObject.activeSelf,"字幕接口不可用。");
                Require(screen.transform.Find("ChapterOneSequence").GetSiblingIndex()<view.transform.Find("PausePage").GetSiblingIndex(),"演出遮挡了暂停菜单。");
                film.Hide();
            }
            finally{if(screen!=null)UnityEngine.Object.DestroyImmediate(screen);}
            CampaignVerification.Check();WaveVerification.Check();PauseSaveVerification.Check();CannonPrefabVerification.Check();
            AssetDatabase.SaveAssets();
            if(Application.isBatchMode)EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
            Require(EditorSceneManager.SaveOpenScenes(),"场景未能保存。");
            report.projectSaved=true;report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        Directory.CreateDirectory("Library");File.WriteAllText("Library/ChapterRevisionVerification.json",JsonUtility.ToJson(report,true));
        if(!report.success)throw new InvalidOperationException(report.error);
        Debug.Log("[ChapterRevisionVerification] 四图、章节预算、血量规则、界面与引用通过；项目已保存。实际设备音效与完整十章试玩需单独验收。");
    }
}
#endif
