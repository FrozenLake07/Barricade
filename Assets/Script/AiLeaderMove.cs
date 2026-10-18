using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AiLeaderMove : MonoBehaviour
{
    Rigidbody2D rb;
    GameObject prefab;


    public float speed=0.5f;
    public Transform target;        // 箭的落点
    public float launchSpeed = 10f; // 出手速度

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        prefab = Resources.Load<GameObject>("arrow/Bomb");
    }

    bool Randoml()
    {
        int t = Random.Range(0, 100);
        if (t <= 40) return true;
        else return false;
    }

    // 低抛解：重力由 Rigidbody 决定，脚本不定义；只算 x 轴距离。
    // 返回 false 表示这个出手速度够不到 target
    bool Aim(Rigidbody2D arrowRb, out float vx, out float vy, out float angle)
    {
        vx = vy = angle = 0f;
        if (target == null) return false;

        float d = Mathf.Abs(target.position.x - transform.position.x);
        float sin2 = d * Mathf.Abs(Physics2D.gravity.y * arrowRb.gravityScale)
                       / (launchSpeed * launchSpeed);       // sin(2θ) = d·g/v²
        if (sin2 > 1f) return false;                        // 超出射程，射不到

        float theta = 0.5f * Mathf.Asin(sin2);              // 低抛角（0~45°）
        vx = launchSpeed * Mathf.Cos(theta);
        vy = launchSpeed * Mathf.Sin(theta);
        if (target.position.x < transform.position.x) vx = -vx;
        angle = theta * Mathf.Rad2Deg;
        return true;
    }

    IEnumerator Ai()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);

            // 概率 + 距离都满足才开火
            if (Randoml() && Aim(prefab.GetComponent<Rigidbody2D>(),
                                 out float vx, out float vy, out float angle))
            {
                rb.velocity = new Vector3(0, 0, 0);
                yield return new WaitForSeconds(0.5f);

                var t = Instantiate(prefab, transform.position,
                                    Quaternion.Euler(0, 0, vx >= 0 ? angle : 180f - angle));
                t.GetComponent<Rigidbody2D>().velocity = new Vector2(vx, vy);

                yield return new WaitForSeconds(1f);
                rb.velocity = new Vector3(speed, 0, 0);
            }
        }

    }
    void Start()
    {
        rb.velocity = new Vector3(speed, 0, 0);
        StartCoroutine(Ai());
    }
}