#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyBodyImpactPlayVerification
{
    const string Pending="1832.EnemyBodyImpact.Play";
    static int previousFrame=-1;static double lastAdvance;
    static string Request=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/EnemyBodyImpactPlay.request"));
    static EnemyBodyImpactPlayVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Request)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var output=File.ReadAllText(Request).Trim();File.Delete(Request);SessionState.SetString(Pending,output);SessionState.SetBool(Pending+".active",true);
            previousFrame=-1;lastAdvance=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Pending+".active",false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(Time.frameCount!=previousFrame){previousFrame=Time.frameCount;lastAdvance=EditorApplication.timeSinceStartup;}
        if(EditorApplication.timeSinceStartup-lastAdvance>1){EditorApplication.isPaused=true;EditorApplication.Step();}
        string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;
        SessionState.EraseString(Pending);Application.runInBackground=true;
        var story=StoryDirector.Current;
        if(story==null)story=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Campaign/CampaignRoot")).GetComponent<StoryDirector>();
        story.gameObject.AddComponent<EnemyBodyImpactPlayProbe>().Begin(path);
    }
    public static void Finished(){SessionState.EraseBool(Pending+".active");EditorApplication.isPlaying=false;}
}
public sealed class EnemyBodyImpactPlayProbe : MonoBehaviour
{
    [Serializable] sealed class Report {public bool success,framesAdvanced;public int checks,startFrame,endFrame;public string error;public string[] passed;public string coverage="Actual gameplay collision callbacks, sprite rendering and pixel comparisons; no physical hardware input injection. Original scene not saved or replaced.";}
    readonly List<string> passed=new List<string>();Report report;string output;StoryDirector story;float listenerVolume;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    CampaignCinematicView Film=>story.GetComponent<CampaignCinematicView>();
    T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    void Check(bool ok,string text){if(!ok)throw new Exception(text);passed.Add(text);}
    public void Begin(string path){output=path;story=GetComponent<StoryDirector>();report=new Report{startFrame=Time.frameCount};listenerVolume=AudioListener.volume;AudioListener.volume=0;StartCoroutine(Guard(Test()));}
    IEnumerator Guard(IEnumerator test)
    {
        var stack=new Stack<IEnumerator>();stack.Push(test);
        while(stack.Count>0)
        {
            object current=null;bool more=false;Exception error=null;
            try{var top=stack.Peek();more=top.MoveNext();if(more)current=top.Current;else stack.Pop();}catch(Exception e){error=e;}
            if(error!=null){report.error=error.ToString();break;}if(!more)continue;if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
        }
        foreach(var it in stack)(it as IDisposable)?.Dispose();AudioListener.volume=listenerVolume;AudioListener.pause=false;Time.timeScale=1;
        report.endFrame=Time.frameCount;report.framesAdvanced=report.endFrame>report.startFrame;report.success=string.IsNullOrEmpty(report.error);report.checks=passed.Count;report.passed=passed.ToArray();File.WriteAllText(Path.Combine(output,"角色撞垒Play检查.json"),JsonUtility.ToJson(report,true));EnemyBodyImpactPlayVerification.Finished();
    }
    IEnumerator Until(Func<bool> ready,bool skipCards=false)
    {
        for(int i=0;i<600;i++){if(ready())yield break;if(skipCards)story.DebugSkip();yield return null;}throw new TimeoutException("Play check waiting for "+story.Phase);
    }
    IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    Color[] Capture(Camera camera,string name)
    {
        var texture=new RenderTexture(640,360,24);texture.Create();var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);
        try{camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels();}
        finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;texture.Release();Destroy(texture);Destroy(pixels);}
    }
    IEnumerator Body(string role)
    {
        var camera=Camera.main;GameManager.WallHP=GameManager.WallMaxHP;
        var enemy=Instantiate(Resources.Load<GameObject>("enemy/"+role),new Vector3(camera.transform.position.x-2,0,0),Quaternion.identity);enemy.GetComponent<EnemyMove>().waveControlled=true;yield return Frames(2);
        var sprites=enemy.GetComponentsInChildren<SpriteRenderer>();Bounds bounds=new Bounds(enemy.transform.position,Vector3.zero);foreach(var sprite in sprites)if(sprite.enabled&&sprite.sprite!=null)bounds.Encapsulate(sprite.bounds);
        var min=camera.WorldToViewportPoint(bounds.min);var max=camera.WorldToViewportPoint(bounds.max);var region=Rect.MinMaxRect(min.x*640-14,min.y*360-14,max.x*640+14,max.y*360+14);
        var before=Capture(camera,role+"_Before");int hp=GameManager.WallHP;var hit=enemy.GetComponent<WallDamage>();hit.ResolveWallHit();hit.ResolveWallHit();
        int damage=CampaignRevisionRules.ScaleCombatDamage(10,story.Chapter.combatDamageMultiplier);Check(GameManager.WallHP==hp-damage,role+" resolves body damage exactly once");
        var effects=UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None);Check(effects.Length==1&&effects[0].RendererCount>0,role+" creates only its own visible sprite effect");var effect=effects[0];effect.Sample(.5f);
        Check(effect.GetComponentsInChildren<Collider2D>().Length==0&&effect.GetComponentsInChildren<EnemyMove>().Length==0,"Visual copy has no collision, firing or movement gameplay");
        Check(effect.GetComponentsInChildren<SpriteRenderer>().All(r=>r.sharedMaterial.shader.name=="Sprites/EnemyWallImpactBlur"&&r.color.a<1),role+" sprite material applies local blur and fade");
        var after=Capture(camera,role+"_During");double sum=0;int count=0,changed=0;
        for(int i=0;i<before.Length;i++)if(Math.Abs(before[i].r-after[i].r)+Math.Abs(before[i].g-after[i].g)+Math.Abs(before[i].b-after[i].b)>.01)changed++;
        Check(changed>80,role+" local fade visibly changes character pixels");for(int y=0;y<360;y++)for(int x=0;x<640;x++)if(!region.Contains(new Vector2(x,y))){int i=y*640+x;sum+=Math.Abs(before[i].r-after[i].r)+Math.Abs(before[i].g-after[i].g)+Math.Abs(before[i].b-after[i].b);count++;}
        Check(sum/Math.Max(1,count)<.001,role+" effect leaves pixels outside the character region unchanged");
        yield return Frames(2);Check(enemy==null&&effect!=null&&effect.GetComponentsInChildren<SpriteRenderer>().All(r=>r.sprite!=null),role+" visual survives original sprite/rig cleanup");
        float start=Time.unscaledTime;while(Time.unscaledTime-start<.4f)yield return null;Check(UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None).Length==0,role+" visual and temporary materials finish without lingering");
    }
    IEnumerator Test()
    {
        yield return Frames(3);StartCoroutine(story.DebugChapter(3,true));yield return Until(()=>story.Phase==CampaignPhase.Defense&&Camera.main!=null);
        var shader=Resources.Load<Shader>("UI/EnemyWallImpactBlur");Check(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Local sprite blur shader compiles");
        var camera=Camera.main;var retired=camera.gameObject.GetComponent<WallImpactBlur>();if(retired==null)retired=camera.gameObject.AddComponent<WallImpactBlur>();retired.Pulse(1);Check(!retired.enabled&&retired.Strength==0,"Legacy camera blur cannot re-enable or render full-screen effects");
        GameManager.WallHP=GameManager.WallMaxHP;int health=GameManager.WallHP;for(int i=0;i<10;i++)GameManager.DamageWall(GameManager.Kind.Bullet);
        Check(GameManager.WallHP==health-Mathf.FloorToInt(10*GameManager.BulletBaseDamage*story.Chapter.combatDamageMultiplier+0.000001f),"Repeated enemy bullets keep their actual damage");Check(UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None).Length==0&&retired.Strength==0,"Repeated enemy bullets create neither a character blur nor a screen pulse");
        GameManager.WallHP=GameManager.WallMaxHP;GameManager.DamageWall(GameManager.Kind.Bomb);Check(UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None).Length==0&&retired.Strength==0,"Grenades do not trigger any impact blur");
        GameManager.WallHP=GameManager.WallMaxHP;GameManager.DamageWallAmount(150,true);Check(UnityEngine.Object.FindObjectsByType<EnemyWallImpactVisual>(FindObjectsSortMode.None).Length==0&&retired.Strength==0,"Cannon explosions do not trigger any impact blur");
        yield return Body("Soldier");yield return Body("Leader");
    }
}
#endif
