using System;
using System.Collections.Generic;
using System.Text;

// Pure parser: quotes protect whitespace/semicolons; Windows backslashes stay literal.
public static class ConsoleSyntax
{
    public static List<string[]> Parse(string text)
    {
        if(text==null)throw new ArgumentNullException(nameof(text));
        if(text.Length>16384)throw new FormatException("单次输入不能超过16384字符。");
        var result=new List<string[]>();var words=new List<string>();var word=new StringBuilder();
        char quote='\0';bool started=false;
        for(int i=0;i<text.Length;i++)
        {
            char c=text[i];
            if(quote!='\0')
            {
                if(c=='\\'&&i+1<text.Length&&text[i+1]==quote){word.Append(text[++i]);continue;}
                if(c==quote){quote='\0';continue;}word.Append(c);continue;
            }
            if(c=='"'||c=='\''){quote=c;started=true;continue;}
            if(char.IsWhiteSpace(c)||c==';'||i==text.Length)
            {
                if(started){words.Add(word.ToString());word.Clear();started=false;}
                if(c==';'&&words.Count>0){result.Add(words.ToArray());words.Clear();}
            }
            else{word.Append(c);started=true;}
        }
        if(quote!='\0')throw new FormatException("引号没有闭合。");
        if(started)words.Add(word.ToString());if(words.Count>0)result.Add(words.ToArray());
        if(result.Count>256)throw new FormatException("一次最多执行256条命令。");
        return result;
    }
}

public sealed class ConsoleHistory
{
    readonly List<string> entries=new List<string>();int cursor;string draft="";
    public void Add(string line){if(entries.Count==0||entries[entries.Count-1]!=line)entries.Add(line);if(entries.Count>100)entries.RemoveAt(0);cursor=entries.Count;draft="";}
    public string Move(int direction,string current)
    {
        if(cursor==entries.Count)draft=current;
        cursor=Math.Max(0,Math.Min(entries.Count,cursor+direction));return cursor==entries.Count?draft:entries[cursor];
    }
}
