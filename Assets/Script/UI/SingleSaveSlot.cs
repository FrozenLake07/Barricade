using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// One durable file. A temporary write is flushed and atomically replaces the previous slot.
public static class SingleSaveSlot
{
    const string Header="BARRICADE_SAVE_1";
    const int MaximumBytes=8*1024*1024;
    static string Hash(string value)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
    public static void Write(string path,string payload)
    {
        if(string.IsNullOrEmpty(payload)||Encoding.UTF8.GetByteCount(payload)>MaximumBytes)throw new InvalidDataException("存档内容为空或过大。");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary=path+".tmp";
        try
        {
            byte[] bytes=new UTF8Encoding(false).GetBytes(Header+"\n"+Hash(payload)+"\n"+payload);
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path))File.Replace(temporary,path,null);
            else File.Move(temporary,path);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static bool TryRead(string path,out string payload,out string error)
    {
        payload=null;error=null;
        try
        {
            if(!File.Exists(path)){error="暂无存档";return false;}
            if(new FileInfo(path).Length>MaximumBytes+128)throw new InvalidDataException("存档过大。");
            string text=File.ReadAllText(path,Encoding.UTF8);
            int first=text.IndexOf('\n'),second=first<0?-1:text.IndexOf('\n',first+1);
            if(first<0||second<0||text.Substring(0,first)!=Header)throw new InvalidDataException("存档版本或格式不正确。");
            payload=text.Substring(second+1);
            if(string.IsNullOrEmpty(payload)||!string.Equals(text.Substring(first+1,second-first-1),Hash(payload),StringComparison.Ordinal))
                throw new InvalidDataException("存档校验失败。");
            return true;
        }
        catch(Exception e){payload=null;error="无法读取存档："+e.Message;return false;}
    }
}
