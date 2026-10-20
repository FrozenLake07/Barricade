using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

// Only known gameplay objects are passed here. Assets, delegates and Unity internals are excluded.
public static class CampaignStateCodec
{
    [Serializable] public sealed class Field
    {
        public string name,kind,value;
        public List<Field> fields;
        public List<string> items;
    }
    static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    static bool Nested(Type type)=>type==typeof(HeroStateMachine)||type==typeof(ConstructionJudgement)||type==typeof(CampaignMagazine);
    static readonly HashSet<string> Ignore=new HashSet<string>{"subscribedTap","subscribedFlow","appliedRevision","checkedController","checkedVersion","warned","initialized","originalSprite","hitTargets"};
    public static List<Field> Capture(object target,Func<UnityEngine.Object,string> reference,params string[] only)
    {
        var result=new List<Field>();if(target==null)return result;
        bool isType=target is Type;Type type=isType?(Type)target:target.GetType();
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly|(isType?BindingFlags.Static:BindingFlags.Instance);
        foreach(var field in type.GetFields(flags))
        {
            if(field.IsLiteral||Ignore.Contains(field.Name)||(only.Length>0&&Array.IndexOf(only,field.Name)<0))continue;
            var record=Read(field.GetValue(isType?null:target),field.FieldType,reference);
            if(record!=null){record.name=field.Name;result.Add(record);}
        }
        return result;
    }
    static Field Read(object value,Type type,Func<UnityEngine.Object,string> reference)
    {
        var item=new Field();
        if(type==typeof(string)){item.kind="string";item.value=(string)value;}
        else if(type.IsEnum||type==typeof(int)||type==typeof(long)||type==typeof(float)||type==typeof(double)||type==typeof(bool))
        {item.kind="scalar";item.value=type.IsEnum?Convert.ToInt64(value).ToString(Invariant):Convert.ToString(value,Invariant);}
        else if(type==typeof(Vector2)||type==typeof(Vector3)||type==typeof(Quaternion)||type==typeof(Color))
        {item.kind="struct";item.value=JsonUtility.ToJson(value);}
        else if(typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            if(value==null||(UnityEngine.Object)value==null){item.kind="reference";item.value="";}
            else {string key=reference((UnityEngine.Object)value);if(key==null)return null;item.kind="reference";item.value=key;}
        }
        else if(Nested(type))
        {item.kind="nested";item.fields=value==null?null:Capture(value,reference);}
        else if(type.IsArray||type==typeof(List<int>)||type==typeof(List<string>))
        {
            Type element=type.IsArray?type.GetElementType():type.GetGenericArguments()[0];
            if(element!=typeof(int)&&element!=typeof(float)&&element!=typeof(bool)&&element!=typeof(string)&&element!=typeof(Color)&&!typeof(UnityEngine.Object).IsAssignableFrom(element))return null;
            item.kind="array";item.fields=new List<Field>();
            if(value!=null)foreach(var entry in (IEnumerable)value)item.fields.Add(Read(entry,element,reference));
        }
        else return null;
        return item;
    }
    public static void Restore(object target,List<Field> records,Func<string,UnityEngine.Object> resolve,
        float sceneOffset=0,float gameOffset=0,float unscaledOffset=0)
    {
        if(target==null||records==null)return;
        bool isType=target is Type;Type type=isType?(Type)target:target.GetType();
        var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly|(isType?BindingFlags.Static:BindingFlags.Instance);
        foreach(var record in records)
        {
            var field=type.GetField(record.name,flags);if(field==null||field.IsLiteral||Ignore.Contains(record.name))continue;
            object existing=field.GetValue(isType?null:target);
            if(record.kind=="nested")
            {if(Nested(field.FieldType)&&existing!=null)Restore(existing,record.fields,resolve,sceneOffset,gameOffset,unscaledOffset);continue;}
            object value=Decode(record,field.FieldType,resolve);
            value=Rebase(type,field.Name,value,sceneOffset,gameOffset,unscaledOffset);
            if(field.IsInitOnly)
            {
                var list=existing as IList;var from=value as IList;
                if(list!=null&&from!=null){list.Clear();foreach(var entry in from)list.Add(entry);}
                continue;
            }
            field.SetValue(isType?null:target,value);
        }
    }
    static object Decode(Field record,Type type,Func<string,UnityEngine.Object> resolve)
    {
        switch(record.kind)
        {
            case "string":return record.value;
            case "scalar":return type.IsEnum?Enum.ToObject(type,long.Parse(record.value,Invariant)):Convert.ChangeType(record.value,type,Invariant);
            case "struct":return JsonUtility.FromJson(record.value,type);
            case "reference":return string.IsNullOrEmpty(record.value)?null:resolve(record.value);
            case "array":
                Type element=type.IsArray?type.GetElementType():type.GetGenericArguments()[0];
                int count=record.fields?.Count??0;
                var array=Array.CreateInstance(element,count);
                for(int i=0;i<count;i++)if(record.fields[i]!=null)array.SetValue(Decode(record.fields[i],element,resolve),i);
                if(type.IsArray)return array;
                var list=(IList)Activator.CreateInstance(type);foreach(var value in array)list.Add(value);return list;
            default:throw new InvalidOperationException("未知存档字段类型："+record.kind);
        }
    }
    static object Rebase(Type type,string name,object value,float scene,float game,float unscaled)
    {
        if(value==null)return null;
        string key=type.Name+"."+name;
        bool sceneTime=key=="CampaignMagazine.reloadTime"||key=="EnemyMove.birthTime"||key=="EnemyMove.pendingFireAt"
            ||key=="Cannon.nextShot"||key=="Cannon.releaseAt"||key=="Cannon.animationEndsAt"||key=="CannonShell.expiresAt"
            ||key=="SBullet.reloadReadyTime"||key=="CapsuleTrack.switchReadyTime";
        bool gameTime=key=="StoryDirector.captionUntil"||key=="ConstructionJudgement.firstTime"||key=="ConstructionJudgement.secondTime";
        bool unscaledTime=key=="StoryDirector.titleSince"||key=="SBullet.<ReloadFlashUntil>k__BackingField";
        if(!sceneTime&&!gameTime&&!unscaledTime)return value;
        double number=Convert.ToDouble(value,Invariant);
        bool zeroValid=key=="EnemyMove.birthTime"||key.StartsWith("ConstructionJudgement.")||key=="CampaignMagazine.reloadTime";
        if(number<0||(!zeroValid&&number==0))return value;
        number+=sceneTime?scene:gameTime?game:unscaled;
        return value is float?(object)(float)number:number;
    }
}
