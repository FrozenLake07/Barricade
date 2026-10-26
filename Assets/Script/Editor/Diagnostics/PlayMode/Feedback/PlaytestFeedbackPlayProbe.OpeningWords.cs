#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class PlaytestFeedbackPlayProbe
{
    IEnumerator OpeningWordsTest()
    {
        RememberPreferences();
        yield return Frames(4);
        DebugConsole.Current?.SetOpen(false);
        report.coverage="Actual chapter 1-9 announcement/epigraph/picture ordering, content, game font glyph coverage, no lyric overlap, five-second automatic completion, skip, chapter 3-8 music start after epigraph, retry and chapter 10 exclusion. User scene not saved.";
        var entries=ChapterOpeningWords.Parse(Resources.Load<TextAsset>("Campaign/ChapterOpeningWords").text);
        Check(entries.Length==9 && entries.All(e=>e.seconds==5),"All nine epigraphs are configured for five seconds");
        Check(ChapterOpeningWords.Find(10)==null,"Chapter ten has no epigraph configuration");
        var music=Get<AudioSource>(story,"audioSource");
        var events=new List<string>();
        bool earlyMusic=false;
        Action<int,string> receive=(chapter,key)=>{events.Add(chapter+":"+key);if(chapter>=3 && chapter<=8 && (key=="chapter-announcement" || key=="chapter-epigraph") && (music.isPlaying || Get<bool>(story,"musicPlaybackActive")))earlyMusic=true;};
        StoryDirector.TransitionRequested+=receive;
        try
        {
            for(int number=1;number<=9;number++)
            {
                events.Clear();OpenLyricsChapter(number);
                yield return Until(()=>story.ChapterOpeningWordsActive,true);
                yield return Frames(2);
                var expected=ChapterOpeningWords.Find(number);
                var label=Get<UnityEngine.UI.Text>(Film,"centerTitle");
                Check(Film.IsBlackCardVisible && label.text==expected.text,"Chapter "+number+" displays its exact epigraph alone");
                Check(events.IndexOf(number+":chapter-announcement")>=0 && events.IndexOf(number+":chapter-epigraph")>events.IndexOf(number+":chapter-announcement") && !events.Contains(number+":chapter-entry") && !events.Contains(number+":chapter-opening"),"Chapter "+number+" epigraph follows announcement and precedes pictures/Timeline");
                Check(label.font==GameFont.Silver && expected.text.Where(c=>!char.IsWhiteSpace(c)).All(c=>label.font.HasCharacter(c)),"Chapter "+number+" uses game font with all characters present");
                Check(!story.MusicLyricsVisible,"Chapter "+number+" epigraph suppresses simultaneous music lyrics");
                if(number>=3 && number<=8)Check(!earlyMusic && !music.isPlaying && !Get<bool>(story,"musicPlaybackActive"),"Chapter "+number+" announcement and epigraph are silent, including previous chapter music");
                if(number<=2)Check(music.isPlaying,"Chapter "+number+" retains music before epigraph ends");
                if(number==9)Check(!music.isPlaying,"Chapter nine remains without music");
                if(number==1||number==5||number==7||number==9)CaptureUi(960,540,"第"+number+"章章首语");
                float start=Time.time;
                if(number==3)
                {
                    while(story.ChapterOpeningWordsActive){if(music.isPlaying)throw new Exception("Music started during epigraph");yield return null;}
                    Check(Time.time-start>4.5f && Time.time-start<5.5f,"Epigraph automatically finishes after five seconds without skip");
                }
                else
                {
                    story.DebugSkip();yield return Until(()=>!story.ChapterOpeningWordsActive);
                    Check(Time.time-start<1,"Chapter "+number+" epigraph supports early skip");
                }
                if(number==1)yield return Until(()=>story.Phase==CampaignPhase.Cinematic);
                else yield return Until(()=>events.Contains(number+":chapter-entry")||events.Contains(number+":chapter-opening"));
                if(number>=3 && number<=8)Check(music.isPlaying && Get<bool>(story,"musicPlaybackActive") && music.clip==story.table.chapters[number-1].openingMusic && music.time<.3f,"Chapter "+number+" starts its correct cue at the beginning when pictures start");
            }
            events.Clear();
            if(lyricFlow!=null)StopCoroutine(lyricFlow);
            lyricFlow=StartCoroutine(story.DebugChapter(3,false,true));
            yield return Until(()=>events.Contains("3:chapter-entry"));
            Check(!story.ChapterOpeningWordsActive && !events.Contains("3:chapter-epigraph") && !events.Contains("3:chapter-announcement"),"Retry skips announcement and epigraph");
            Check(music.isPlaying && music.clip==story.table.chapters[2].openingMusic,"Retry without epigraph starts chapter music immediately");
            events.Clear();RunState.Ending=CampaignEnding.Hope;OpenLyricsChapter(10);
            yield return Until(()=>RunState.Chapter==10 && Film.Active && !Film.IsBlackCardVisible);
            Check(music.isPlaying,"Chapter ten ending music still begins immediately");
            Check(!story.ChapterOpeningWordsActive && !events.Contains("10:chapter-epigraph"),"Chapter ten goes directly from announcement to ending images");
        }
        finally{StoryDirector.TransitionRequested-=receive;}
    }
}
#endif
