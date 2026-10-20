using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class CampaignSession : MonoBehaviour
{
    enum Mode { Live, Paused, CountIn, Loading }
    const string LaunchedKey="Barricade.HasLaunched";
    public static CampaignSession Current {get;private set;}
    public static bool Suspended=>Current!=null&&Current.mode!=Mode.Live;
    public static bool InputBlocked=>Suspended||(Current!=null&&Time.frameCount<=Current.blockedThroughFrame);
    public static bool Restoring=>Current!=null&&Current.mode==Mode.Loading;
    public static bool Counting=>Current!=null&&Current.mode==Mode.CountIn;
    public static float CountPhase=>Counting?Current.count.Phase:0f;
    public static string SavePath=>Path.Combine(Application.persistentDataPath,"barricade-slot.sav");
    StoryDirector owner;
    StartScreenView screen;
    CampaignMenuView menu;
    Mode mode;
    readonly ResumeCountIn count=new ResumeCountIn();
    AudioSource pulse;
    AudioClip pulseClip;
    EventSystem events;
    Coroutine counting;
    bool firstLaunch,consumedFirst,ownsFreeze,previousAudioPause;
    float previousScale;
    int blockedThroughFrame=-1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Current=null;}
    void Awake(){Current=this;}
    public void Initialize(StoryDirector director,StartScreenView view)
    {
        owner=director;firstLaunch=PlayerPrefs.GetInt(LaunchedKey,0)==0;
        PlayerPrefs.SetInt(LaunchedKey,1);PlayerPrefs.Save();
        pulse=gameObject.AddComponent<AudioSource>();pulse.playOnAwake=false;pulse.ignoreListenerPause=true;pulse.volume=.6f;
        pulseClip=Resources.Load<AudioClip>("SFX/HeartbeatPulse/Pulse-V1");
        var go=new GameObject("CampaignEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));go.transform.SetParent(transform,false);events=go.GetComponent<EventSystem>();
        SceneManager.sceneLoaded+=SceneLoaded;EnsureEvents();Bind(view);
    }
    void SceneLoaded(Scene scene,LoadSceneMode loadMode){EnsureEvents();}
    void EnsureEvents()
    {
        if(events==null)return;
        foreach(var system in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))if(system!=events)system.enabled=false;
        events.enabled=true;EventSystem.current=events;
    }
    public void Bind(StartScreenView view)
    {
        screen=view;menu=CampaignMenuView.Ensure(view);
        Bind(menu.newGame,NewGame);Bind(menu.continueGame,LoadGame);Bind(menu.titleQuit,Quit);
        Bind(menu.resume,Resume);Bind(menu.save,Save);Bind(menu.load,LoadGame);Bind(menu.pauseQuit,Quit);
        menu.pausePage.SetActive(false);menu.countInPage.SetActive(false);
    }
    static void Bind(UnityEngine.UI.Button button,UnityEngine.Events.UnityAction action)
    {button.onClick.RemoveAllListeners();button.onClick.AddListener(action);}
    public void ShowTitle()
    {
        bool first=firstLaunch&&!consumedFirst;
        screen.prompt.gameObject.SetActive(first);menu.titleMenu.SetActive(!first);
        RefreshSlot();if(!first)Select(menu.newGame);
    }
    public bool FirstPrompt=>firstLaunch&&!consumedFirst;
    public void NewGame()
    {
        if(mode!=Mode.Live||owner.Phase!=CampaignPhase.Title)return;
        consumedFirst=true;owner.StartNewCampaign();
    }
    void Update()
    {
        if(owner==null||menu==null)return;
        if(mode==Mode.Live&&Input.GetKeyDown(KeyCode.Escape)&&owner.CanPause)Pause();
        else if((mode==Mode.Paused||mode==Mode.CountIn)&&Input.GetKeyDown(KeyCode.Escape))
        {
            if(mode==Mode.CountIn){if(counting!=null)StopCoroutine(counting);pulse.Stop();mode=Mode.Paused;menu.countInPage.SetActive(false);menu.pausePage.SetActive(true);Select(menu.resume);}
            else Resume();
        }
    }
    void Freeze()
    {
        if(ownsFreeze)return;
        previousScale=Time.timeScale;previousAudioPause=AudioListener.pause;ownsFreeze=true;
        Time.timeScale=0;AudioListener.pause=true;
    }
    void Thaw()
    {
        if(!ownsFreeze)return;
        Time.timeScale=previousScale;AudioListener.pause=previousAudioPause;ownsFreeze=false;
    }
    public void Pause()
    {
        if(mode!=Mode.Live||!owner.CanPause)return;
        mode=Mode.Paused;Freeze();menu.pausePage.SetActive(true);RefreshSlot();Select(menu.resume);
    }
    public void Resume()
    {if(mode==Mode.Paused)counting=StartCoroutine(CountAndResume());}
    IEnumerator CountAndResume()
    {
        mode=Mode.CountIn;menu.pausePage.SetActive(false);menu.countInPage.SetActive(true);events.SetSelectedGameObject(null);
        count.Reset(owner.Clock.Period>.1f?owner.Clock.Period:1.35f);int hit=0;menu.countInText.text="3";
        while(!count.Finished)
        {
            menu.countInText.text=count.Remaining.ToString();
            // Each heartbeat has two audible pulses, matching the project's heart visual.
            double at=(hit/2+(hit%2==0?.12:.30))*count.Period;
            if(hit<ResumeCountIn.Beats*2&&count.Elapsed>=at){if(pulseClip!=null)pulse.PlayOneShot(pulseClip);hit++;}
            yield return null;
            count.Advance(Math.Min(.1f,Time.unscaledDeltaTime));
        }
        menu.countInPage.SetActive(false);pulse.Stop();blockedThroughFrame=Time.frameCount;
        mode=Mode.Live;Thaw();counting=null;
    }
    void RefreshSlot()
    {
        bool exists=CampaignSaveData.TryRead(SavePath,out var saved,out string error);
        menu.continueGame.interactable=menu.load.interactable=exists;menu.save.interactable=owner.CanSave;
        string description=exists?$"唯一存档：第{saved.chapter}章  {ReadOpened(saved)}拍\n{DateTime.Parse(saved.utc).ToLocalTime():yyyy-MM-dd HH:mm:ss}":error;
        menu.titleStatus.text=description;
        menu.pauseStatus.text=(owner.CanSave?"Esc：继续   存档将覆盖唯一存档位":"过场播放中，结束后可存档")+"\n"+description;
    }
    static int ReadOpened(CampaignSaveData data)
    {var field=data.clock.Find(item=>item.name=="<Opened>k__BackingField");return field!=null&&int.TryParse(field.value,out int value)?value:0;}
    public void Save()
    {
        if(mode!=Mode.Paused||!owner.CanSave)return;
        try{var data=owner.CaptureSavedGame();SingleSaveSlot.Write(SavePath,JsonUtility.ToJson(data));RefreshSlot();menu.pauseStatus.text="已存档（唯一存档位）\n"+menu.titleStatus.text;}
        catch(Exception e){menu.pauseStatus.text="存档失败，原存档已保留："+e.Message;Debug.LogException(e);}
    }
    public void LoadGame()
    {
        if(mode==Mode.Loading||mode==Mode.CountIn)return;
        if(!CampaignSaveData.TryRead(SavePath,out var data,out string error)){menu.pauseStatus.text=menu.titleStatus.text=error;return;}
        consumedFirst=true;StartCoroutine(Load(data));
    }
    IEnumerator Load(CampaignSaveData data)
    {
        mode=Mode.Loading;Freeze();menu.pauseStatus.text=menu.titleStatus.text="正在读档……";
        var restore=owner.RestoreSavedGame(data);Exception failure=null;
        while(true)
        {
            bool more=false;object next=null;
            try{more=restore.MoveNext();if(more)next=restore.Current;}catch(Exception e){failure=e;}
            if(failure!=null||!more)break;
            yield return next;
        }
        if(failure!=null)
        {
            Debug.LogException(failure);yield return owner.RecoverLoadFailure();mode=Mode.Live;Thaw();
            menu.titleStatus.text="读档失败，存档文件已保留："+failure.Message;yield break;
        }
        mode=Mode.Paused;counting=StartCoroutine(CountAndResume());
    }
    void Select(UnityEngine.UI.Button button){events.SetSelectedGameObject(button.gameObject);}
    public void Quit()
    {
        pulse.Stop();Thaw();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded-=SceneLoaded;
        if(Current==this){Thaw();Current=null;}
    }
}
