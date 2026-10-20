#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PauseSaveVerification
{
    [Serializable] sealed class Report{public bool success;public int checks;public string error;}
    [MenuItem("街垒/战役/检查暂停与单存档配置")]
    public static void Check()
    {
        var report=new Report();
        Action<bool,string> require=(condition,message)=>{report.checks++;if(!condition)throw new InvalidOperationException(message);};
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Campaign/StartScreen.prefab");
            require(prefab!=null,"开始界面预制体缺失。");
            var view=prefab.GetComponent<CampaignMenuView>();
            require(view!=null&&prefab.GetComponent<UnityEngine.UI.GraphicRaycaster>()!=null,"菜单组件或 GraphicRaycaster 缺失。");
            require(view.titleMenu!=null&&view.pausePage!=null&&view.countInPage!=null,"菜单页面引用缺失。");
            foreach(var button in new[]{view.newGame,view.continueGame,view.titleQuit,view.resume,view.pauseQuit,view.save,view.load})
                require(button!=null&&button.targetGraphic!=null,"按钮或点击区域缺失。");
            foreach(var text in prefab.GetComponentsInChildren<UnityEngine.UI.Text>(true))require(text.font==GameFont.Silver,"菜单文字未使用 Silver："+text.name);
            var clock=new CampaignBeatClock();clock.Reset(.85f,.5f);clock.Advance(.9f);
            var count=new ResumeCountIn();count.Reset(clock.Period);
            while(!count.Finished)count.Advance(.02);
            require(clock.Opened==1&&clock.Completed==0&&Mathf.Approximately(clock.Elapsed,.9f),"恢复倒计时消耗了实际拍数。");
            string path="Library/PauseSaveVerificationSlot.sav";
            try
            {
                SingleSaveSlot.Write(path,"{\"check\":1}");SingleSaveSlot.Write(path,"{\"check\":2}");
                require(SingleSaveSlot.TryRead(path,out string payload,out _)&&payload=="{\"check\":2}","唯一存档覆盖或校验失败。");
                require(!File.Exists(path+".tmp"),"临时写入未清理。");
            }
            finally{if(File.Exists(path))File.Delete(path);}
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        Directory.CreateDirectory("Library");File.WriteAllText("Library/PauseSaveVerification.json",JsonUtility.ToJson(report,true));
        if(!report.success)throw new InvalidOperationException(report.error);
        Debug.Log("[PauseSaveVerification] 暂停菜单、字体、独立倒计时与单存档文件核验通过。仍需Play模式检验完整读档。");
    }
}
#endif
