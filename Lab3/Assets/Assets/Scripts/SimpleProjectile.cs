using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    public float life = 3f;

    Vector3 dir;
    float speed;
    Transform ignoreRoot;

    public void InitTowards(Vector3 targetPoint, float spd, int damage, Transform ignore)
    {
        dir = (targetPoint - transform.position).normalized;
        speed = spd;
        ignoreRoot = ignore;
        Destroy(gameObject, life);
    }

    void Update()
    {
        transform.position += dir * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;
        if (ignoreRoot && other.transform.IsChildOf(ignoreRoot)) return;

        Destroy(gameObject);
    }
}