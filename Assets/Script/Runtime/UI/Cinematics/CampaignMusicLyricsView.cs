using UnityEngine;

// Captions read the actual music cursor, never a separate picture timer.
public sealed class CampaignMusicLyricsView : MonoBehaviour
{
    GameObject page;
    UnityEngine.UI.Text chinese,english,speaker;
    StoryDirector owner;
    AudioSource music;
    MusicLyricsCatalog catalog;
    AudioClip lastClip;
    MusicLyricTrack track;
    MusicLyricCue shown;
    int shownChapter=-1;
    public bool Visible=>page!=null&&page.activeSelf;
    public string CurrentSpeaker=>speaker!=null?speaker.text:"";
    public string CurrentChinese=>chinese!=null?chinese.text:"";
    public string CurrentEnglish=>english!=null?english.text:"";
    public void Initialize(StoryDirector director,AudioSource source,StartScreenView screen)
    {
        owner=director;music=source;catalog=MusicLyricsCatalog.Load();lastClip=null;shown=null;track=null;shownChapter=-1;
        if(page!=null){page.SetActive(false);Destroy(page);}
        page=new GameObject("MusicLyrics",typeof(RectTransform));page.transform.SetParent(screen.transform,false);
        ParisUI.Place((RectTransform)page.transform,0,0,1,.22f);
        var shade=page.AddComponent<ParisBackdropGraphic>();shade.fadeMode=3;shade.color=new Color(.012f,.016f,.022f,.88f);shade.raycastTarget=false;
        speaker=Label("Speaker",26,.08f,.76f,.92f,.96f,ParisUI.Style.muted);
        chinese=Label("Chinese",42,.06f,.4f,.94f,.77f,ParisUI.Style.cream);
        english=Label("English",30,.06f,.05f,.94f,.4f,new Color(.84f,.84f,.81f));
        var pause=screen.transform.Find("PausePage");if(pause!=null)page.transform.SetSiblingIndex(pause.GetSiblingIndex());
        page.SetActive(false);
    }
    UnityEngine.UI.Text Label(string name,int size,float x0,float y0,float x1,float y1,Color color)
    {
        var label=ParisUI.Label(page.transform,name,"",size,color,x0,y0,x1,y1);label.font=GameFont.Silver;
        label.supportRichText=false;label.resizeTextForBestFit=false;label.raycastTarget=false;label.alignment=TextAnchor.MiddleCenter;
        var shadow=label.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(1,-1);SubtitleSizePreference.Attach(label);return label;
    }
    void LateUpdate()
    {
        if(page==null||owner==null||music==null)return;
        ((RectTransform)page.transform).anchorMax=new Vector2(1,Mathf.Lerp(.22f,.30f,(GamePresentationOptions.SubtitleScale-1)*2));
        if(music.clip!=lastClip){lastClip=music.clip;track=lastClip!=null?catalog.Find(lastClip.name):null;shown=null;}
        var cue=track!=null?track.At(music.time):null;
        bool show=owner.MusicLyricsVisible&&cue!=null;
        if(page.activeSelf!=show)page.SetActive(show);
        if(!show)return;
        if(shown!=cue||shownChapter!=RunState.Chapter)
        {
            shown=cue;shownChapter=RunState.Chapter;
            chinese.text=cue.zh;english.text=cue.en;speaker.text=MusicLyricsCatalog.SpeakerFor(cue,shownChapter);
        }
    }
    void OnDestroy(){if(page!=null)Destroy(page);}
}
