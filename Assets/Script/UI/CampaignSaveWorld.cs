using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rebuild runtime combat objects from the project's existing prefabs, then reconnect references.
public sealed class CampaignSaveWorld
{
    public static readonly Type[] Types={typeof(HeroActor),typeof(HeroConstructionDriver),typeof(Tap),typeof(TapSec),typeof(SBullet),typeof(CapsuleTrack),
        typeof(EnemyMove),typeof(EnemyHP),typeof(ArrowDefinition),typeof(BulletHit),typeof(BombHP),typeof(WallDamage),typeof(Cannon),typeof(CannonShell),
        typeof(WaveThreat),typeof(WaveSquad),typeof(CampaignActor),typeof(WaveExitVisual),typeof(CampaignProjectile)};
    [Serializable] public sealed class ComponentData
    {public string path,type;public bool enabled;public List<CampaignStateCodec.Field> fields;public List<string> hitTargets;}
    [Serializable] public sealed class Parameter {public int hash,type,integer;public float number;public bool boolean;}
    [Serializable] public sealed class Node
    {
        public string path;public Vector3 position,scale;public Quaternion rotation;
        public bool hasSprite,spriteEnabled,flipX,flipY,hasBody,simulated,hasAnimator,animatorEnabled;
        public Color color;public Vector2 velocity;public float angularVelocity,gravity,animationTime,animationSpeed;
        public int animationState;public List<Parameter> parameters;
    }
    [Serializable] public sealed class Entity
    {public string key,factory,name;public Vector3 position,scale;public Quaternion rotation;}
    [Serializable] public sealed class Data
    {public List<Entity> entities=new List<Entity>();public List<ComponentData> components=new List<ComponentData>();public List<Node> nodes=new List<Node>();}
    readonly Dictionary<GameObject,string> roots=new Dictionary<GameObject,string>();
    readonly Dictionary<string,GameObject> restored=new Dictionary<string,GameObject>();
    public Data Saved {get;private set;}
    static string Factory(GameObject go)
    {
        var actor=go.GetComponent<CampaignActor>();if(actor!=null)return actor.escort?"@escort":"@decoy";
        if(go.GetComponent<WaveSquad>()!=null)return "@squad";
        if(go.GetComponent<CannonShell>()!=null)return "@shell";
        if(go.GetComponent<Cannon>()!=null)return "enemy/Cannon";
        var move=go.GetComponent<EnemyMove>();if(move!=null)return move.role==EnemyMove.Role.Leader?"enemy/Leader":"enemy/Soldier";
        if(go.GetComponent<BombHP>()!=null)return "arrow/Bomb";
        if(go.GetComponent<ArrowDefinition>()!=null)return "arrow/Bullet";
        return null;
    }
    static bool Alive(GameObject go)
    {
        var actor=go.GetComponent<CampaignActor>();if(actor!=null&&actor.IsResolved)return false;
        var hp=go.GetComponent<EnemyHP>();if(hp!=null&&hp.HP<=0)return false;
        var bomb=go.GetComponent<BombHP>();if(bomb!=null&&!bomb.IsAlive)return false;
        var shell=go.GetComponent<CannonShell>();if(shell!=null&&!shell.IsAlive)return false;
        return go.activeInHierarchy;
    }
    public Data Capture()
    {
        Saved=new Data();
        var scene=SceneManager.GetActiveScene();
        var components=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c!=null&&(c.gameObject.scene==scene||c is WaveSquad)&&Types.Contains(c.GetType())).ToArray();
        foreach(var component in components)
        {
            var go=component.gameObject;var factory=Factory(go);
            if(factory==null||roots.ContainsKey(go)||!Alive(go))continue;
            string key="e"+Saved.entities.Count;roots[go]=key;
            Saved.entities.Add(new Entity{key=key,factory=factory,name=go.name,position=go.transform.position,rotation=go.transform.rotation,scale=go.transform.localScale});
        }
        var nodes=new HashSet<string>();
        foreach(var component in components)
        {
            // Dead units and spent effects have no gameplay state to resurrect.
            if(Factory(component.gameObject)!=null&&!roots.ContainsKey(component.gameObject))continue;
            Saved.components.Add(new ComponentData{path=Reference(component.gameObject),type=component.GetType().FullName,enabled=component.enabled,
                fields=CampaignStateCodec.Capture(component,Reference),hitTargets=component is BulletHit hit?hit.CaptureSaveTargets(Reference):null});
            foreach(var transform in component.GetComponentsInChildren<Transform>(true))
            {
                string path=Reference(transform.gameObject);if(!nodes.Add(path))continue;
                var sprite=transform.GetComponent<SpriteRenderer>();var body=transform.GetComponent<Rigidbody2D>();var animator=transform.GetComponent<Animator>();
                var node=new Node{path=path,position=transform.localPosition,rotation=transform.localRotation,scale=transform.localScale,
                    hasSprite=sprite!=null,hasBody=body!=null,hasAnimator=animator!=null};
                if(sprite!=null){node.spriteEnabled=sprite.enabled;node.color=sprite.color;node.flipX=sprite.flipX;node.flipY=sprite.flipY;}
                if(body!=null){node.velocity=body.velocity;node.angularVelocity=body.angularVelocity;node.gravity=body.gravityScale;node.simulated=body.simulated;}
                if(animator!=null&&animator.runtimeAnimatorController!=null)
                {
                    var state=animator.GetCurrentAnimatorStateInfo(0);node.animationState=state.fullPathHash;node.animationTime=state.normalizedTime;
                    node.animationSpeed=animator.speed;node.animatorEnabled=animator.enabled;node.parameters=new List<Parameter>();
                    foreach(var parameter in animator.parameters)
                    {
                        var item=new Parameter{hash=parameter.nameHash,type=(int)parameter.type};
                        if(parameter.type==AnimatorControllerParameterType.Int)item.integer=animator.GetInteger(parameter.nameHash);
                        else if(parameter.type==AnimatorControllerParameterType.Float)item.number=animator.GetFloat(parameter.nameHash);
                        else if(parameter.type==AnimatorControllerParameterType.Bool)item.boolean=animator.GetBool(parameter.nameHash);
                        node.parameters.Add(item);
                    }
                }
                Saved.nodes.Add(node);
            }
        }
        return Saved;
    }
    public string Reference(UnityEngine.Object value)
    {
        if(value==null)return "";
        var go=value as GameObject;var component=value as Component;if(component!=null)go=component.gameObject;
        if(go==null||!go.scene.IsValid())return null; // External assets remain the prefab's original references.
        var cursor=go.transform;var suffix="";string key=null;
        while(cursor!=null)
        {
            if(roots.TryGetValue(cursor.gameObject,out key)||restored.Any(pair=>pair.Value==cursor.gameObject))
            {
                if(key==null)key=restored.First(pair=>pair.Value==cursor.gameObject).Key;
                break;
            }
            if(StoryDirector.Current!=null&&cursor.gameObject==StoryDirector.Current.gameObject){key="d";break;}
            if(cursor.parent==null){key="s"+cursor.GetSiblingIndex();break;}
            suffix="/"+cursor.GetSiblingIndex()+suffix;cursor=cursor.parent;
        }
        if(key==null)return null;
        string path=key+suffix;
        if(component!=null)
        {
            var same=go.GetComponents(component.GetType());int index=Array.IndexOf(same,component);
            path+="#"+component.GetType().FullName+":"+index;
        }
        return path;
    }
    public UnityEngine.Object Resolve(string key)
    {
        if(string.IsNullOrEmpty(key))return null;
        string[] pieces=key.Split('#');string[] path=pieces[0].Split('/');GameObject go;
        if(path[0]=="d")go=StoryDirector.Current.gameObject;
        else if(path[0].StartsWith("e")){if(!restored.TryGetValue(path[0],out go))return null;}
        else
        {
            int index=int.Parse(path[0].Substring(1));var sceneRoots=SceneManager.GetActiveScene().GetRootGameObjects();
            go=sceneRoots.FirstOrDefault(root=>root.transform.GetSiblingIndex()==index);if(go==null)return null;
        }
        for(int i=1;i<path.Length;i++)
        {int index=int.Parse(path[i]);if(index<0||index>=go.transform.childCount)return null;go=go.transform.GetChild(index).gameObject;}
        if(pieces.Length==1)return go;
        int colon=pieces[1].LastIndexOf(':');string type=pieces[1].Substring(0,colon);int ordinal=int.Parse(pieces[1].Substring(colon+1));
        return go.GetComponents<Component>().Where(c=>c!=null&&c.GetType().FullName==type).Skip(ordinal).FirstOrDefault();
    }
    public void Create(Data data)
    {
        Saved=data;
        foreach(var entity in data.entities)
        {
            GameObject go;
            if(entity.factory=="@escort"||entity.factory=="@decoy")
            {
                bool escort=entity.factory=="@escort";go=new GameObject(entity.name);
                var sprite=go.AddComponent<SpriteRenderer>();sprite.sortingOrder=7;
                sprite.sprite=escort?Resources.Load<Sprite>("HeroAnimations/Frames/Walk/Hero_Walk_01"):Resources.Load<GameObject>("enemy/Leader").GetComponent<SpriteRenderer>().sprite;
                var box=go.AddComponent<BoxCollider2D>();box.isTrigger=true;box.size=new Vector2(.65f,1.5f);
                go.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
            }
            else if(entity.factory=="@squad")go=new GameObject(entity.name);
            else
            {
                GameObject prefab=entity.factory=="@shell"?Resources.Load<GameObject>("enemy/Cannon").GetComponent<Cannon>().shellPrefab.gameObject:Resources.Load<GameObject>(entity.factory);
                if(prefab==null)throw new InvalidOperationException("存档需要的资源缺失："+entity.factory);
                go=UnityEngine.Object.Instantiate(prefab,entity.position,entity.rotation);
                var move=go.GetComponent<EnemyMove>();if(move!=null)move.waveControlled=true;
            }
            go.name=entity.name;go.transform.SetPositionAndRotation(entity.position,entity.rotation);go.transform.localScale=entity.scale;
            restored[entity.key]=go;
        }
        foreach(var dataComponent in data.components)
        {
            Type type=Types.FirstOrDefault(candidate=>candidate.FullName==dataComponent.type);
            if(type==null)throw new InvalidOperationException("不支持的存档组件："+dataComponent.type);
            var go=Resolve(dataComponent.path) as GameObject;
            if(go==null)continue;
            if(go.GetComponent(type)==null)go.AddComponent(type);
        }
    }
    public void Apply(float sceneOffset,float gameOffset,float unscaledOffset)
    {
        // Ensure the actor's pure state machine exists before restoring its nested fields.
        foreach(var actor in UnityEngine.Object.FindObjectsByType<HeroActor>(FindObjectsSortMode.None)){var state=actor.State;}
        foreach(var data in Saved.components)
        {
            var go=Resolve(data.path) as GameObject;if(go==null)continue;
            Type type=Types.First(candidate=>candidate.FullName==data.type);var component=go.GetComponent(type) as MonoBehaviour;
            component.enabled=data.enabled;
            CampaignStateCodec.Restore(component,data.fields,Resolve,sceneOffset,gameOffset,unscaledOffset);
            if(component is BulletHit hit)hit.RestoreSaveTargets(data.hitTargets,Resolve);
            if(component is CampaignActor actor&&actor.escort)CampaignActor.ActiveEscort=actor;
        }
        foreach(var node in Saved.nodes)
        {
            var go=Resolve(node.path) as GameObject;if(go==null)continue;var transform=go.transform;
            transform.localPosition=node.position;transform.localRotation=node.rotation;transform.localScale=node.scale;
            var sprite=go.GetComponent<SpriteRenderer>();if(node.hasSprite&&sprite!=null)
            {sprite.enabled=node.spriteEnabled;sprite.color=node.color;sprite.flipX=node.flipX;sprite.flipY=node.flipY;}
            var body=go.GetComponent<Rigidbody2D>();if(node.hasBody&&body!=null)
            {body.velocity=node.velocity;body.angularVelocity=node.angularVelocity;body.gravityScale=node.gravity;body.simulated=node.simulated;}
            var animator=go.GetComponent<Animator>();if(node.hasAnimator&&animator!=null&&animator.runtimeAnimatorController!=null)
            {
                animator.enabled=true;animator.speed=node.animationSpeed;
                if(node.parameters!=null)foreach(var parameter in node.parameters)
                {
                    var kind=(AnimatorControllerParameterType)parameter.type;
                    if(kind==AnimatorControllerParameterType.Int)animator.SetInteger(parameter.hash,parameter.integer);
                    else if(kind==AnimatorControllerParameterType.Float)animator.SetFloat(parameter.hash,parameter.number);
                    else if(kind==AnimatorControllerParameterType.Bool)animator.SetBool(parameter.hash,parameter.boolean);
                }
                if(node.animationState!=0){animator.Play(node.animationState,0,node.animationTime);animator.Update(0);}
                animator.enabled=node.animatorEnabled;
            }
        }
        Physics2D.SyncTransforms();
    }
}
