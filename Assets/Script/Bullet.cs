using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 15f;
    public float lifetime = 3f;
    public int damage = 1;

    private GameObject shooter;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Bullet 缺少 Rigidbody2D！", this);
            Destroy(gameObject);
            return;
        }
        Destroy(gameObject, lifetime);
    }

    public void SetVelocity(Vector2 direction)
    {
        if (rb == null)
        {
            Debug.LogError("Bullet.rb 為 null！無法設定速度。", this);
            return;
        }
        rb.velocity = direction.normalized * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && other.gameObject != gameObject)
        {
            if (other.CompareTag("Player") && other.gameObject == shooter)
            {
                return; 
            }

            if (other.CompareTag("Player"))
            {
                PlayerController health = other.GetComponent<PlayerController>();
                if (health != null)
                    health.TakeDamage(damage);

                Destroy(gameObject);
            }
        }
    }

    public void SetShooter(GameObject owner)
    {
        shooter = owner;
    }
}