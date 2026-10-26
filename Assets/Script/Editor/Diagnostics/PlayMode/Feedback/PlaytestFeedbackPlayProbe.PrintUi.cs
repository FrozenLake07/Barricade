#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator PrintUiTest()
    {
        RememberPreferences();yield return Frames(4);DebugConsole.Current?.SetOpen(false);
        yield return story.DebugChapter(3,true);CampaignDebug.Value.freeze=true;ClearThreats();yield return Frames(2);
        var menu=FindFirstObjectByType<CampaignMenuView>();CampaignSession.Current.Pause();yield return Frames(3);
        Check(menu.pausePage.activeSelf&&menu.pausePage.transform.Find("PausePaper/PrintTreatment")!=null,"Pause uses custom print sheet without replacing existing menu");
        Check(menu.pauseQuit.GetComponent<RectTransform>().anchorMax.y<menu.pausePage.transform.Find("Settings").GetComponent<RectTransform>().anchorMin.y,"Pause settings/quit order is retained");
        var pauseButtons=new[]{menu.resume,menu.save,menu.load,menu.pausePage.transform.Find("Settings").GetComponent<UnityEngine.UI.Button>(),menu.pauseQuit};
        foreach(var button in pauseButtons)
        {
            var rect=button.GetComponent<RectTransform>();
            Check(Mathf.Abs(rect.anchorMin.x-.38f)<.0001f&&Mathf.Abs(rect.anchorMax.x-.62f)<.0001f&&rect.offsetMin==Vector2.zero&&rect.offsetMax==Vector2.zero,"Pause button shares common horizontal bounds: "+button.name);
        }
        Check(menu.pausePage.transform.Find("PausePaper").GetComponent<UnityEngine.UI.Image>().color.a==0,"Pause paper base no longer paints a dark outer rectangle");
        CaptureUi(1920,1080,"暂停_街垒布告");CaptureUi(960,540,"暂停_960");
        menu.pausePage.transform.Find("Settings").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return Frames(3);
        var settings=GameSettingsView.Current;var page=Get<GameObject>(settings,"page");
        Check(GameSettingsView.IsOpen&&page.transform.Find("PrintTreatment")!=null,"Existing settings button opens newly styled settings page");
        Check(page.GetComponentsInChildren<ParisPrintGraphic>(true).All(g=>!g.raycastTarget),"All custom decoration ignores pointer input");
        var graphic=page.transform.Find("PrintTreatment").GetComponent<ParisPrintGraphic>();
        var mesh=graphic.canvasRenderer.GetMesh();
        Check(mesh!=null&&mesh.vertexCount>100,"Custom print background has a generated renderable mesh");
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"PrintMesh.txt"),"vertices="+mesh.vertexCount+" bounds="+mesh.bounds+" rect="+graphic.rectTransform.rect+" color="+graphic.color+" rendercolor="+graphic.canvasRenderer.GetColor()+" inheritedAlpha="+graphic.canvasRenderer.GetInheritedAlpha()+" culled="+graphic.canvasRenderer.cull+" depth="+graphic.depth+" texture="+graphic.mainTexture+" verticesStart="+(mesh.vertexCount>0?mesh.vertices[0].ToString():"none")+" colorStart="+(mesh.colors32.Length>0?mesh.colors32[0].ToString():"none"));
        CaptureUi(1920,1080,"设置_街垒布告");CaptureUi(960,540,"设置_960");
        var old=GameOptions.Difficulty;page.transform.Find("ControlsPage/DifficultyNext").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(GameOptions.Difficulty!=old,"Difficulty buttons remain functional through custom decorations");
        settings.Close();Check(CampaignSession.Suspended,"Closing styled settings preserves pause");
        CampaignSession.Current.Resume();yield return Frames(2);CaptureUi(1280,720,"恢复倒数_街垒布告");yield return Until(()=>!CampaignSession.Suspended);
        Set(story,"failed",false);typeof(StoryDirector).GetMethod("ShowResults",Private).Invoke(story,null);yield return Frames(3);
        Check(story.Phase==CampaignPhase.Results&&Get<CampaignUI>(story,"ui").View.transform.Find("StoryPaper/PrintTreatment").gameObject.activeInHierarchy,"Results use custom print treatment");
        CaptureUi(1920,1080,"结算_街垒布告");
        StartCoroutine((IEnumerator)typeof(StoryDirector).GetMethod("Calibrate",Private).Invoke(story,new object[]{false}));
        yield return Until(()=>story.Phase==CampaignPhase.Calibration);yield return Frames(4);
        Check(Get<CampaignUI>(story,"ui").Main.rectTransform.anchorMin.y>=.64f,"Calibration heading is laid out above the animated heart");
        CaptureUi(1920,1080,"校音_街垒布告");CaptureUi(960,540,"校音_960");
        OpeningCalibration.DebugSkip=true;yield return Until(()=>story.Phase==CampaignPhase.Title);yield return Frames(2);
        var screen=Get<CampaignUI>(story,"ui").View;
        Check(!screen.transform.Find("StoryPaper/PrintTreatment").gameObject.activeInHierarchy,"Print story sheet is hidden on return to title");
        Check(screen.titlePage.transform.Find("GameTitleMark").GetComponent<UnityEngine.UI.Image>().material==UnityEngine.UI.Graphic.defaultGraphicMaterial,"Original non-tricolor title is retained");
        StartCoroutine(Film.PlayBlackCard("第 五 章",.5f,false,44));yield return Frames(2);
        Check(Film.IsBlackCardVisible&&Get<GameObject>(Film,"blackCardPage").GetComponentsInChildren<ParisPrintGraphic>(true).Length==0,"Intentional narrative black cards remain plain");
        Film.DebugSkip();yield return Frames(3);
        report.coverage="Actual advancing Play: pause/settings interaction, difficulty change and pause restoration, custom sheet visibility, original title, plain narrative cards and calibration layout. Screenshots at 1920x1080 and 960x540. Save slots untouched and preferences restored.";
    }
}
#endif
