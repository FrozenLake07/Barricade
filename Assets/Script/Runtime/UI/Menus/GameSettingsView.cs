using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

// One settings overlay is shared by title and pause, preserving the existing session freeze/count-in.
[DefaultExecutionOrder(-1100)]
public sealed partial class GameSettingsView : MonoBehaviour
{
    public static GameSettingsView Current {get;private set;}
    public static bool IsOpen=>Current!=null&&Current.page!=null&&Current.page.activeSelf;
    GameObject page;
    UnityEngine.UI.Text status,difficultyLabel,resolutionLabel,modeLabel;
    UnityEngine.UI.Button[] bindingButtons;
    UnityEngine.UI.Button titleButton,pauseButton;
    GameAction? listening;
    int listeningFrame,openedFrame,resolutionIndex;
    bool fullscreen,previousAudioPause;
    Vector2Int[] resolutions;
    float previousScale;
    GameObject returnSelection;
    static readonly string[] DifficultyNames={"简单","普通","困难","噩梦"};
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Current=null;}
    public static void Attach(CampaignMenuView menu)
    {
        var view=menu.GetComponent<GameSettingsView>();if(view==null)view=menu.gameObject.AddComponent<GameSettingsView>();
        view.Build(menu);view.LayoutPause(menu);
    }
    void Build(CampaignMenuView menu)
    {
        Current=this;if(page!=null)return;
        fullscreen=Screen.fullScreen;
        titleButton=Button(menu.titleMenu.transform.parent,"Settings","设置",.10f,.20f,.32f,.27f,()=>Open(titleButton.gameObject));
        // Pause settings sit above Quit; title-menu placement remains independent.
        pauseButton=Button(menu.pausePage.transform,"Settings","设置",.40f,.20f,.60f,.28f,()=>Open(pauseButton.gameObject));
        // Cover selection owns menu placement; use its actual left/right layout after creating the fourth entry.
        GetComponent<StartScreenView>().ApplyTitleCover(false);
        LayoutPause(menu);
        ParisUI.Place(menu.pauseStatus.rectTransform,.24f,.10f,.76f,.18f);
        page=Node(transform,"SettingsPage",0,0,1,1);
        var image=page.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.065f,.061f,.053f,1f);ParisPrintGraphic.Attach(image,0);
        Label(page.transform,"Heading","设置",.08f,.89f,.92f,.97f,60);
        BuildPreferenceTabs();
        var body=controlsPage.transform;
        bindingButtons=new UnityEngine.UI.Button[GameBindings.Defaults.Length];
        for(int i=0;i<bindingButtons.Length;i++)
        {
            int index=i;float x=i<4?.08f:.51f;int row=i<4?i:i-4;float top=.77f-row*.068f;
            Label(body,"Action"+i,GameBindings.Names[i],x,top-.055f,x+.20f,top,38);
            bindingButtons[i]=Button(body,"Key"+i,"",x+.22f,top-.055f,x+.38f,top,()=>{listening=(GameAction)index;listeningFrame=Time.frameCount;status.text="请按下新键，Esc 取消";EventSystem.current?.SetSelectedGameObject(null);});
        }
        difficultyLabel=Label(body,"Difficulty","",.08f,.30f,.56f,.37f,38);
        Button(body,"DifficultyPrev","←",.60f,.305f,.67f,.36f,()=>ChangeDifficulty(-1));
        Button(body,"DifficultyNext","→",.70f,.305f,.77f,.36f,()=>ChangeDifficulty(1));
        Label(body,"DifficultyHint","简单：敌兵50%（第9章80%）  普通：80%  困难：100%\n噩梦：敌兵125%，防守章节额外一队掷弹兵\n噩梦：无换字保护，普通建造不加血",.08f,.215f,.92f,.30f,28);
        var native=Screen.currentResolution;
        resolutions=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height))
            .Concat(new[]{new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080)})
            .Where(r=>r.x>=640&&r.y>=360&&r.x<=Mathf.Max(native.width,Screen.width)&&r.y<=Mathf.Max(native.height,Screen.height)).Distinct().ToArray();
        var current=new Vector2Int(Screen.width,Screen.height);
        if(!resolutions.Contains(current))resolutions=resolutions.Concat(new[]{current}).ToArray();
        resolutions=resolutions.OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();resolutionIndex=Array.IndexOf(resolutions,current);
        resolutionLabel=Label(body,"Resolution","",.08f,.15f,.39f,.215f,38);
        Button(body,"ResolutionPrev","←",.40f,.16f,.46f,.21f,()=>ChangeResolution(-1));
        Button(body,"ResolutionNext","→",.47f,.16f,.53f,.21f,()=>ChangeResolution(1));
        var mode=Button(body,"WindowMode","",.58f,.16f,.78f,.215f,ToggleMode);modeLabel=mode.GetComponentInChildren<UnityEngine.UI.Text>();
        status=Label(page.transform,"Status","",.08f,.095f,.92f,.15f,28);
        Button(body,"ResetBindings","恢复默认按键",.08f,.025f,.35f,.09f,()=>{GameBindings.Reset();Refresh();status.text="已恢复默认按键";});
        Button(page.transform,"Close","返回",.70f,.025f,.92f,.09f,Close);
        BuildAudioPreferences();SelectPreferenceTab(false);page.SetActive(false);Refresh();
    }
    public void LayoutPause(CampaignMenuView menu)
    {
        if(pauseButton==null)return;
        ParisUI.Button(menu.resume,.38f,.64f,.62f,.72f);
        ParisUI.Button(menu.save,.38f,.53f,.62f,.61f);
        ParisUI.Button(menu.load,.38f,.42f,.62f,.50f);
        ParisUI.Button(pauseButton,.38f,.31f,.62f,.39f);
        ParisUI.Button(menu.pauseQuit,.38f,.20f,.62f,.28f);
    }
    public void LayoutTitle(CampaignMenuView menu,float x0,float x1)
    {
        if(titleButton==null)return;
        ParisUI.Button(menu.newGame,x0,.46f,x1,.54f);
        ParisUI.Button(menu.continueGame,x0,.35f,x1,.43f);
        ParisUI.Button(titleButton,x0,.24f,x1,.32f);
        ParisUI.Button(menu.titleQuit,x0,.13f,x1,.21f);
        ParisUI.Place(menu.titleStatus.rectTransform,x0,.075f,x1,.12f);
    }
    public static bool TitleSettingsClick=>Current!=null&&Current.titleButton!=null&&Current.titleButton.gameObject.activeInHierarchy
        &&Input.GetMouseButtonDown(0)&&RectTransformUtility.RectangleContainsScreenPoint(Current.titleButton.GetComponent<RectTransform>(),Input.mousePosition,Current.titleButton.GetComponentInParent<Canvas>().worldCamera);
    public void Open(GameObject selection=null)
    {
        if(IsOpen)return;returnSelection=selection;previousScale=Time.timeScale;previousAudioPause=AudioListener.pause;Time.timeScale=0;AudioListener.pause=true;
        listening=null;openedFrame=Time.frameCount;page.SetActive(true);page.transform.SetAsLastSibling();status.text="修改即时生效并保存";Refresh();CampaignSession.BlockCurrentInput();EventSystem.current?.SetSelectedGameObject(null);
    }
    public void Close()
    {
        if(!IsOpen)return;StopSettingsCalibration();listening=null;page.SetActive(false);Time.timeScale=previousScale;AudioListener.pause=previousAudioPause;CampaignSession.BlockCurrentInput();EventSystem.current?.SetSelectedGameObject(returnSelection);
    }
    void Update()
    {
        if(!IsOpen||Time.frameCount<=openedFrame)return;
        if(calibrating){UpdateSettingsCalibrationInput();return;}
        if(listening.HasValue)
        {
            if(Time.frameCount<=listeningFrame)return;
            if(Input.GetKeyDown(KeyCode.Escape)){listening=null;status.text="已取消";return;}
            foreach(KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if(!GameBindings.Allowed(key)||!Input.GetKeyDown(key))continue;
                if(GameBindings.TrySet(listening.Value,key,out string reason)){listening=null;Refresh();}
                status.text=reason;return;
            }
        }
        else if(Input.GetKeyDown(KeyCode.Escape))Close();
    }
    void ChangeDifficulty(int delta){GameOptions.Difficulty=(GameDifficulty)(((int)GameOptions.Difficulty+delta+DifficultyNames.Length)%DifficultyNames.Length);Refresh();status.text="已保存；后续刷敌采用新难度，场上敌人保持不变";}
    void ChangeResolution(int delta){resolutionIndex=(resolutionIndex+delta+resolutions.Length)%resolutions.Length;ApplyResolution();}
    void ToggleMode(){fullscreen=!fullscreen;var r=resolutions[resolutionIndex];ApplyScreen(r,fullscreen);Refresh();}
    void ApplyResolution(){ApplyScreen(resolutions[resolutionIndex],fullscreen);Refresh();}
    static void ApplyScreen(Vector2Int r,bool full)
    {
        PlayerPrefs.SetInt("Barricade.ScreenWidth",r.x);PlayerPrefs.SetInt("Barricade.ScreenHeight",r.y);PlayerPrefs.SetInt("Barricade.Fullscreen",full?1:0);PlayerPrefs.Save();
        Screen.SetResolution(r.x,r.y,full?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void RestoreScreen()
    {
        if(!Application.isEditor&&PlayerPrefs.HasKey("Barricade.ScreenWidth"))ApplyScreen(new Vector2Int(PlayerPrefs.GetInt("Barricade.ScreenWidth"),PlayerPrefs.GetInt("Barricade.ScreenHeight")),PlayerPrefs.GetInt("Barricade.Fullscreen",1)!=0);
    }
    void Refresh()
    {
        if(bindingButtons==null)return;
        RefreshAudioPreferences();
        for(int i=0;i<bindingButtons.Length;i++)bindingButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().text=GameBindings.Label((GameAction)i);
        difficultyLabel.text="难度："+DifficultyNames[(int)GameOptions.Difficulty];var r=resolutions[resolutionIndex];resolutionLabel.text=$"分辨率：{r.x} × {r.y}";modeLabel.text=fullscreen?"全屏（点击切换）":"窗口（点击切换）";
    }
    void OnDestroy(){if(Current==this){if(IsOpen){StopSettingsCalibration();Time.timeScale=previousScale;AudioListener.pause=previousAudioPause;}Current=null;}}
    static GameObject Node(Transform parent,string name,float x0,float y0,float x1,float y1)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;return go;
    }
    static UnityEngine.UI.Text Label(Transform parent,string name,string value,float x0,float y0,float x1,float y1,int size)
    {
        var go=Node(parent,name,x0,y0,x1,y1);var t=go.AddComponent<UnityEngine.UI.Text>();t.font=GameFont.Silver;t.fontSize=size;t.resizeTextForBestFit=true;t.resizeTextMinSize=16;t.resizeTextMaxSize=size;t.text=value;t.color=ParisUI.Style.cream;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;
    }
    static UnityEngine.UI.Button Button(Transform parent,string name,string value,float x0,float y0,float x1,float y1,UnityEngine.Events.UnityAction action)
    {
        var go=Node(parent,name,x0,y0,x1,y1);var image=go.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.13f,.15f,.17f,1);var b=go.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;ParisUI.StyleButton(b);
        if(parent.name=="SettingsPage"||parent.name=="PausePage"||parent.name=="ControlsPage"||parent.name=="AudioPage"||parent.name=="SettingsCalibration")ParisPrintGraphic.Attach(image,4);
        var text=Label(go.transform,"Label",value,.05f,.02f,.95f,.98f,38);text.alignment=TextAnchor.MiddleCenter;b.onClick.AddListener(action);SelectedButtonAudio.Attach(b);return b;
    }
}
