using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float jumpForce = 5f;
    public bool flipOnMouseLeft = true;

    public int maxHealth = 3;
    public int currentHealth;

    public GameObject bullet;
    public Transform ShootPoint;
    public float ShootRate=0.2f;
    public float nextShootTime = 0f;

    private Vector3 mouseScreenPos;
    private Vector3 mouseWorldPos;

    private SpriteRenderer spriteRenderer;
    private Camera playerCamera;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isGrounded;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCamera = GetComponentInChildren<Camera>();
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
    }
    void Update()
    {
        Vector2 velocity = rb.velocity;
        velocity.x = moveInput.x * moveSpeed;  
        rb.velocity = velocity;

        mouseScreenPos = Input.mousePosition;
        mouseWorldPos = playerCamera.ScreenToWorldPoint(mouseScreenPos);
        bool shouldFlip = mouseWorldPos.x < transform.position.x;
        spriteRenderer.flipX = flipOnMouseLeft ? shouldFlip : !shouldFlip;
    }

    void OnMove(InputValue value){
        moveInput = value.Get<Vector2>();
        //Debug.Log("Move Input: " + moveInput); 
    }

    void OnJump(){
        if (isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            isGrounded = false;
        }
    }

    void OnShoot()
    {
        if (Time.time < nextShootTime || playerCamera == null) return;
        
            mouseScreenPos = Mouse.current.position.ReadValue();
            mouseScreenPos.z = Mathf.Abs(playerCamera.transform.position.z);
            mouseWorldPos = playerCamera.ScreenToWorldPoint(mouseScreenPos);
            Vector2 direction = (mouseWorldPos - ShootPoint.position).normalized;

            GameObject bulletPrefab = Instantiate(bullet, ShootPoint.position, Quaternion.identity);
            bulletPrefab.GetComponent<Bullet>().SetVelocity(direction);

        nextShootTime = Time.time + ShootRate;
       
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }
}
