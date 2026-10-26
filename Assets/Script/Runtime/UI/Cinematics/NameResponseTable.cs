using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[CreateAssetMenu(menuName="街垒/对话/姓名回应表")]
public sealed class NameResponseTable : ScriptableObject
{
    [Serializable] public sealed class Response
    {
        public string id="";
        [Tooltip("完整匹配；忽略首尾空格、英文大小写与全角英文差异。")]
        public string[] aliases=Array.Empty<string>();
        public string speaker="";
        [TextArea(2,6),Tooltip("支持 {playerName}。留空则不等待回应，直接继续演出。")]
        public string reply="";
        public bool extraBeforeThird;
        public string extraSpeaker="";
        [TextArea(2,6)] public string extraReply="";
    }
    [Serializable] public sealed class SpecialNameFile
    {
        public Response[] specialNames;
    }
    [Tooltip("特殊名字配置文件；留空时自动读取 Resources/SpecialNameResponses.txt。默认/匿名回复仍在本资产配置。旧 JSON 引用仍兼容。")]
    public TextAsset specialNamesJson;
    [NonSerialized] string cachedJson;
    [NonSerialized] Response[] cachedRules;
    Response[] SpecialRules()
    {
        // Resources includes the editable configuration in both the Editor and builds.
        var source=specialNamesJson!=null?specialNamesJson:Resources.Load<TextAsset>("SpecialNameResponses");
        if(source==null)return specialNames;
        string text=source.text;
        if(cachedJson!=text)
        {
            cachedJson=text;cachedRules=null;
            try
            {
                if(text.TrimStart('\uFEFF',' ','\t','\r','\n').StartsWith("{",StringComparison.Ordinal))
                {
                    var file=JsonUtility.FromJson<SpecialNameFile>(text);
                    if(file==null||file.specialNames==null)throw new FormatException("缺少 specialNames 数组。");
                    cachedRules=file.specialNames;
                }
                else cachedRules=ParseText(text);
            }
            catch(Exception e){Debug.LogWarning("[NameResponseTable] 特殊姓名配置无效，使用原有列表："+e.Message,this);}
        }
        return cachedRules??specialNames;
    }
    // Plain-text blocks; this parser also runs in standalone checks without the Editor.
    public static Response[] ParseText(string text)
    {
        var rules=new List<Response>();Response current=null;string previous="";
        var identifiers=new HashSet<string>(StringComparer.Ordinal);
        var aliases=new HashSet<string>(StringComparer.Ordinal);
        using(var reader=new StringReader((text??"").TrimStart('\uFEFF')))
        {
            string raw;int number=0;
            while((raw=reader.ReadLine())!=null)
            {
                number++;string line=raw.Trim();if(line.Length==0)continue;
                // Indented lines continue the preceding reply, allowing normal multiline dialogue.
                if(char.IsWhiteSpace(raw[0])&&(previous=="回复"||previous=="插入内容"))
                {
                    if(previous=="回复")current.reply+="\n"+line;else current.extraReply+="\n"+line;
                    continue;
                }
                if(line.StartsWith("#",StringComparison.Ordinal))continue;
                if(line.StartsWith("[",StringComparison.Ordinal)&&line.EndsWith("]",StringComparison.Ordinal))
                {
                    string id=line.Substring(1,line.Length-2).Trim();
                    if(id.Length==0||!identifiers.Add(id))throw new FormatException("第"+number+"行：分组标识为空或重复。");
                    current=new Response{id=id};rules.Add(current);previous="";continue;
                }
                int split=line.IndexOf('=');
                if(current==null||split<0)throw new FormatException("第"+number+"行：请使用 [分组] 或 字段=内容。");
                string key=line.Substring(0,split).Trim(),value=line.Substring(split+1).Trim();previous=key;
                switch(key)
                {
                    case "名称":current.aliases=Array.ConvertAll(value.Split('|'),a=>a.Trim());break;
                    case "说话人":current.speaker=value;break;
                    case "回复":current.reply=value.Replace("\\n","\n");break;
                    case "插入对话":
                        switch(value.ToLowerInvariant())
                        {
                            case "true":case "是":case "1":current.extraBeforeThird=true;break;
                            case "false":case "否":case "0":case "":current.extraBeforeThird=false;break;
                            default:throw new FormatException("第"+number+"行：插入对话请填写 true / false（或 是 / 否）。");
                        }
                        break;
                    case "插入说话人":current.extraSpeaker=value;break;
                    case "插入内容":current.extraReply=value.Replace("\\n","\n");break;
                    default:throw new FormatException("第"+number+"行：未知字段「"+key+"」。");
                }
            }
        }
        foreach(var rule in rules)
        {
            bool named=false;
            foreach(string alias in rule.aliases)
            {
                string key=MatchKey(alias);if(key.Length==0)continue;named=true;
                if(!aliases.Add(key))throw new FormatException("名字重复：「"+alias+"」。请只放在一组中。");
            }
            if(!named)throw new FormatException("分组 ["+rule.id+"] 缺少名称。");
        }
        return rules.ToArray();
    }
    public Response[] specialNames=Array.Empty<Response>();
    public Response defaultResponse=new Response{id="default"};
    public Response unnamedResponse=new Response{id="unnamed"};
    public Response Resolve(string name)
    {
        string key=MatchKey(name);
        if(key.Length==0)return unnamedResponse;
        foreach(var rule in SpecialRules()??Array.Empty<Response>())
            if(rule!=null&&Array.Exists(rule.aliases??Array.Empty<string>(),alias=>MatchKey(alias).Length>0&&string.Equals(MatchKey(alias),key,StringComparison.Ordinal)))return rule;
        return defaultResponse;
    }
    public static string MatchKey(string name)=>(name??"").Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant();
}
