using System.Collections;

public sealed partial class StoryDirector
{
    public bool ChapterOpeningWordsActive {get;private set;}
    IEnumerator PlayChapterOpeningWords(int chapter)
    {
        var entry=ChapterOpeningWords.Find(chapter);
        if(entry==null||string.IsNullOrWhiteSpace(entry.text))yield break;
        ChapterOpeningWordsActive=true;
        try{yield return OpeningCard("chapter-epigraph",entry.text,entry.seconds,44,true);}
        finally{ChapterOpeningWordsActive=false;}
    }
}
