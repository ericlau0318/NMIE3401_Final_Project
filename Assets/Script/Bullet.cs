using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 15f;
    public float lifetime = 3f;
    public int damage = 1;
    
    private Rigidbody2D rb;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Destroy(gameObject);
            return;
        }
        Destroy(gameObject, lifetime);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SetVelocity(Vector2 direction)
    {
        if (rb == null)
        {
            return;
        }
        rb.velocity = direction.normalized * speed;
    }
    void OnTriggerEnter2D(Collider2D other)
    { /*
        if (other.CompareTag("Player") && !other.transform.IsChildOf(transform.parent))
        {
            PlayerController health = other.GetComponent<PlayerController>();
            if (health != null)
                health.TakeDamage(damage);
          
            Destroy(gameObject);
        }*/
    }
}
