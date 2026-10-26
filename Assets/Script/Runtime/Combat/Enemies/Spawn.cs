using System.Collections;
using UnityEngine;

public class Spawn : MonoBehaviour
{
    public enum SpawnMode { Time, Music }
    public SpawnMode mode = SpawnMode.Time;

    [Header("单兵生成")]
    public float interval = 1.5f;
    public bool randomStartDelay = true;
    public float intervalJitter = 0.3f;
    [Range(0f, 100f)] public float baseSpawnRate = 30f;
    public float rateGrowthPerMinute = 2f;
    [Range(0f, 100f)] public float maxSpawnRate = 38f;
    [Tooltip("三个生成点共用的在场敌人上限，包含已经投完弹的 Leader")]
    public int maxActiveEnemies = 12;
    [Tooltip("三个生成点之间的最短刷出间隔，避免同帧堆兵")]
    public float globalSpawnGap = 0.65f;

    [Header("Leader 三人组")]
    [Range(0, 100)] public int leaderChance = 12;
    public float leaderUnlockTime = 45f;
    [Tooltip("上一组恢复行进或全部被击杀后，再等多久允许下一组")]
    public float leaderCooldown = 18f;
    public float leaderSpacing = 0.9f;

    [Header("终局火炮（全场一门；三个Spawn保持相同解锁时间）")]
    public bool enableCannon = true;
    [Tooltip("按策划：第二幕开始28分钟后部署；测试可改为较小秒数")]
    [Min(0f)] public float cannonUnlockTime = 28f * 60f;
    [Tooltip("相对镜头左边缘的初始偏移，负值从画面外入场；最终由炮车可见比例限制行程")]
    public float cannonScreenInset = -.9f;
    [Tooltip("炮车根节点是脚底，与现有士兵的轨道脚底对齐")]
    public float cannonFootOffset = -0.59375f;

    [Header("阶段与节拍接口")]
    public float lateGameTime = 180f;
    public int beatsPerSpawn = 4;
    [Tooltip("投向街垒：在场景中拖 Wall 的 Transform")]
    public Transform target;

    GameObject soldierPrefab, leaderPrefab, cannonPrefab;
    int beatCount;
    bool wasAtBeat;
    static int nextGroupId = 1;
    static int sceneHandle = int.MinValue;
    static float lastSpawnTime = float.NegativeInfinity;
    static float nextLeaderTime;
    static bool leaderWasBusy;
    static bool cannonDeployed, cannonWarning;
    const int LeaderCount = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRun()
    {
        nextGroupId = 1;
        sceneHandle = int.MinValue;
        ResetSceneTimers();
    }

    static void ResetSceneTimers()
    {
        lastSpawnTime = float.NegativeInfinity;
        nextLeaderTime = 0f;
        leaderWasBusy = false;
        cannonDeployed = false;
        cannonWarning = false;
    }

    void Awake()
    {
        // 场景重载时局部时间归零，静态截止时间也要清掉；同场景新加生成点不重置。
        int currentScene = gameObject.scene.handle;
        if (sceneHandle == currentScene) return;
        sceneHandle = currentScene;
        ResetSceneTimers();
    }
    public static int NewGroupId() => nextGroupId++;
    protected virtual float SpawnRate() => Mathf.Clamp(
        baseSpawnRate + rateGrowthPerMinute * (Time.timeSinceLevelLoad / 60f),
        0f, maxSpawnRate);
    protected virtual bool LeaderAllowed() =>
        leaderUnlockTime <= 0f || Time.timeSinceLevelLoad >= leaderUnlockTime;
    protected virtual bool IsLateGame() =>
        lateGameTime <= 0f || Time.timeSinceLevelLoad >= lateGameTime;
    protected virtual int CurrentBeat() => beatCount;

    void RefreshLeaderGate()
    {
        bool busy = EnemyMove.LeaderBusy;
        // 三个 Spawn 共享这次状态变化；被提前击杀也不能留下永久锁。
        if (leaderWasBusy && !busy)
            nextLeaderTime = Time.timeSinceLevelLoad + Mathf.Max(0f, leaderCooldown);
        leaderWasBusy = busy;
    }

