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

    [Header("阶段与节拍接口")]
    public float lateGameTime = 180f;
    public int beatsPerSpawn = 4;
    [Tooltip("投向街垒：在场景中拖 Wall 的 Transform")]
    public Transform target;

    GameObject soldierPrefab, leaderPrefab;
    int beatCount;
    bool wasAtBeat;
    static int nextGroupId = 1;
    static int sceneHandle = int.MinValue;
    static float lastSpawnTime = float.NegativeInfinity;
    static float nextLeaderTime;
    static bool leaderWasBusy;
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

    void CreateEnemy(GameObject prefab, Vector3 position, int id)
    {
        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        EnemyMove movement = go.GetComponent<EnemyMove>();
        if (movement != null) movement.ConfigureSpawn(id, target);
        else Debug.LogWarning("[Spawn] 敌人预制体没有 EnemyMove。");
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
        soldierPrefab = Resources.Load<GameObject>("enemy/Soldier");
        leaderPrefab = Resources.Load<GameObject>("enemy/Leader");
        if (soldierPrefab == null) Debug.LogWarning("[Spawn] 找不到 enemy/Soldier。");
        if (target == null) Debug.LogWarning("[Spawn] 未指定街垒落点，Leader 会跳过投弹。");
        StartCoroutine(SpawnLoop());
    }

    void Update()
    {
        RefreshLeaderGate();
        bool atBeat = TapSec.beattime <= 0f;
        if (atBeat && !wasAtBeat) beatCount++;
        wasAtBeat = atBeat;
    }
}
