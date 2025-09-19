using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [Header("Pool Setup")]
    public Projectile projectilePrefab;
    public int prewarmCount = 50;
    public bool isAllowExpand = true;
    public Transform container; // optional parent for pooled objects

    private readonly Queue<Projectile> pool = new Queue<Projectile>();

    private void Awake()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("ProjectilePool: projectilePrefab is not assigned.");
        }

        if (container == null)
        {
            container = this.transform;
        }

        Prewarm();
    }

    private void Prewarm()
    {
        if (projectilePrefab == null)
        {
            return;
        }

        for (int i = 0; i < prewarmCount; i++)
        {
            Projectile p = Instantiate(projectilePrefab, container);
            p.gameObject.SetActive(false);
            pool.Enqueue(p);
        }
    }

    public Projectile Get()
    {
        if (pool.Count > 0)
        {
            Projectile p = pool.Dequeue();
            if (p != null)
            {
                p.gameObject.SetActive(true);
                return p;
            }
        }

        if (isAllowExpand)
        {
            if (projectilePrefab != null)
            {
                Projectile p = Instantiate(projectilePrefab, container);
                p.gameObject.SetActive(true);
                return p;
            }
        }

        return null;
    }

    public void Release(Projectile p)
    {
        if (p == null)
        {
            return;
        }

        p.PrepareForPoolReturn();
        p.transform.SetParent(container);

        if (p.gameObject.activeSelf)
        {
            p.gameObject.SetActive(false);
        }

        pool.Enqueue(p);
    }

    public void ClearAndDestroyAll()
    {
        while (pool.Count > 0)
        {
            Projectile p = pool.Dequeue();
            if (p != null)
            {
                Destroy(p.gameObject);
            }
        }
    }
}
