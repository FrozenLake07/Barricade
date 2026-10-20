using System.Collections;
using UnityEngine;

public class BombHP : MonoBehaviour
{
    public int HP = 100;
    public float explosionDuration = 0.3f;
    public float explosionScale = 6f;
    [HideInInspector] public int groupId;
    [HideInInspector] public float trackY;
    bool hasTrack, exploded;
    public bool IsAlive => !exploded && HP > 0;
    public float AttackTrackY => hasTrack ? trackY : transform.position.y;

    public void Configure(int id, float laneY)
    {
        groupId = id;
        trackY = laneY;
        hasTrack = true;
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive) return;
        HP -= damage;
        if (HP <= 0) Explode(false);
    }

    public void Explode(bool hitWall)
    {
        if (exploded) return;
        // 先禁用整组碰撞，再结算触发者，避免同帧三枚撞墙扣三次血。
        BombHP[] bombs = FindObjectsByType<BombHP>(FindObjectsSortMode.None);
        BeginExplosion();
        SfxPlayer.Play("SFX-BMB-04", 0.8f);
        int chained = 0;
        foreach (BombHP bomb in bombs)
            if (bomb != null && bomb != this && bomb.IsAlive && groupId > 0 && bomb.groupId == groupId)
            {
                bomb.BeginExplosion();
                chained++;
            }
        if (chained > 0) SfxPlayer.Play("SFX-BMB-05", 0.5f);
        if (hitWall){GetComponent<WaveThreat>()?.RecordImpact();GameManager.DamageWall(GameManager.Kind.Bomb);}
    }

    void BeginExplosion()
    {
        if (exploded) return;
        exploded = true;
        HP = 0;
        foreach (Collider2D collider in GetComponents<Collider2D>()) collider.enabled = false;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null) { body.velocity = Vector2.zero; body.simulated = false; }
        // 复用手榴弹自身的图形做扩散淡出，不需要额外 GameObject 或爆炸预制体。
        StartCoroutine(ExplosionVisual());
    }

    IEnumerator ExplosionVisual()
    {
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        Vector3 initialScale = transform.localScale;
        float duration = Mathf.Max(0.05f, explosionDuration);
        float elapsed = 0f;
        if (sprite != null) { sprite.color = new Color(1f, 0.55f, 0.08f, 1f); sprite.sortingOrder += 1; }
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            transform.localScale = initialScale * Mathf.Lerp(1f, Mathf.Max(1f, explosionScale), progress);
            if (sprite != null) sprite.color = new Color(1f, 0.55f, 0.08f, 1f - progress);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    void Update()
    {
        if (!exploded && HP <= 0) Explode(false);
    }
}
