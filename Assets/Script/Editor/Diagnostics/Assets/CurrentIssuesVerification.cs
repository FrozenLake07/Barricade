#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CurrentIssuesVerification
{
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/CurrentIssuesInspect.request"));
    static CurrentIssuesVerification(){EditorApplication.update+=Poll;}
    [Serializable] public sealed class Frame {public string name,path;public Vector2 pivot,size;public float ppu;public Rect opaque,body;}
    [Serializable] sealed class Report {public bool success,playing,sceneDirty;public string scene,error;public List<Frame> frames=new List<Frame>();public List<string> routes=new List<string>();public string[] music;}
    public static Rect Opaque(Sprite sprite)
    {
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
            var pixels=tex.GetPixels32();var rect=sprite.rect;int x0=(int)rect.x,y0=(int)rect.y;
            int left=(int)rect.width,bottom=(int)rect.height,right=-1,top=-1;
            for(int y=0;y<(int)rect.height;y++)for(int x=0;x<(int)rect.width;x++)
                if(pixels[(y+y0)*tex.width+x+x0].a>=32){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
            return right<0?new Rect():new Rect(left,bottom,right-left+1,top-bottom+1);
        }
        finally{UnityEngine.Object.DestroyImmediate(tex);}
    }
    public static Rect Body(Sprite sprite)
    {
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));var pixels=tex.GetPixels32();
            int width=(int)sprite.rect.width,height=(int)sprite.rect.height,x0=(int)sprite.rect.x,y0=(int)sprite.rect.y;
            var seen=new bool[width*height];var queue=new Queue<int>();int largest=0;var result=new Rect();
            for(int start=0;start<seen.Length;start++)
            {
                if(seen[start])continue;seen[start]=true;int sx=start%width,sy=start/width;
                if(pixels[(sy+y0)*tex.width+sx+x0].a<32)continue;
                queue.Enqueue(start);int count=0,left=sx,right=sx,bottom=sy,top=sy;
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%width,y=at/width;count++;
                    left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=width||ny>=height)continue;int next=ny*width+nx;
                        if(seen[next])continue;seen[next]=true;
                        if(pixels[(ny+y0)*tex.width+nx+x0].a>=32)queue.Enqueue(next);
                    }
                }
                if(count>largest){largest=count;result=new Rect(left,bottom,right-left+1,top-bottom+1);}
            }
            return result;
        }
        finally{UnityEngine.Object.DestroyImmediate(tex);}
    }
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string setup=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/CurrentIssuesSetup.request"));
        if(File.Exists(setup)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            string setupOutput=File.ReadAllText(setup).Trim();File.Delete(setup);
            try{Setup();File.WriteAllText(Path.Combine(setupOutput,"原生配置.txt"),"success");}
            catch(Exception e){File.WriteAllText(Path.Combine(setupOutput,"原生配置.txt"),e.ToString());Debug.LogException(e);}return;
        }
        if(!File.Exists(Request))return;
        string output=File.ReadAllText(Request).Trim();File.Delete(Request);var r=new Report();
        try
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();r.scene=scene.name;r.sceneDirty=scene.isDirty;r.playing=EditorApplication.isPlaying;
            var art=Resources.Load<CampaignActorArt>("CampaignActors/ActorArt");
            var sprites=new List<Sprite>();
            foreach(string name in new[]{"Soldier","Leader"})sprites.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/enemy/"+name+".prefab").GetComponent<SpriteRenderer>().sprite);
            var lib=Resources.Load<EnemyAnimationLibrary>(EnemyAnimationLibrary.ResourcePath);sprites.AddRange(lib.alignedSoldierWalk.Take(1));
            foreach(var list in new[]{art.soldierHit,art.soldierDeath,art.grenadierHit,art.grenadierDeath,art.grenadierWindup})sprites.AddRange(list);
            foreach(var s in sprites.Where(s=>s!=null))r.frames.Add(new Frame{name=s.name,path=AssetDatabase.GetAssetPath(s),size=s.rect.size,pivot=s.pivot,ppu=s.pixelsPerUnit,opaque=Opaque(s),body=Body(s)});
            var c=Resources.Load<CampaignCinematics>("Campaign/Cinematics");foreach(var route in c.routes)r.routes.Add(route.chapter+":"+route.key+":"+route.ending+":"+string.Join(",",route.shots.Select(s=>s.image.name+"="+s.Duration+";cue="+s.triggerHopeFinale)));
            var d=Resources.Load<GameObject>("Campaign/CampaignRoot").GetComponent<StoryDirector>();r.music=new[]{d.chapterMusic?.name,d.mourningMusic?.name,d.finaleMusic?.name};r.success=true;
        }
        catch(Exception e){r.error=e.ToString();}
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"现场检查.json"),JsonUtility.ToJson(r,true));
    }
    static float FootX(Sprite sprite)
    {
        var bounds=Opaque(sprite);var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));var pixels=tex.GetPixels32();double sum=0;int count=0;
            for(int y=(int)bounds.y;y<bounds.y+3;y++)for(int x=0;x<sprite.rect.width;x++)
                if(pixels[(y+(int)sprite.rect.y)*tex.width+x+(int)sprite.rect.x].a>=32){sum+=x+.5;count++;}
            return count>0?(float)(sum/count):sprite.pivot.x;
        }
        finally{UnityEngine.Object.DestroyImmediate(tex);}
    }
    static void Align(Sprite[] frames,Sprite reference)
    {
        var original=Opaque(reference);var first=Opaque(frames[0]);
        float height=original.height/reference.pixelsPerUnit;
        float ground=(original.y-reference.pivot.y)/reference.pixelsPerUnit;
        float center=(FootX(reference)-reference.pivot.x)/reference.pixelsPerUnit;
        float ppu=first.height/height,anchor=FootX(frames[0])-center*ppu;
        foreach(var sprite in frames)
        {
            var bounds=Opaque(sprite);string path=AssetDatabase.GetAssetPath(sprite);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);Undo.RecordObject(importer,"对齐敌人动作大小与脚底");
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;
            settings.spritePivot=new Vector2(anchor/sprite.rect.width,(bounds.y-ground*ppu)/sprite.rect.height);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.spritePixelsPerUnit=ppu;
            importer.SaveAndReimport();
        }
    }
    static void Setup()
    {
        var art=Resources.Load<CampaignActorArt>("CampaignActors/ActorArt");
        var soldier=Resources.Load<GameObject>("enemy/Soldier").GetComponent<SpriteRenderer>().sprite;
        var grenadier=Resources.Load<GameObject>("enemy/Leader").GetComponent<SpriteRenderer>().sprite;
        Align(art.soldierHit,soldier);Align(art.soldierDeath,soldier);
        Align(art.grenadierHit,grenadier);Align(art.grenadierDeath,grenadier);Align(art.grenadierWindup,grenadier);
        var table=Resources.Load<ChapterTable>("Campaign/ChapterTable");var owner=Resources.Load<GameObject>("Campaign/CampaignRoot").GetComponent<StoryDirector>();
        Undo.RecordObject(table,"补齐章节开头配乐");
        foreach(var chapter in table.chapters)if(chapter.number<=2&&chapter.openingMusic==null)chapter.openingMusic=owner.chapterMusic;
        var cinematics=Resources.Load<CampaignCinematics>("Campaign/Cinematics");cinematics.Validate();EditorUtility.SetDirty(cinematics);
        EditorUtility.SetDirty(table);table.Validate();AssetDatabase.SaveAssets();
    }
}
#endif
