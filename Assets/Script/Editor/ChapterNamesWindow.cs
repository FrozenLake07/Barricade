#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChapterNamesWindow : EditorWindow
{
    const string AssetPath="Assets/Resources/Campaign/ChapterTable.asset";
    ChapterTable table;
    string[] draft;
    Label status;

    [MenuItem("街垒/战役/编辑章节名字")]
    public static void Open()
    {
        var window=GetWindow<ChapterNamesWindow>("章节名字");
        window.minSize=new Vector2(400,460);
        window.Show();
    }
    void OnEnable(){Undo.undoRedoPerformed+=ReloadAfterUndo;}
    void OnDisable(){Undo.undoRedoPerformed-=ReloadAfterUndo;}
    void ReloadAfterUndo(){CreateGUI();}
    public void CreateGUI()
    {
        var root=rootVisualElement;root.Clear();
        root.style.paddingLeft=root.style.paddingRight=12;
        root.style.paddingTop=root.style.paddingBottom=12;
        table=AssetDatabase.LoadAssetAtPath<ChapterTable>(AssetPath);
        if(table==null||table.chapters==null||table.chapters.Length!=10||Array.Exists(table.chapters,c=>c==null))
        {
            root.Add(new HelpBox("未找到完整的十章配置，请检查 "+AssetPath,HelpBoxMessageType.Error));return;
        }
        root.Add(new HelpBox("修改后点击“保存章名”。支持 Ctrl+Z 撤销；名字统一用于章节标题和游戏 HUD。",HelpBoxMessageType.Info));
        var scroll=new ScrollView();scroll.style.flexGrow=1;root.Add(scroll);
        draft=new string[table.chapters.Length];
        for(int i=0;i<draft.Length;i++)
        {
            int index=i;var chapter=table.chapters[i];draft[i]=chapter.title??"";
            var field=new TextField("第"+chapter.number+"章"){name="ChapterTitle"+chapter.number,value=draft[i]};
            field.style.marginTop=6;
            field.RegisterValueChangedCallback(evt=>{draft[index]=evt.newValue;status.text="有未保存的章名修改";});
            scroll.Add(field);
        }
        status=new Label("章名来源："+AssetPath);status.style.whiteSpace=WhiteSpace.Normal;status.style.marginTop=10;root.Add(status);
        var buttons=new VisualElement();buttons.style.flexDirection=FlexDirection.Row;buttons.style.marginTop=8;root.Add(buttons);
        var save=new Button(Save){text="保存章名"};save.style.flexGrow=1;buttons.Add(save);
        buttons.Add(new Button(()=>{Selection.activeObject=table;EditorGUIUtility.PingObject(table);}){text="定位配置资产"});
    }
    void Save()
    {
        if(table==null||draft==null||table.chapters==null||table.chapters.Length!=draft.Length)
        {status.text="配置已变化，请重新打开窗口。";return;}
        for(int i=0;i<draft.Length;i++)
        {
            if(string.IsNullOrWhiteSpace(draft[i])){status.text="第"+(i+1)+"章名字不能为空。";return;}
            if(table.chapters[i]==null){status.text="第"+(i+1)+"章配置缺失。";return;}
        }
        Undo.RecordObject(table,"修改章节名字");
        for(int i=0;i<draft.Length;i++)table.chapters[i].title=draft[i].Trim();
        EditorUtility.SetDirty(table);AssetDatabase.SaveAssetIfDirty(table);
        status.text="十章名字已保存。Ctrl+Z 可撤销修改。";
    }
}
#endif