    IEnumerator WaitBeats(int count)
    {
        int beatTarget = CurrentBeat() + count;
        float timeout = Time.timeSinceLevelLoad + count * 3f;
        while (CurrentBeat() < beatTarget && Time.timeSinceLevelLoad < timeout) yield return null;
    }

    int ActiveEnemyCount()
    {
        int count = 0;
        foreach (EnemyHP enemy in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))
            if (enemy != null && enemy.HP > 0) count++;
        return count;
    }

    public float WaveSpeed
    {
        get {var source=Resources.Load<GameObject>("enemy/Soldier");var move=source!=null?source.GetComponent<EnemyMove>():null;return move!=null?move.speed:.5f;}
    }
    // 调度与节流由WaveDirector负责；此入口只执行一个原子批次。
    public float WaveEntryX(bool allowOnscreenOpening,float advance=0)
    {
        float x=transform.position.x;
        if(allowOnscreenOpening)return x+Mathf.Clamp(advance,0,10);
        var camera=Camera.main;
        if(camera!=null&&camera.orthographic)
            x=camera.transform.position.x-camera.orthographicSize*camera.aspect-1.25f;
        return x;
    }
    public GameObject[] SpawnAt(WaveEventType type,float advance,int count=1,bool allowOnscreenOpening=false)
    {
        var source=Resources.Load<GameObject>(type==WaveEventType.Squad?"enemy/Leader":"enemy/Soldier");
        if(source==null)return new GameObject[0];
        int id=NewGroupId();var result=new GameObject[Mathf.Max(1,count)];
        for(int i=0;i<result.Length;i++)
        {
            Vector3 position=new Vector3(WaveEntryX(allowOnscreenOpening,advance)-i*.9f,transform.position.y,transform.position.z);
            result[i]=Instantiate(source,position,Quaternion.identity);
            var move=result[i].GetComponent<EnemyMove>();
            move.waveControlled=true;move.ConfigureSpawn(id,target);
        }
        return result;
    }

    void CreateEnemy(GameObject prefab, Vector3 position, int id)
    {
        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        EnemyMove movement = go.GetComponent<EnemyMove>();
        if (movement != null) movement.ConfigureSpawn(id, target);
        else Debug.LogWarning("[Spawn] 敌人预制体没有 EnemyMove。");
    }

    [ContextMenu("立即部署终局火炮（运行中，全场一门）")]
    public void DeployCannonNow()
    {
        if (Application.isPlaying && isActiveAndEnabled && gameObject.scene.name == CampaignSceneNames.Defense)
            TryDeployCannon(true);
    }

    void TryDeployCannon(bool immediate = false)
    {
        if (cannonDeployed || gameObject.scene.name != CampaignSceneNames.Defense) return;
        if (cannonPrefab == null) cannonPrefab = Resources.Load<GameObject>("enemy/Cannon");
        if (!immediate && (!enableCannon || Time.timeSinceLevelLoad < Mathf.Max(0f, cannonUnlockTime))) return;
        // 手动放入场景的炮车也算一门，不因到了解锁时刻再叠一门。
        foreach (var existing in FindObjectsByType<Cannon>(FindObjectsSortMode.None))
            if (existing.gameObject.scene == gameObject.scene)
            { cannonDeployed = true; return; }
        if (cannonPrefab == null || cannonPrefab.GetComponent<Cannon>() == null)
        {
            if (!cannonWarning) Debug.LogWarning("[Spawn] 缺少有效的enemy/Cannon预制体，无法部署终局火炮。", this);
            cannonWarning = true;
            return;
        }
        var lanes = new System.Collections.Generic.List<Spawn>();
        foreach (var spawn in FindObjectsByType<Spawn>(FindObjectsSortMode.None))
            if (spawn.isActiveAndEnabled && spawn.gameObject.scene == gameObject.scene) lanes.Add(spawn);
        lanes.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        Spawn middle = lanes.Count > 0 ? lanes[lanes.Count / 2] : this;
        Collider2D wall = middle.target != null ? middle.target.GetComponent<Collider2D>() : null;
        if (wall == null || !wall.enabled || !GameManager.IsWall(wall))
        {
            wall = null;
            foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (collider.enabled && collider.gameObject.scene == gameObject.scene && GameManager.IsWall(collider))
                { wall = collider; break; }
        }
        if (wall == null)
        {
            if (!cannonWarning) Debug.LogWarning("[Spawn] 未找到场景2街垒，等待后再部署火炮。", this);
            cannonWarning = true;
            return;
        }
        Vector3 position = middle.transform.position;
        Camera camera = Camera.main;
        if (camera != null && camera.orthographic && camera.gameObject.scene == gameObject.scene)
            position.x = Mathf.Max(position.x, camera.transform.position.x - camera.orthographicSize * camera.aspect
                + Mathf.Clamp(middle.cannonScreenInset, -2f, 0f));
        position.y += middle.cannonFootOffset;
        position.z = 0f;
        // 先占共享名额；Instantiate的Awake/OnEnable可能触发其他脚本。
        cannonDeployed = true;
        GameObject instance = Instantiate(cannonPrefab, position, Quaternion.identity);
        instance.GetComponent<Cannon>().ConfigureSpawn(wall);
        Debug.Log(StoryDirector.Active?"[Spawn] 火炮已部署，由波次表控制炮击。":"[Spawn] 终局火炮已部署，首发将在30秒后发射。", instance);
    }

    void SpawnOneOrSquad()
    {
        RefreshLeaderGate();
        if (Time.timeSinceLevelLoad - lastSpawnTime < Mathf.Max(0f, globalSpawnGap)) return;
        int alive = ActiveEnemyCount();
        if (alive >= Mathf.Max(1, maxActiveEnemies)) return;

        bool leader = leaderPrefab != null && LeaderAllowed() && !EnemyMove.LeaderBusy
            && Time.timeSinceLevelLoad >= nextLeaderTime && alive + LeaderCount <= maxActiveEnemies
            && Random.Range(0, 100) < leaderChance;
        int id = NewGroupId();
        if (leader)
        {
            // 同一帧生成，横向错开站位；三人共用投弹状态与全局刷出锁。
            for (int i = 0; i < LeaderCount; i++)
                CreateEnemy(leaderPrefab, transform.position + Vector3.left * (i * leaderSpacing), id);
            SfxPlayer.Play("SFX-ENM-07", 0.5f);
            leaderWasBusy = EnemyMove.LeaderBusy;
        }
        else if (soldierPrefab != null)
            CreateEnemy(soldierPrefab, transform.position, id);
        lastSpawnTime = Time.timeSinceLevelLoad;
    }

    IEnumerator SpawnLoop()
    {
        if (randomStartDelay)
            yield return new WaitForSeconds(Random.Range(0f, Mathf.Max(0.1f, interval)));
        while (true)
        {
            if (mode == SpawnMode.Music) yield return WaitBeats(Mathf.Max(1, beatsPerSpawn));
            else yield return new WaitForSeconds(Mathf.Max(0.1f,
                interval + Random.Range(-intervalJitter, intervalJitter)));
            if (Random.Range(0f, 100f) < SpawnRate()) SpawnOneOrSquad();
        }
    }

    void Start()
    {
        if (StoryDirector.Active) return;
        soldierPrefab = Resources.Load<GameObject>("enemy/Soldier");
        leaderPrefab = Resources.Load<GameObject>("enemy/Leader");
        cannonPrefab = Resources.Load<GameObject>("enemy/Cannon");
        if (soldierPrefab == null) Debug.LogWarning("[Spawn] 找不到 enemy/Soldier。");
        if (target == null) Debug.LogWarning("[Spawn] 未指定街垒落点，Leader 会跳过投弹。");
        StartCoroutine(SpawnLoop());
    }

    void Update()
    {
        if (StoryDirector.Active) return;
        TryDeployCannon();
        RefreshLeaderGate();
        bool atBeat = TapSec.beattime <= 0f;
        if (atBeat && !wasAtBeat) beatCount++;
        wasAtBeat = atBeat;
    }
}

