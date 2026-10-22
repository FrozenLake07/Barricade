#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ParisUIVerification
{
    [MenuItem("街垒/界面/检查巴黎像素界面与护送扩容")]
    public static void Run()
    {
        int checks=0;Action<bool,string> require=(pass,message)=>{checks++;if(!pass)throw new InvalidOperationException(message);};
        var original=CampaignStateCodec.Capture(typeof(RunState),o=>"");GameObject screen=null;
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var style=Resources.Load<ParisUIStyle>("UI/Paris1832/Style");require(style!=null,"缺少界面样式。");
            require(style.heartTint.a==1&&style.heartEmptyTint.a==1,"心形两层着色应保持不透明度一致。");
            require(style.titleMark!=null&&style.titleBackground!=null&&style.heart!=null,"缺少像素标题、心脏或开始页背景。");
            foreach(var sprite in new[]{style.heart,style.heartEmpty,style.titleMark,style.leadBall})
            {
                require(sprite!=null,"缺少材质或铅弹图。");var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                require(importer.filterMode==FilterMode.Point&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"界面素材导入设置错误。");
            }
            screen=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Campaign/StartScreen.prefab"));
            var view=screen.GetComponent<StartScreenView>();ParisUITheme.Apply(view);ParisUITheme.Apply(view);
            foreach(var name in new[]{"StoryPaper","ChapterLeather","CaptionLeather"})require(screen.transform.Find(name)!=null,"缺少界面素材层："+name);
            require(screen.transform.Find("StoryPaper").GetSiblingIndex()<view.main.transform.GetSiblingIndex(),"面板遮挡了功能文字。");
            var menu=CampaignMenuView.Ensure(view);
            foreach(var button in new[]{menu.newGame,menu.continueGame,menu.titleQuit,menu.resume,menu.save,menu.load,menu.pauseQuit})
            {
                require(button.targetGraphic!=null&&button.targetGraphic.raycastTarget,"按钮无法接收点击。");
                require(button.GetComponent<ParisMenuAccent>()!=null,"缺少鼠标/键盘焦点提示。");
                require(button.GetComponents<ParisMenuAccent>().Length==1,"重复应用主题叠加焦点组件。");
                require(button.transform.Find("FocusMark")!=null&&button.transform.Find("MenuRule")!=null,"缺少焦点装饰。");
            }
            require(screen.transform.Find("StoryPaper").GetComponent<ParisBackdropGraphic>()!=null,"缺少宽幅暗场。");
            foreach(var image in screen.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if(image.name.EndsWith("Paper")||image.name.EndsWith("Leather"))require(!image.raycastTarget,"装饰层拦截点击："+image.name);
            require(screen.GetComponent<CampaignGameplayHud>()!=null,"缺少分层局内标题界面。");
            require(!view.hud.enabled,"内部HUD文字不应重复绘制。");
            require(Resources.Load<GameObject>("UI/ConstructionCue")!=null,"缺少可编辑建造提示预制体。");
            var ammo=AmmoHudView.Attach(screen.transform);require(ammo!=null,"缺少弹药界面。");
            require(ammo.heartPulse!=null&&ammo.heartFill!=null&&ammo.heartEmpty!=null,"缺少同组心脏图层。");
            require(ammo.heartPulse.parent==ammo.transform&&ammo.counter.transform.parent==ammo.transform,"心脏与弹药未处于同一布局组。");
            require(ammo.GetComponentsInChildren<Heartbeat>(true).Length==0,"显示层不应创建第二套心跳计时/音频。");
            require(CampaignHudPresentation.HealthRatio(0,600)==0&&CampaignHudPresentation.HealthRatio(600,600)==1,"心脏耐久映射不符。");
            for(int capacity=3;capacity<=4;capacity++)
            {
                ammo.illustration.SetState(AmmoHudState.Build(capacity,capacity,false,false,0,1,.8f,false));
                for(int slot=1;slot<=6;slot++)require(ammo.illustration.transform.Find("LeadBall"+slot).gameObject.activeSelf==(slot<=capacity),"弹药扩容图标数量错误。");
            }
            RunState.NewCampaign();RunState.Chapter=4;RunState.EnterChapter(false);
            RunState.RegisterEscortResult(true);RunState.RegisterEscortResult(false);RunState.RegisterEscortResult(true);require(RunState.AmmoBonus==0,"失败一段仍获得完整救援奖励。");
            RunState.NewCampaign();RunState.Chapter=4;RunState.EnterChapter(false);
            require(!RunState.RegisterEscortResult(true)&&RunState.AmmoBonus==0,"第一段提前发奖。");
            require(!RunState.RegisterEscortResult(true)&&RunState.AmmoBonus==0,"第二段提前发奖。");
            require(RunState.RegisterEscortResult(true)&&RunState.AmmoBonus==1,"三段全部完成未获得一次扩容。");
            RunState.Chapter=5;RunState.EnterChapter(false);RunState.ResetChapter();require(RunState.AmmoBonus==1,"扩容未跨章保留。");
            var snapshot=CampaignStateCodec.Capture(typeof(RunState),o=>"");RunState.NewCampaign();CampaignStateCodec.Restore(typeof(RunState),snapshot,k=>null);RunState.EnterChapter(true);require(RunState.AmmoBonus==1,"读档重试丢失扩容。");
            Debug.Log("[ParisUI] 检查通过："+checks);
            File.WriteAllText("Library/ParisUIVerification.json","{\"success\":true,\"checks\":"+checks+"}");
        }
        finally{if(screen!=null)UnityEngine.Object.DestroyImmediate(screen);CampaignStateCodec.Restore(typeof(RunState),original,k=>null);}
    }
    [MenuItem("街垒/界面/将巴黎样式保存到开始界面预制体")]
    public static void Bake()
    {
        const string path="Assets/UI/Campaign/StartScreen.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{ParisUITheme.Apply(root.GetComponent<StartScreenView>());PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
