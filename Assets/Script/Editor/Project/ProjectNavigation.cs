#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Discoverable shortcuts; opening a scene follows Unity's normal unsaved-change prompt.
public static class ProjectNavigation
{
    static void Open(string scene)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.Log("请先退出 Play，再打开编辑场景。");return;}
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(CampaignSceneNames.AssetPath(scene));
    }
    static void Select(string path)
    {
        var asset=AssetDatabase.LoadAssetAtPath<Object>(path);
        Selection.activeObject=asset;if(asset!=null)EditorGUIUtility.PingObject(asset);
    }
    [MenuItem("街垒/工程导航/场景/主菜单")] static void Title()=>Open(CampaignSceneNames.Title);
    [MenuItem("街垒/工程导航/场景/建造")] static void Construction()=>Open(CampaignSceneNames.Construction);
    [MenuItem("街垒/工程导航/场景/防守")] static void Defense()=>Open(CampaignSceneNames.Defense);
    [MenuItem("街垒/工程导航/场景/酒馆 Timeline")] static void Tavern()=>Open(CampaignSceneNames.Tavern);
    [MenuItem("街垒/工程导航/配置/战役主控")] static void Root()=>Select("Assets/Resources/Campaign/CampaignRoot.prefab");
    [MenuItem("街垒/工程导航/配置/章节与难度")] static void Chapters()=>Select("Assets/Resources/Campaign/ChapterTable.asset");
    [MenuItem("街垒/工程导航/配置/剧情分镜与结局")] static void Cinematics()=>Select("Assets/Resources/Campaign/Cinematics.asset");
    [MenuItem("街垒/工程导航/配置/音乐歌词字幕")] static void Lyrics()=>Select("Assets/Resources/Campaign/MusicLyrics.json");
    [MenuItem("街垒/工程导航/配置/游戏字体")] static void Font()=>Select("Assets/Resources/Campaign/GameFont.asset");
    [MenuItem("街垒/工程导航/配置/酒馆对白")] static void Dialogue()=>Select("Assets/Cinematics/TavernUI/Data/TavernDialogue.asset");
    [MenuItem("街垒/工程导航/配置/特殊名字回复")] static void Names()=>Select("Assets/Cinematics/TavernUI/Data/NameResponses.asset");
    [MenuItem("街垒/工程导航/打开工程说明")] static void Guide()=>EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(Application.dataPath,"../Documentation/README.md")));
}
#endif
