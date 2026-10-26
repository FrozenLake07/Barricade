using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeaderEnemMove : MonoBehaviour
{
    Rigidbody2D rb;
    GameObject[] prefab; 

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        prefab = Resources.LoadAll<GameObject>("arrow");
    }

    bool Randoml()
    {
        int t = Random.Range(0, 100);
        if (t <= 20) return true;
        else return false;
    }
    IEnumerator Ai()
    {
        while(true)
        {
            yield return new WaitForSeconds(2f);
            if(Randoml())
            {
                rb.velocity = new Vector3(0, 0, 0);
                yield return new WaitForSeconds(0.5f);
                var t = Instantiate(prefab[1], transform.position, Quaternion.Euler(0,0,45));
                yield return new WaitForSeconds(1f);
                rb.velocity = new Vector3(1, 0, 0);
            }
        }
        
    }
    void Start()
    {
        rb.velocity = new Vector3(1, 0, 0);
        StartCoroutine(Ai());
    }
}
