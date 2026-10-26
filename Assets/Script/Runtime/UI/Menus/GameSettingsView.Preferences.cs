using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class GameSettingsView
{
    GameObject controlsPage,audioPage;
    UnityEngine.UI.Button controlsTab,audioTab;
    UnityEngine.UI.Slider masterSlider,musicSlider,effectsSlider,heartbeatSlider,subtitleSlider;
    UnityEngine.UI.Text masterValue,musicValue,effectsValue,heartbeatValue,subtitleValue;
    bool audioSelected;
    void BuildPreferenceTabs()
    {
        controlsPage=Node(page.transform,"ControlsPage",0,0,1,1);
        controlsTab=Button(page.transform,"ControlsTab","操作与难度",.08f,.805f,.49f,.875f,()=>SelectPreferenceTab(false));
        audioTab=Button(page.transform,"AudioTab","声音与字幕",.51f,.805f,.92f,.875f,()=>SelectPreferenceTab(true));
    }
    void SelectPreferenceTab(bool audio)
    {
        audioSelected=audio;listening=null;
        controlsPage.SetActive(!audio);if(audioPage!=null)audioPage.SetActive(audio);
        controlsTab.image.color=audio?new Color(.09f,.10f,.11f):new Color(.24f,.17f,.14f);
        audioTab.image.color=audio?new Color(.24f,.17f,.14f):new Color(.09f,.10f,.11f);
        if(status!=null)status.text="修改即时生效并保存";
        EventSystem.current?.SetSelectedGameObject(null);
    }
    void BuildAudioPreferences()
    {
        audioPage=Node(page.transform,"AudioPage",0,0,1,1);
        masterSlider=PreferenceSlider(audioPage.transform,"MasterVolume","总音量",.73f,0,100,v=>{GamePresentationOptions.MasterVolume=v/100;RefreshAudioPreferences();},out masterValue);
        musicSlider=PreferenceSlider(audioPage.transform,"MusicVolume","音乐",.64f,0,100,v=>{GamePresentationOptions.MusicVolume=v/100;RefreshAudioPreferences();},out musicValue);
        effectsSlider=PreferenceSlider(audioPage.transform,"EffectsVolume","音效",.55f,0,100,v=>{GamePresentationOptions.EffectsVolume=v/100;RefreshAudioPreferences();},out effectsValue);
        heartbeatSlider=PreferenceSlider(audioPage.transform,"HeartbeatVolume","心跳",.46f,0,100,v=>{GamePresentationOptions.HeartbeatVolume=v/100;RefreshAudioPreferences();},out heartbeatValue);
        subtitleSlider=PreferenceSlider(audioPage.transform,"SubtitleSize","字幕大小",.36f,100,150,v=>{GamePresentationOptions.SubtitleScale=v/100;RefreshAudioPreferences();},out subtitleValue);
        var preview=Label(audioPage.transform,"SubtitlePreview","心跳静止时，按下按键。",.08f,.265f,.92f,.315f,32);preview.alignment=TextAnchor.MiddleCenter;SubtitleSizePreference.Attach(preview);
        Button(audioPage.transform,"Recalibrate","重新校准",.08f,.17f,.43f,.235f,StartSettingsCalibration);
        Label(audioPage.transform,"CalibrationHint","重新对齐心跳声音与画面",.47f,.17f,.92f,.235f,28);
        Button(audioPage.transform,"ResetPresentation","恢复本页默认",.08f,.025f,.43f,.09f,()=>{GamePresentationOptions.MasterVolume=.8f;GamePresentationOptions.MusicVolume=GamePresentationOptions.EffectsVolume=GamePresentationOptions.HeartbeatVolume=1;GamePresentationOptions.SubtitleScale=1;RefreshAudioPreferences();status.text="已恢复声音与字幕默认值";});
        BuildSettingsCalibration();
    }
    UnityEngine.UI.Slider PreferenceSlider(Transform parent,string name,string caption,float y,float min,float max,UnityEngine.Events.UnityAction<float> changed,out UnityEngine.UI.Text value)
    {
        Label(parent,name+"Caption",caption,.08f,y-.035f,.28f,y+.035f,38);
        value=Label(parent,name+"Value","",.82f,y-.035f,.92f,y+.035f,32);value.alignment=TextAnchor.MiddleCenter;
        var root=Node(parent,name,.31f,y-.027f,.79f,y+.027f);
        root.AddComponent<UnityEngine.UI.Image>().color=Color.clear;
        var track=Node(root.transform,"Track",0,.43f,1,.57f).AddComponent<UnityEngine.UI.Image>();track.color=new Color(.32f,.31f,.28f);track.raycastTarget=false;
        var area=Node(root.transform,"FillArea",0,.43f,1,.57f);
        var fill=Node(area.transform,"Fill",0,0,1,1).AddComponent<UnityEngine.UI.Image>();fill.color=ParisUI.Style.red;fill.raycastTarget=false;
        var handleArea=Node(root.transform,"HandleArea",0,0,1,1);
        var handle=Node(handleArea.transform,"Handle",0,.5f,0,.5f).AddComponent<UnityEngine.UI.Image>();handle.color=ParisUI.Style.cream;
        handle.rectTransform.sizeDelta=new Vector2(10,22);
        var slider=root.AddComponent<UnityEngine.UI.Slider>();slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=true;
        slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.onValueChanged.AddListener(changed);return slider;
    }
    void RefreshAudioPreferences()
    {
        if(masterSlider==null)return;
        SetPreference(masterSlider,masterValue,GamePresentationOptions.MasterVolume);
        SetPreference(musicSlider,musicValue,GamePresentationOptions.MusicVolume);
        SetPreference(effectsSlider,effectsValue,GamePresentationOptions.EffectsVolume);
        SetPreference(heartbeatSlider,heartbeatValue,GamePresentationOptions.HeartbeatVolume);
        SetPreference(subtitleSlider,subtitleValue,GamePresentationOptions.SubtitleScale);
    }
    static void SetPreference(UnityEngine.UI.Slider slider,UnityEngine.UI.Text label,float value)
    {slider.SetValueWithoutNotify(Mathf.Round(value*100));label.text=Mathf.RoundToInt(value*100)+"%";}
}
