#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CampaignCinematicTools
{
    [MenuItem("街垒/战役/编辑过场图片、文案与命名")]
    public static void Open()
    {
        var asset=AssetDatabase.LoadAssetAtPath<CampaignCinematics>("Assets/Resources/Campaign/Cinematics.asset");
        Selection.activeObject=asset;EditorGUIUtility.PingObject(asset);
    }
    [MenuItem("街垒/战役/检查过场图片与主角命名")]
    public static void Verify()
    {
        var asset=AssetDatabase.LoadAssetAtPath<CampaignCinematics>("Assets/Resources/Campaign/Cinematics.asset");
        if(asset==null)throw new InvalidOperationException("缺少过场配置。");asset.Validate();
        var images=new HashSet<Sprite>();
        foreach(var route in asset.routes)foreach(var shot in route.shots)
        {
            images.Add(shot.image);var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(shot.image)) as TextureImporter;
            if(importer==null||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed)
                throw new InvalidOperationException("过场图片导入设置错误："+shot.image.name);
        }
        if(images.Count!=34)throw new InvalidOperationException("应配置34张独立过场图片，实际："+images.Count);
        var release=asset.Find(7,"chapter-exit",JavertDecision.Released);
        var execute=asset.Find(7,"chapter-exit",JavertDecision.Executed);
        if(release==null||execute==null||release==execute||asset.Find(7,"chapter-exit",JavertDecision.Unchosen)!=null)
            throw new InvalidOperationException("第七章分支未正确隔离。");
        var opening=asset.Find(2,"chapter-entry",JavertDecision.Unchosen);
        if(opening==null||opening.nameAfterShot!=1)throw new InvalidOperationException("主角命名节点缺失。");
        var state=new CinematicPlaybackState(new double[]{5,5,6},1);state.Skip();
        if(!state.AwaitingName||state.Finished)throw new InvalidOperationException("跳过动画越过了命名节点。");
        state.ConfirmName();state.Advance(6);
        if(!state.Finished)throw new InvalidOperationException("命名后过场未完成。");
        if(!ProtagonistNameRules.TryNormalize("  街垒  ",out var name)||name!="街垒"||ProtagonistNameRules.TryNormalize("甲\n乙",out _))
            throw new InvalidOperationException("姓名校验失败。");
        AssetDatabase.SaveAssets();Debug.Log("[过场检查] 34张独立图片、分支隔离、命名跳过与导入设置通过。");
    }
}
#endif
