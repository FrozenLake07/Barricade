#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectVerification
{
    [Serializable] sealed class Report{public bool success;public string utc,error;public bool campaign,waves,animations,cannon,pauseSave;}
    [MenuItem("街垒/战役/运行全部工程核验")]
    public static void Run()
    {
        var report=new Report{utc=DateTime.UtcNow.ToString("O")};
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CampaignVerification.Check();report.campaign=true;
            PauseSaveVerification.Check();report.pauseSave=true;
            WaveVerification.Check();
            var audit=WaveImportValidation.ValidateAll();if(!audit.success)throw new InvalidOperationException("WaveValidation未通过，错误数"+audit.errors);
            report.waves=true;
            if(!EnemyAnimatorSetup.Build(false))throw new InvalidOperationException("敌人动画工具未通过。");report.animations=true;
            CannonPrefabVerification.Check();report.cannon=true;
            report.success=true;
        }
        catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
        Directory.CreateDirectory("Library");File.WriteAllText("Library/ProjectVerification.json",JsonUtility.ToJson(report,true));
        if(!report.success)throw new InvalidOperationException(report.error);
        Debug.Log("[ProjectVerification] 原生数据、引用和动画核验通过；仍需Play模式实际流程核验。");
    }
    public static void BuildWindows()
    {
        Run();
        string[] scenes={"Assets/Scenes/Title.unity","Assets/Scenes/1.unity","Assets/Scenes/2.unity"};
        Directory.CreateDirectory("Builds/Windows");
        var result=BuildPipeline.BuildPlayer(scenes,"Builds/Windows/Barricade.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Windows构建失败："+result.summary.result);
    }
}
#endif
