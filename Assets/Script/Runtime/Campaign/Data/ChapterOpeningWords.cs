using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public sealed class ChapterOpeningWords
{
    public int chapter;
    public float seconds=5;
    public string text;
    public static ChapterOpeningWords[] Parse(string content)
    {
        var result=new List<ChapterOpeningWords>();ChapterOpeningWords current=null;
        using(var reader=new StringReader((content??"").TrimStart('\uFEFF')))
        {
            string raw;int line=0;
            while((raw=reader.ReadLine())!=null)
            {
                line++;string value=raw.Trim();if(value.StartsWith("#",StringComparison.Ordinal))continue;
                if(value.StartsWith("[",StringComparison.Ordinal)&&value.EndsWith("]",StringComparison.Ordinal))
                {
                    if(!int.TryParse(value.Substring(1,value.Length-2),out int number)||number<1||number>9||result.Exists(e=>e.chapter==number))throw new FormatException("章首语第"+line+"行：章节应为不重复的1—9。");
                    current=new ChapterOpeningWords{chapter=number,text=""};result.Add(current);continue;
                }
                if(current==null){if(value.Length==0)continue;throw new FormatException("章首语第"+line+"行：请先填写[章节数字]。");}
                if(value.StartsWith("秒数=",StringComparison.Ordinal))
                {
                    if(!float.TryParse(value.Substring(3),NumberStyles.Float,CultureInfo.InvariantCulture,out float seconds)||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0)throw new FormatException("章首语第"+line+"行：秒数必须大于0。");
                    current.seconds=seconds;continue;
                }
                current.text+=(current.text.Length==0?"":"\n")+raw.TrimEnd();
            }
        }
        foreach(var entry in result)entry.text=entry.text.Trim();return result.ToArray();
    }
    public static ChapterOpeningWords Find(int chapter)
    {
        if(chapter<1||chapter>9)return null;
        var file=Resources.Load<TextAsset>("Campaign/ChapterOpeningWords");if(file==null)return null;
        try{return Array.Find(Parse(file.text),e=>e.chapter==chapter);}
        catch(FormatException e){Debug.LogWarning(e.Message);return null;}
    }
}
